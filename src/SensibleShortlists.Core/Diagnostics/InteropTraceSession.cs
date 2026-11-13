using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<ulong, string> _tracedChannels = new();
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
    public void EventSent(string? name, uint number, string? target, string? data)
    {
        lock (_gate)
        {
            _sink.Write(
                $"EVENT {TraceText.Sanitize(name)}({number}) target={TraceText.Sanitize(target)} data={TraceText.Sanitize(data)}");
        }
    }

    public void ChannelOpened(ulong key, string? reference, string? property)
    {
        var label = $"{TraceText.Sanitize(reference)}.{TraceText.Sanitize(property)}";
        if (!_filter.Matches(label))
        {
            // The game reuses keys. A key that now stands for an untraced channel must not keep
            // reporting values under its old label.
            _tracedChannels.TryRemove(key, out _);
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
    /// static describer: the common case costs one lock-free lookup and no allocation.
    /// </summary>
    public void ValueChanged<TValue>(ulong key, TValue value, Func<TValue, string?> describe)
    {
        if (!_tracedChannels.TryGetValue(key, out var label))
        {
            return;
        }

        // Describing reads game objects. That must happen outside the lock: the game may answer by
        // calling back into a hook on another thread, which would then wait on this one forever.
        var description = TraceText.Sanitize(TraceText.Describe(value, describe));
        lock (_gate)
        {
            _sink.Write($"VALUE key={key} {label} = {description}");
        }
    }

    // The node graph is how the UI tells the simulation what a binding hangs off: every binding is a
    // node with a parent, and a node can point at data such as a game object. It is recorded whole,
    // not filtered: a node of interest is understood only through the nodes above it, and those
    // carry no property to filter on.

    public void NodeAdded(ulong key, ulong parent, string? property) =>
        Write($"NODE key={key} parent={parent} property={TraceText.Sanitize(property)}");

    public void NodeRemoved(ulong key) => Write($"UNNODE key={key}");

    public void TargetSet(ulong key, ulong data) => Write($"TARGET key={key} data={data}");

    public void DataAdded(ulong data) => Write($"DATA key={data}");

    public void DataSet<TValue>(ulong data, TValue value, Func<TValue, string?> describe) =>
        Write($"DATA key={data} = {TraceText.Sanitize(TraceText.Describe(value, describe))}");

    public void DataReleased(ulong data) => Write($"UNDATA key={data}");

    private void Write(string line)
    {
        lock (_gate)
        {
            _sink.Write(line);
        }
    }

    /// <summary>
    /// The game delivers values in batches: a list of channel keys and a list of values. Whether the
    /// two always line up is not known, so a batch that does not is noted rather than dropped.
    /// </summary>
    public void ValuesDelivered<TValue>(
        IReadOnlyList<ulong> keys,
        IReadOnlyList<TValue> values,
        Func<TValue, string?> describe)
    {
        if (keys.Count != values.Count && keys.Any(_tracedChannels.ContainsKey))
        {
            lock (_gate)
            {
                _sink.Write($"NOTE value batch has {keys.Count} keys and {values.Count} values; paired by position");
            }
        }

        for (var i = 0; i < keys.Count && i < values.Count; i++)
        {
            ValueChanged(keys[i], values[i], describe);
        }
    }
}
