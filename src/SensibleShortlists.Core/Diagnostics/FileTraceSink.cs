using System.Globalization;

namespace SensibleShortlists.Core.Diagnostics;

/// <summary>
/// Writes trace records to a file, replacing any earlier trace. Every line is flushed as it is
/// written, because the game may crash or be killed mid-session and the trace must survive that.
/// </summary>
public sealed class FileTraceSink : ITraceSink, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly Func<DateTime> _now;

    public FileTraceSink(string path, Func<DateTime> now)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        _now = now;
    }

    public void Write(string line) =>
        _writer.WriteLine($"{_now().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {line}");

    public void Dispose() => _writer.Dispose();
}
