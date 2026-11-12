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

        var escaped = text.Replace("\r", @"\r").Replace("\n", @"\n").Replace("\t", @"\t");
        return escaped.Length <= MaxLength
            ? escaped
            : $"{escaped[..MaxLength]}…(+{escaped.Length - MaxLength})";
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
