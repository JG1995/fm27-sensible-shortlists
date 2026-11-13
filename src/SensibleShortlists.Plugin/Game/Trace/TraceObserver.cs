using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>
/// Feeds the interop trace. It only reads: each call turns game objects into text and hands them
/// to the session, which decides what is recorded.
/// </summary>
internal sealed class TraceObserver : IInteropObserver
{
    private readonly InteropTraceSession _session;

    public TraceObserver(InteropTraceSession session) => _session = session;

    public void EventSent(uint number, InteropReference? target, TypedValue? data) =>
        _session.EventSent(
            GameDescriber.EventName(number), number, GameDescriber.Reference(target), GameDescriber.Value(data));

    public void ChannelOpened(InteropReference? reference, PropertyID property, ulong key) =>
        _session.ChannelOpened(key, GameDescriber.Reference(reference), GameDescriber.Property(property));

    // The static describer keeps this allocation-free for values that are not traced, which is nearly all.
    public void ValuesDelivered(ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values) =>
        _session.ValuesDelivered(keys, new GameList(values), static value => GameDescriber.Value(value));

    public void NodeAdded(ulong key, ulong parent, PropertyID property) =>
        _session.NodeAdded(key, parent, GameDescriber.Property(property));

    public void NodeRemoved(ulong key) => _session.NodeRemoved(key);

    public void TargetSet(ulong key, ulong data) => _session.TargetSet(key, data);

    public void DataAdded(ulong data) => _session.DataAdded(data);

    public void DataSet(ulong data, TypedValue? value) =>
        _session.DataSet(data, value, static set => GameDescriber.Value(set));

    public void DataReleased(ulong data) => _session.DataReleased(data);
}
