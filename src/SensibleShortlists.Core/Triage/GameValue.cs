namespace SensibleShortlists.Core.Triage;

/// <summary>
/// A value read from the game, reduced to text: what the game prints for it, and for a list, what
/// it prints for each item. Reading text is the one way of reading values that is known to work
/// for every type the game sends.
/// </summary>
public sealed record GameValue(string? Text, IReadOnlyList<string>? Items)
{
    /// <summary>The game prints booleans as <c>True</c> and <c>False</c>; anything else is not one.</summary>
    public bool? AsFlag() => Text switch
    {
        "True" => true,
        "False" => false,
        _ => null,
    };
}
