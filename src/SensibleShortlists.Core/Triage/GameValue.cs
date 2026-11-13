namespace SensibleShortlists.Core.Triage;

/// <summary>
/// A value read from the game, reduced to what the plugin can rely on: the text the game prints
/// for it, which is known to work for every type the game sends, and, when the value is a game
/// object or a list of them, the database records they stand for.
/// </summary>
/// <param name="Records">Null when the value is neither a game object nor a list.</param>
/// <param name="Flags">
/// The status flags a list value holds, as the game names them. Null when the value is not a list
/// of status flags.
/// </param>
public sealed record GameValue(string? Text, IReadOnlyList<uint>? Records = null, IReadOnlyList<string>? Flags = null)
{
    /// <summary>The game prints booleans as <c>True</c> and <c>False</c>; anything else is not one.</summary>
    public bool? AsFlag() => Text switch
    {
        "True" => true,
        "False" => false,
        _ => null,
    };
}
