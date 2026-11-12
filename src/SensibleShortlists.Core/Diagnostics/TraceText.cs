namespace SensibleShortlists.Core.Diagnostics;

/// <summary>Turns text taken from the game into something safe to put on one trace line.</summary>
public static class TraceText
{
    public const int MaxLength = 400;

    public static string Sanitize(string? text)
    {
        if (text is null)
        {
            return "<null>";
        }

        // The limit applies to the text itself, so escaping can never push a short text over it,
        // and the cut never lands inside an escape or a surrogate pair.
        var kept = Math.Min(text.Length, MaxLength);
        if (kept < text.Length && char.IsHighSurrogate(text[kept - 1]))
        {
            kept--;
        }

        var escaped = text[..kept].Replace("\r", @"\r").Replace("\n", @"\n").Replace("\t", @"\t");
        return kept == text.Length ? escaped : $"{escaped}…(+{text.Length - kept})";
    }

    /// <summary>
    /// Runs a description that reads game objects. Those reads can fail in ways the plugin cannot
    /// predict, and a failed description must not cost the record it belongs to.
    /// </summary>
    public static string? Describe<TState>(TState state, Func<TState, string?> describe)
    {
        try
        {
            return describe(state);
        }
        catch (Exception exception)
        {
            return $"<error: {exception.GetType().Name}>";
        }
    }
}
