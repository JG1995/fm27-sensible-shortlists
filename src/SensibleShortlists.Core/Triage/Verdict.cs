namespace SensibleShortlists.Core.Triage;

/// <summary>Whether a message is shortlist noise, and if not, the reason it must stay.</summary>
public readonly record struct Verdict(string? KeepReason)
{
    public static readonly Verdict Quiet = new(null);

    public bool IsQuiet => KeepReason is null;

    public static Verdict Keep(string reason) => new(reason);

    public override string ToString() => IsQuiet ? "QUIET" : $"KEEP({KeepReason})";
}
