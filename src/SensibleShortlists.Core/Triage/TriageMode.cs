namespace SensibleShortlists.Core.Triage;

public enum TriageMode
{
    Off,
    Observe,
}

public static class TriageModes
{
    /// <summary>
    /// A setting the plugin does not recognise turns triage off: a typo must never switch on
    /// something that reads from the game.
    /// </summary>
    public static TriageMode Parse(string? setting) =>
        string.Equals(setting?.Trim(), nameof(TriageMode.Observe), StringComparison.OrdinalIgnoreCase)
            ? TriageMode.Observe
            : TriageMode.Off;
}
