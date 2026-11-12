namespace SensibleShortlists.Core.Diagnostics;

/// <summary>
/// Records the traffic between the game's UI and its simulation core: events going out, and the
/// values that come back on the property channels the filter selects.
/// </summary>
public sealed class InteropTraceSession
{
    public const int DefaultChannelCapacity = 20_000;

    private readonly TraceFilter _filter;
    private readonly ITraceSink _sink;
    private readonly int _channelCapacity;
    private readonly Dictionary<ulong, string> _tracedChannels = new();
    private readonly object _gate = new();

    public InteropTraceSession(
        TraceFilter filter,
        ITraceSink sink,
        int channelCapacity = DefaultChannelCapacity)
    {
        _filter = filter;
        _sink = sink;
        _channelCapacity = channelCapacity;
    }

    /// <summary>Events are rare and each one is a user action, so the filter does not apply.</summary>
    public void EventSent(string name, uint number, string? target, string? data)
    {
        lock (_gate)
        {
            _sink.Write(
                $"EVENT {name}({number}) target={TraceText.Sanitize(target)} data={TraceText.Sanitize(data)}");
        }
    }

    public void ChannelOpened(ulong key, string? reference, string? property)
    {
        var label = TraceText.Sanitize($"{reference}.{property}");
        if (!_filter.Matches(label))
        {
            return;
        }

        lock (_gate)
        {
            // The game opens channels for as long as it runs. Forgetting everything is crude, but it
            // bounds memory, and a channel still in use is reopened or simply stops being traced.
            if (_tracedChannels.Count >= _channelCapacity && !_tracedChannels.ContainsKey(key))
            {
                _tracedChannels.Clear();
                _sink.Write($"NOTE channel capacity {_channelCapacity} reached; forgot all traced channels");
            }

            _tracedChannels[key] = label;
            _sink.Write($"CHANNEL key={key} {label}");
        }
    }

    /// <summary>
    /// Values arrive for every bound property in the game, thousands per screen. The description is
    /// only computed for traced channels, and the value is passed as state so callers can use a
    /// static describer: the common case costs one dictionary lookup and no allocation.
    /// </summary>
    public void ValueChanged<TValue>(ulong key, TValue value, Func<TValue, string?> describe)
    {
        lock (_gate)
        {
            if (!_tracedChannels.TryGetValue(key, out var label))
            {
                return;
            }

            _sink.Write($"VALUE key={key} {label} = {TraceText.Sanitize(TraceText.Describe(value, describe))}");
        }
    }
}
