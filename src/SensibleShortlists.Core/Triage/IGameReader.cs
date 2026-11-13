namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Asks the game for one property of one object. The answer arrives later, exactly once, and is
/// null when the property could not be read.
/// </summary>
public interface IGameReader
{
    /// <param name="message">The game's own handle for the message, as it was listed.</param>
    void ReadMessage(object message, string property, Action<GameValue?> onValue);

    /// <summary>Reads from the object behind the Shortlist screen of one manager at one team.</summary>
    void ReadShortlist(uint manager, uint team, string property, Action<GameValue?> onValue);
}
