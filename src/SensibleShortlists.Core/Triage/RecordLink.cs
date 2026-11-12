namespace SensibleShortlists.Core.Triage;

/// <summary>
/// A database record a text links to. The game packs the kind of record into the top byte and its
/// number into the rest.
/// </summary>
public readonly record struct RecordLink(uint Record)
{
    private const uint PersonKind = 1;
    private const uint TeamKind = 24;

    public uint Kind => Record >> 24;

    public uint Number => Record & 0x00FF_FFFF;

    public bool IsPerson => Kind == PersonKind;

    public bool IsTeam => Kind == TeamKind;

    public override string ToString() => $"{Kind}:{Number}";
}
