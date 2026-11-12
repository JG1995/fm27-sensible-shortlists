namespace SensibleShortlists.Core.Diagnostics;

/// <summary>Decides which channels are worth tracing, by case-insensitive terms in their label.</summary>
public sealed class TraceFilter
{
    private readonly string[] _terms;

    private TraceFilter(string[] terms) => _terms = terms;

    /// <summary>Parses comma-separated terms. No terms means every label matches.</summary>
    public static TraceFilter Parse(string? terms) =>
        new((terms ?? string.Empty).Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public bool Matches(string label) =>
        _terms.Length == 0
        || _terms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase));
}
