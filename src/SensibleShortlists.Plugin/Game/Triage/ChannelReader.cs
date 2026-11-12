using FM.GamePlugin;
using SensibleShortlists.Core.Triage;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Triage;

/// <summary>
/// Reads properties from the game the way its own UI does: open a channel for one property of one
/// object under a key of the plugin's own, take the first value the game sends for that key, and
/// close the channel.
/// </summary>
/// <remarks>
/// Reads are queued and made on the game's tick, a few at a time. A read asked for from inside a
/// hook would otherwise call into the interop layer while the game is in the middle of using it,
/// and an inbox of a hundred messages would otherwise open several hundred channels in one frame.
/// </remarks>
internal sealed class ChannelReader : IGameReader
{
    private const int ReadsPerTick = 48;

    // Keys the UI uses are hashes of binding paths. The plugin counts up from a fixed start instead.
    private const ulong FirstKey = 0x5353_4C52_0000_0000;

    private readonly PropertyCatalog _properties;
    private readonly Queue<(Action<GameInteropSubsystem> Call, Action<GameValue?>? OnValue)> _queue = new();
    private readonly Dictionary<ulong, Action<GameValue?>> _waiting = new();
    private ulong _nextKey = FirstKey;
    private int? _game;

    public ChannelReader(PropertyCatalog properties) => _properties = properties;

    public void ReadMessage(object message, string property, Action<GameValue?> onValue)
    {
        var reference = (InteropReference)message;
        if (_game is null && GameReferences.TryGetGame(reference, out var game))
        {
            _game = game;
        }

        _queue.Enqueue((interop => Open(interop, reference, property, onValue), onValue));
    }

    public void ReadPerson(uint record, string property, Action<GameValue?> onValue) =>
        _queue.Enqueue((
            interop =>
            {
                if (_game is not { } game)
                {
                    onValue(null);
                    return;
                }

                Open(interop, GameReferences.Person(record, game), property, onValue);
            },
            onValue));

    /// <summary>Makes the queued calls into the game. Called on the game's tick.</summary>
    public void Flush()
    {
        if (_queue.Count == 0 || !GameSubsystems.TryGet<GameInteropSubsystem>(out var interop))
        {
            return;
        }

        for (var made = 0; made < ReadsPerTick && _queue.TryDequeue(out var queued); made++)
        {
            // A read that cannot be made is answered as unreadable, so whoever asked can still finish.
            try
            {
                queued.Call(interop);
            }
            catch (Exception exception)
            {
                InteropFeed.ReportFirstFailure(nameof(ChannelReader), exception);
                queued.OnValue?.Invoke(null);
            }
        }
    }

    /// <summary>Takes a delivered value if it answers one of the plugin's reads.</summary>
    public bool TryDeliver(ulong key, TypedValue? value)
    {
        if (!_waiting.Remove(key, out var onValue))
        {
            return false;
        }

        _queue.Enqueue((interop => interop.CloseChannel(GameReferences.Key(key)), null));
        onValue(value is null ? null : new GameValue(value.AsString()));
        return true;
    }

    private void Open(GameInteropSubsystem interop, InteropReference target, string property, Action<GameValue?> onValue)
    {
        if (!_properties.TryFind(target, property, out var id))
        {
            onValue(null);
            return;
        }

        var key = _nextKey++;
        _waiting[key] = onValue;
        try
        {
            interop.OpenChannel(target, id, GameReferences.Key(key));
        }
        catch
        {
            _waiting.Remove(key);
            throw;
        }
    }
}
