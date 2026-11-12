using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Tests.Diagnostics;

public sealed class FileTraceSinkTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"trace-{Guid.NewGuid():N}.log");

    private static readonly DateTime Noon = new(2026, 11, 12, 12, 3, 44, 512);

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Each_line_is_prefixed_with_the_time_of_day()
    {
        using (var sink = new FileTraceSink(_path, () => Noon))
        {
            sink.Write("EVENT one");
        }

        Assert.Equal(new[] { "12:03:44.512 EVENT one" }, File.ReadAllLines(_path));
    }

    [Fact]
    public void A_line_is_on_disk_before_the_sink_is_closed()
    {
        using var sink = new FileTraceSink(_path, () => Noon);

        sink.Write("EVENT one");

        Assert.Equal(new[] { "12:03:44.512 EVENT one" }, ReadWhileOpen());
    }

    [Fact]
    public void Opening_the_sink_discards_an_earlier_trace()
    {
        File.WriteAllText(_path, "stale line\n");

        using (var sink = new FileTraceSink(_path, () => Noon))
        {
            sink.Write("EVENT fresh");
        }

        Assert.Equal(new[] { "12:03:44.512 EVENT fresh" }, File.ReadAllLines(_path));
    }

    private string[] ReadWhileOpen()
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
    }
}
