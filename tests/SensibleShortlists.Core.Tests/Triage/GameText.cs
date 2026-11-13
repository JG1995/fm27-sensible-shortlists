using System.Text;

namespace SensibleShortlists.Core.Tests.Triage;

/// <summary>Builds strings the way the game writes them, from a link captured in a live trace.</summary>
internal static class GameText
{
    public const uint Brighton = (24u << 24) | 504;
    public const uint ManagerTeam = (24u << 24) | 565;
    public const uint ManagerClub = (3u << 24) | 439;
    public const uint BrightonClub = (3u << 24) | 504;
    public const uint Player = (1u << 24) | 155126;
    public const uint OtherPlayer = (1u << 24) | 95733;

    // The bytes around the record in link data captured for "Brighton & Hove Albion".
    private static readonly byte[] BeforeRecord = Convert.FromHexString(
        "03000000" + "20313131" + "012203" + "20303131" + "1c000000" + "64697547" + "75975c00" + "66657264");

    private static readonly byte[] AfterRecord = Convert.FromHexString("59634f4f" + "010301" + "746e6968" + "011a");

    public static string Link(uint record, string visibleText, string hint = "Click to view")
    {
        var hintBytes = Encoding.UTF8.GetBytes(hint);
        var data = BeforeRecord
            .Concat(BitConverter.GetBytes(record))
            .Concat(AfterRecord)
            .Concat(BitConverter.GetBytes(hintBytes.Length))
            .Concat(hintBytes)
            .ToArray();
        return $"\u0001{Convert.ToBase64String(data)}\u0002{visibleText}\u0001";
    }
}
