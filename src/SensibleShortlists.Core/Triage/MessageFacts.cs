namespace SensibleShortlists.Core.Triage;

/// <summary>
/// What the plugin learned about one message. A null means the game could not be read for that
/// fact, which is different from the fact being false.
/// </summary>
/// <param name="Title">The title as plain text.</param>
/// <param name="Links">The records the title links to, in order.</param>
/// <param name="Shortlisted">For each linked person, whether they are on the manager's shortlist.</param>
public sealed record MessageFacts(
    long Id,
    bool? CanRespond,
    bool? Responded,
    bool? Priority,
    bool? Forwarded,
    string? Title,
    IReadOnlyList<RecordLink> Links,
    IReadOnlyDictionary<uint, bool?> Shortlisted);
