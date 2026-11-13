using FM.UI;
using SensibleShortlists.Core.Hiding;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Screen;

/// <summary>
/// Carries out what <see cref="MessageScreen"/> decides. As the game delivers an inbox value, it
/// puts the value the screen should get in its place, before the game's handler reads the batch.
/// On the tick it gives the screen what a verdict has changed since.
/// </summary>
/// <remarks>
/// It must come after the triage's observer in the feed. The triage has to see the game's own
/// list to learn of a new message, and the screen has to be asked after the triage has heard.
/// Anything that goes wrong here stops the hiding for the session: a screen that shows too much
/// is the safe failure.
/// </remarks>
internal sealed class ScreenObserver : IInteropObserver
{
    private readonly MessageScreen _screen;
    private readonly ScreenValues _values = new();
    private readonly ScreenWriter _writer = new();

    public ScreenObserver(MessageScreen screen) => _screen = screen;

    public void ChannelOpened(InteropReference? reference, PropertyID property, ulong key)
    {
        // Runs for every binding in the game. Only a key that carried the inbox has anything to forget.
        var carried = _screen.RoleOf(key) != ScreenRole.None;
        if (_screen.ChannelOpened(key, GameDescriber.Property(property)) != ScreenRole.None || carried)
        {
            _values.Forget(key);
        }
    }

    public void ValuesDelivered(InteropDataHandler handler, ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values)
    {
        for (var i = 0; i < keys.Length && i < values.Count; i++)
        {
            var role = _screen.RoleOf(keys[i]);
            if (role == ScreenRole.None)
            {
                continue;
            }

            try
            {
                _writer.Seen(handler);
                if (InPlaceOf(role, keys[i], values[i]) is { } other)
                {
                    values[i] = other;
                }
            }
            catch (Exception exception)
            {
                Stop(nameof(ValuesDelivered), exception);
                return;
            }
        }
    }

    public void Tick()
    {
        var updates = _screen.Refresh();
        if (updates.Count == 0)
        {
            return;
        }

        try
        {
            _writer.Begin();
            try
            {
                foreach (var update in updates)
                {
                    if (!_writer.TrySet(update.Key, ValueFor(update)))
                    {
                        // The binding no longer exists.
                        _values.Forget(update.Key);
                        _screen.Forget(update.Key);
                    }
                }
            }
            finally
            {
                _writer.End();
            }
        }
        catch (Exception exception)
        {
            Stop(nameof(Tick), exception);
        }
    }

    // The value to give the screen in the delivered one's place, or null to leave the game's alone.
    private TypedValue? InPlaceOf(ScreenRole role, ulong key, TypedValue? delivered)
    {
        switch (role)
        {
            case ScreenRole.InboxList or ScreenRole.IncomingList
                when _values.RememberList(key, delivered, role == ScreenRole.InboxList) is { } messages:
                return _screen.ListDelivered(key, messages) is { } places ? _values.List(key, places) : null;

            case ScreenRole.MessageToOpen when _values.RememberMessage(key, delivered) is { } message:
                return _screen.MessageToOpenDelivered(key, message) is { } other ? _values.Message(key, other) : null;

            case ScreenRole.UnreadPosition when ScreenValues.PositionIn(delivered) is { } position:
                return _screen.PositionDelivered(key, position) is { } moved ? ScreenValues.Position(moved) : null;

            default:
                // Not what the channel carries. What was delivered on it before no longer stands.
                _screen.Forget(key);
                return null;
        }
    }

    private TypedValue ValueFor(ScreenUpdate update) => update switch
    {
        ListUpdate list => _values.List(list.Key, list.Positions),
        MessageToOpenUpdate open => _values.Message(open.Key, open.Message),
        PositionUpdate position => ScreenValues.Position(position.Position),
        _ => throw new InvalidOperationException($"Nothing knows how to give the screen a {update.GetType().Name}."),
    };

    private void Stop(string where, Exception exception)
    {
        var place = $"{nameof(ScreenObserver)}.{where}";
        _screen.Stop(place);
        InteropFeed.ReportFirstFailure(place, exception);
    }
}
