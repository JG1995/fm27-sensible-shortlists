using System.Text;

namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Text as the game stores it: plain text with links to database records woven in. A link is
/// 0x01, base64 data, 0x02, the visible text, 0x01.
/// </summary>
public sealed record EmbeddedText(string Plain, IReadOnlyList<RecordLink> Links)
{
    private const char LinkEdge = '\u0001';
    private const char DataEnd = '\u0002';

    // The tag that precedes the linked record inside the decoded link data.
    private static readonly byte[] RecordTag = Encoding.ASCII.GetBytes("ferd");

    public static EmbeddedText Parse(string text)
    {
        var plain = new StringBuilder(text.Length);
        var links = new List<RecordLink>();
        var position = 0;
        while (position < text.Length)
        {
            var start = text.IndexOf(LinkEdge, position);
            var dataEnd = start < 0 ? -1 : text.IndexOf(DataEnd, start + 1);
            var end = dataEnd < 0 ? -1 : text.IndexOf(LinkEdge, dataEnd + 1);
            if (end < 0)
            {
                break;
            }

            // An edge with another edge before its data ends does not open a link; the next one may.
            var nextEdge = text.IndexOf(LinkEdge, start + 1);
            if (nextEdge < dataEnd)
            {
                plain.Append(text, position, nextEdge - position);
                position = nextEdge;
                continue;
            }

            plain.Append(text, position, start - position).Append(text, dataEnd + 1, end - dataEnd - 1);
            if (TryReadRecord(text.AsSpan(start + 1, dataEnd - start - 1), out var record))
            {
                links.Add(new RecordLink(record));
            }

            position = end + 1;
        }

        return new EmbeddedText(plain.Append(text, position, text.Length - position).ToString(), links);
    }

    private static bool TryReadRecord(ReadOnlySpan<char> base64, out uint record)
    {
        record = 0;
        var data = new byte[base64.Length];
        if (!Convert.TryFromBase64Chars(base64, data, out var length))
        {
            return false;
        }

        var tag = data.AsSpan(0, length).IndexOf(RecordTag);
        if (tag < 0 || tag + RecordTag.Length + sizeof(uint) > length)
        {
            return false;
        }

        record = BitConverter.ToUInt32(data, tag + RecordTag.Length);
        return true;
    }
}
