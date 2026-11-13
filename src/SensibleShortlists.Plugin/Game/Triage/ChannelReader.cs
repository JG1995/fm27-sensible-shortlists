using FM.GamePlugin;
using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Triage;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Triage;

/// <summary>
/// Reads properties from the game the way its own UI does: register a node under a key of the
/// plugin's own, open a channel on it for one property of one object, take the first value the
/// game sends for that key, then close the channel and remove the node.
/// </summary>
/// <remarks>
/// Reads are queued and made on the game's tick, a few at a time. A read asked for from inside a
/// hook would otherwise call into the interop layer while the game is in the middle of using it,
/// and an inbox of a hundred messages would otherwise open several hundred channels in one frame.
/// </remarks>
internal sealed class ChannelReader : IGameReader
{
    private const int ReadsPerTick = 48;

    // The game answers its own UI within a tick or two.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    // How a team context reaches the simulation is not known yet. Until a live run shows it, a read
    // in the context of a team tries every form that is safe to try: no context, and the team
    // carried on the person's reference under a key the game is seen to use for a context or a team
    // elsewhere. This is a probe, and it is the one place the adapter chooses between forms; once a
    // live run shows the right one, the others go.
    private static readonly string?[] TeamContextCodes = { null, "ctxt", "Ttea", "team" };

    // Keys the UI uses are hashes of binding paths. The plugin counts up from a fixed start instead.
    private const ulong FirstKey = 0x5353_4C52_0000_0000;

    private readonly PropertyCatalog _properties;
    private readonly ITraceSink _log;
    private readonly Queue<(Action<GameInteropSubsystem> Call, Action<GameValue?> OnValue)> _queue = new();
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly ReadLedger _waiting;
    private readonly List<ulong> _finished = new();
    private readonly List<ulong> _nodesOnly = new();
    private ulong _nextKey = FirstKey;
    private bool _contextsNoted;

    public ChannelReader(PropertyCatalog properties, ITraceSink log)
    {
        _properties = properties;
        _log = log;
        _waiting = new ReadLedger(Patience, () => _clock.Elapsed);
    }

    /// <summary>The running game's identifier, needed to refer to an object in it.</summary>
    public int? Game { get; set; }

    public void ReadMessage(object message, string property, Action<GameValue?> onValue)
    {
        var reference = (InteropReference)message;
        _queue.Enqueue((interop => Open(interop, reference, property, onValue), onValue));
    }

    public void ReadShortlist(uint manager, uint team, string property, Action<GameValue?> onValue) =>
        _queue.Enqueue((
            interop =>
            {
                if (Game is not { } game)
                {
                    onValue(null);
                    return;
                }

                Open(interop, GameReferences.Shortlist(manager, team, game), property, onValue);
            },
            onValue));

    public void ReadPersonInTeam(uint person, uint team, string property, Action<GameValue?> onValue) =>
        _queue.Enqueue((
            interop =>
            {
                if (Game is not { } game)
                {
                    onValue(null);
                    return;
                }

                // Every form is tried and every answer recorded, so one session shows which form
                // works. Whoever asked gets the first form, in order, that answered with something.
                var answers = new GameValue?[TeamContextCodes.Length];
                var waitingFor = answers.Length;
                for (var i = 0; i < TeamContextCodes.Length; i++)
                {
                    var form = i;
                    var label = TeamContextCodes[i] ?? "no context";

                    void Answered(GameValue? value)
                    {
                        var empty = value is null || (string.IsNullOrEmpty(value.Text) && value.Records is null);
                        answers[form] = empty ? null : value! with { Text = $"[{label}] {value.Text}" };
                        _log.Write($"NOTE status of {person} as [{label}]: {(empty ? "nothing" : value!.Text)}");
                        if (--waitingFor == 0)
                        {
                            onValue(answers.FirstOrDefault(answer => answer is not null));
                        }
                    }

                    try
                    {
                        var reference = GameReferences.Person(person, game, TeamContextCodes[i], team);
                        NoteContextsOnce(reference, property);
                        Open(interop, reference, property, Answered);
                    }
                    catch (Exception exception)
                    {
                        InteropFeed.ReportFirstFailure($"{nameof(ChannelReader)}.{nameof(ReadPersonInTeam)}", exception);
                        Answered(null);
                    }
                }
            },
            onValue));

    // What the game itself says a property needs. Written once, for whoever works out the context.
    private void NoteContextsOnce(InteropReference reference, string property)
    {
        if (_contextsNoted || !_properties.TryFind(reference, property, out var id))
        {
            return;
        }

        _contextsNoted = true;
        try
        {
            var contexts = new Il2CppSystem.Collections.Generic.List<ContextID>();
            reference.GetContexts(id.id, contexts);
            var names = new List<string>();
            for (var i = 0; i < contexts.Count; i++)
            {
                names.Add($"{GameReferences.CodeName(contexts[i].id)}:{contexts[i].id}");
            }

            _log.Write($"NOTE contexts of {property}: {(names.Count == 0 ? "none" : string.Join(" ", names))}");
        }
        catch (Exception exception)
        {
            _log.Write($"NOTE contexts of {property}: <error: {exception.GetType().Name}>");
        }
    }

    /// <summary>Makes the queued calls into the game. Called on the game's tick.</summary>
    public void Flush()
    {
        var givenUp = _waiting.GiveUpOverdue();
        if (givenUp.Count > 0)
        {
            _finished.AddRange(givenUp);
            _log.Write($"NOTE the game did not answer {givenUp.Count} reads; they count as unreadable");
        }

        if ((_queue.Count == 0 && _finished.Count == 0 && _nodesOnly.Count == 0)
            || !GameSubsystems.TryGet<GameInteropSubsystem>(out var interop))
        {
            return;
        }

        GiveBack(interop);

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
                queued.OnValue(null);
            }
        }
    }

    // Giving back what the plugin took is not rationed. The lists are emptied whatever happens:
    // a key the game refuses to release must not be offered again on every tick, blocking all reads.
    private void GiveBack(GameInteropSubsystem interop)
    {
        try
        {
            foreach (var key in _finished)
            {
                interop.CloseChannel(GameReferences.Key(key));
                interop.RemoveNode(GameReferences.Key(key));
            }

            // A read that failed between registering its node and opening its channel has only a node.
            foreach (var key in _nodesOnly)
            {
                interop.RemoveNode(GameReferences.Key(key));
            }
        }
        catch (Exception exception)
        {
            InteropFeed.ReportFirstFailure($"{nameof(ChannelReader)}.{nameof(GiveBack)}", exception);
        }
        finally
        {
            _finished.Clear();
            _nodesOnly.Clear();
        }
    }

    /// <summary>Takes a delivered value if it answers one of the plugin's reads.</summary>
    public bool TryDeliver(ulong key, TypedValue? value)
    {
        if (!_waiting.IsExpected(key))
        {
            return false;
        }

        _finished.Add(key);

        // Whatever goes wrong reading the value, whoever asked still gets an answer, and a value
        // whose records cannot be read still gives its text.
        string? text = null;
        IReadOnlyList<uint>? records = null;
        try
        {
            text = value?.AsString();
            records = value is null ? null : GameReferences.Records(value);
        }
        catch (Exception exception)
        {
            InteropFeed.ReportFirstFailure(nameof(ChannelReader), exception);
        }

        return _waiting.Answer(key, text is null ? null : new GameValue(text, records));
    }

    private void Open(GameInteropSubsystem interop, InteropReference target, string property, Action<GameValue?> onValue)
    {
        if (!_properties.TryFind(target, property, out var id))
        {
            onValue(null);
            return;
        }

        // The simulation answers a channel only on a node it knows. The game's UI registers every
        // binding as a node before opening a channel on it; a node at the root has no parent.
        var key = _nextKey++;
        interop.AddNode(GameReferences.Key(key), default, id);
        try
        {
            interop.OpenChannel(target, id, GameReferences.Key(key));
        }
        catch
        {
            _nodesOnly.Add(key);
            throw;
        }

        _waiting.Expect(key, onValue);
    }
}
