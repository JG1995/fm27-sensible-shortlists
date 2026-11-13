namespace SensibleShortlists.Core.Triage;

/// <summary>
/// What the plugin learned about one message. A null means the game could not be read for that
/// fact, which is different from the fact being false.
/// </summary>
/// <param name="Read">Whether the game counts the message as read. No verdict rule uses it.</param>
/// <param name="Title">The title as plain text.</param>
/// <param name="Links">The records the title links to, in order.</param>
/// <param name="Clubs">
/// For each person the title links, the clubs that person belongs to: the one played for and the
/// one holding the contract. Empty for a person without a club, null when the game could not be
/// read for that person.
/// </param>
public sealed record MessageFacts(
    long Id,
    bool? Responded,
    bool? Priority,
    bool? Forwarded,
    bool? Read,
    string? Title,
    IReadOnlyList<RecordLink> Links,
    IReadOnlyDictionary<uint, IReadOnlySet<uint>?> Clubs);
