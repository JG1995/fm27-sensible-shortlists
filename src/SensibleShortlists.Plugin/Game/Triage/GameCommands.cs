using FM.GamePlugin;
using SensibleShortlists.Core.Triage;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Triage;

/// <summary>
/// Changes the game by sending it the events its own UI sends. Like every call the plugin makes
/// into the game, an event waits for the game's tick: a command asked for from inside a hook would
/// otherwise reach the interop layer while the game is in the middle of using it.
/// </summary>
internal sealed class GameCommands : IGameCommands
{
    private readonly Queue<Action<GameInteropSubsystem>> _queue = new();

    public void MarkRead(object message)
    {
        // Whoever asks is in the middle of handling a message; a command that cannot be prepared
        // is dropped and reported, and must not stop the rest of that.
        try
        {
            // The game sends this event to a message when the manager opens it. Sent alone, it
            // marks the message without opening it.
            var target = GameReferences.MessageToMark((InteropReference)message)
                ?? throw new InvalidOperationException("The message does not say which game, manager, or number it is.");
            _queue.Enqueue(interop => interop.SendEvent((uint)InteropEventIDs.EventFmxMarkPortalMessageAsRead, target, null));
        }
        catch (Exception exception)
        {
            InteropFeed.ReportFirstFailure($"{nameof(GameCommands)}.{nameof(MarkRead)}", exception);
        }
    }

    /// <summary>Sends what is waiting. Called on the game's tick, before that tick's reads.</summary>
    public void Flush()
    {
        if (_queue.Count == 0 || !GameSubsystems.TryGet<GameInteropSubsystem>(out var interop))
        {
            return;
        }

        // A command that cannot be sent is dropped, not retried: the game refused it once.
        while (_queue.TryDequeue(out var send))
        {
            try
            {
                send(interop);
            }
            catch (Exception exception)
            {
                InteropFeed.ReportFirstFailure(nameof(GameCommands), exception);
            }
        }
    }
}
