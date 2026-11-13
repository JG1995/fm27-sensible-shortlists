namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Asks the game for one property of one object. The answer arrives later, exactly once, and is
/// null when the property could not be read.
/// </summary>
public interface IGameReader
{
    /// <param name="message">The game's own handle for the message, as it was listed.</param>
    void ReadMessage(object message, string property, Action<GameValue?> onValue);

    /// <summary>Reads a property of a person, named by the database record a link points to.</summary>
    void ReadPerson(uint person, string property, Action<GameValue?> onValue);
}
