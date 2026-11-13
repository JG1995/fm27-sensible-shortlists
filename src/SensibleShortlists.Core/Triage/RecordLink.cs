namespace SensibleShortlists.Core.Triage;

/// <summary>
/// A database record a text links to. The game packs the kind of record into the top byte and its
/// number into the rest.
/// </summary>
public readonly record struct RecordLink(uint Record)
{
    private const uint PersonKind = 1;
    private const uint TeamKind = 24;
    private const uint ClubKind = 3;

    public uint Kind => Record >> 24;

    public uint Number => Record & 0x00FF_FFFF;

    public bool IsPerson => Kind == PersonKind;

    public bool IsTeam => Kind == TeamKind;

    /// <summary>A club is a record of its own, apart from each of its teams.</summary>
    public bool IsClub => Kind == ClubKind;

    public override string ToString() => $"{Kind}:{Number}";
}
