namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Changes something in the game. Nothing comes back: whoever asks for a change reads the game
/// afterwards if it needs to know what happened.
/// </summary>
public interface IGameCommands
{
    /// <summary>Marks a message as read, as opening it in Messages would, without opening it.</summary>
    /// <param name="message">The game's own handle for the message, as it was listed.</param>
    void MarkRead(object message);
}
