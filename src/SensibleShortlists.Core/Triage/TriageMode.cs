namespace SensibleShortlists.Core.Triage;

public enum TriageMode
{
    /// <summary>Do nothing.</summary>
    Off,

    /// <summary>Judge each inbox message and record the verdict. Change nothing in the game.</summary>
    Observe,

    /// <summary>Judge and record, and mark quiet messages as read.</summary>
    Quiet,

    /// <summary>As <see cref="Quiet"/>, and keep quiet messages out of what the screen lists.</summary>
    Hide,
}

public static class TriageModes
{
    /// <summary>
    /// A setting the plugin does not recognise turns triage off: a typo must never switch on
    /// something that reads from the game, still less something that changes it.
    /// </summary>
    public static TriageMode Parse(string? setting) => setting?.Trim().ToLowerInvariant() switch
    {
        "observe" => TriageMode.Observe,
        "quiet" => TriageMode.Quiet,
        "hide" => TriageMode.Hide,
        _ => TriageMode.Off,
    };

    public static bool MarksRead(this TriageMode mode) => mode is TriageMode.Quiet or TriageMode.Hide;

    public static bool Hides(this TriageMode mode) => mode == TriageMode.Hide;
}
