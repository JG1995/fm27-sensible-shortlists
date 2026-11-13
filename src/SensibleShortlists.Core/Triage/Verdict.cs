namespace SensibleShortlists.Core.Triage;

/// <summary>Whether a message is shortlist noise, and if not, the reason it must stay.</summary>
/// <remarks>
/// A quiet verdict is acted on, so it has to be given on purpose: a verdict left at its default
/// keeps the message.
/// </remarks>
public readonly record struct Verdict
{
    public static readonly Verdict Quiet = new(isQuiet: true, keepReason: null);

    private Verdict(bool isQuiet, string? keepReason)
    {
        IsQuiet = isQuiet;
        KeepReason = keepReason;
    }

    public bool IsQuiet { get; }

    public string? KeepReason { get; }

    public static Verdict Keep(string reason) => new(isQuiet: false, reason);

    public override string ToString() => IsQuiet ? "QUIET" : $"KEEP({KeepReason ?? "no verdict"})";
}
