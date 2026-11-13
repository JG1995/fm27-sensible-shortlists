namespace SensibleShortlists.Core.Triage;

/// <summary>
/// What the plugin learned about one message. A null means the game could not be read for that
/// fact, which is different from the fact being false.
/// </summary>
/// <param name="Read">Whether the game counts the message as read. No verdict rule uses it.</param>
/// <param name="Title">The title as plain text.</param>
/// <param name="Links">The records the title links to, in order.</param>
/// <param name="Shortlist">The people on the manager's shortlist when the message was listed.</param>
public sealed record MessageFacts(
    long Id,
    bool? CanRespond,
    bool? Responded,
    bool? Priority,
    bool? Forwarded,
    bool? Read,
    string? Title,
    IReadOnlyList<RecordLink> Links,
    IReadOnlySet<uint>? Shortlist);
