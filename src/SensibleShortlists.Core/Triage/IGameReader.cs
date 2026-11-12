namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Asks the game for one property of one object. The answer arrives later, at most once, and is
/// null when the property could not be read. A read the game never answers never calls back.
/// </summary>
public interface IGameReader
{
    /// <param name="message">The game's own handle for the message, as it was listed.</param>
    void ReadMessage(object message, string property, Action<GameValue?> onValue);

    /// <param name="record">The person's database record, as found in a link.</param>
    void ReadPerson(uint record, string property, Action<GameValue?> onValue);
}
