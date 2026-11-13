using BepInEx.Logging;
using SensibleShortlists.Core.Diagnostics;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>
/// The one place the game is hooked. The hooks hand what they see to this feed, and the feed hands
/// it to each observer. Nothing may escape from here into the game: an exception thrown into
/// IL2CPP code can take the process down, so each observer fails on its own and is reported once.
/// </summary>
internal static class InteropFeed
{
    private static readonly FirstFailureGate Failures = new();

    private static IInteropObserver[] s_observers = Array.Empty<IInteropObserver>();
    private static ManualLogSource? s_log;

    /// <param name="withNodeGraph">
    /// Also hook the node graph. It is every binding in the UI, so it is hooked only when someone
    /// has asked to see it.
    /// </param>
    public static (int Installed, int Total) Start(
        HookInstaller hooks,
        ManualLogSource log,
        IReadOnlyCollection<IInteropObserver> observers,
        bool withNodeGraph)
    {
        s_log = log;
        s_observers = observers.ToArray();

        var installed = new List<bool>
        {
            hooks.TryInstallPrefix(SendEventHook.Name, SendEventHook.Resolve),
            hooks.TryInstallPrefix(OpenChannelHook.Name, OpenChannelHook.Resolve),
            hooks.TryInstallPrefix(ChannelDataHook.Name, ChannelDataHook.Resolve),
            hooks.TryInstallPrefix(TickHook.Name, TickHook.Resolve),
        };
        if (withNodeGraph)
        {
            installed.AddRange(NodeGraphHooks.All.Select(hook => hooks.TryInstallPrefix(hook.Name, hook.Resolve)));
        }

        return (installed.Count(wasInstalled => wasInstalled), installed.Count);
    }

    public static void EventSent(uint number, InteropReference? target, TypedValue? data) =>
        Publish(nameof(EventSent), (number, target, data), static (observer, e) => observer.EventSent(e.number, e.target, e.data));

    public static void ChannelOpened(InteropReference? reference, PropertyID property, ulong key) =>
        Publish(
            nameof(ChannelOpened),
            (reference, property, key),
            static (observer, c) => observer.ChannelOpened(c.reference, c.property, c.key));

    public static void ValuesDelivered(ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values) =>
        Publish(nameof(ValuesDelivered), (keys, values), static (observer, v) => observer.ValuesDelivered(v.keys, v.values));

    public static void Tick() => Publish(nameof(Tick), 0, static (observer, _) => observer.Tick());

    public static void NodeAdded(ulong key, ulong parent, PropertyID property) =>
        Publish(nameof(NodeAdded), (key, parent, property), static (observer, n) => observer.NodeAdded(n.key, n.parent, n.property));

    public static void NodeRemoved(ulong key) =>
        Publish(nameof(NodeRemoved), key, static (observer, removed) => observer.NodeRemoved(removed));

    public static void TargetSet(ulong key, ulong data) =>
        Publish(nameof(TargetSet), (key, data), static (observer, t) => observer.TargetSet(t.key, t.data));

    public static void DataAdded(ulong data) =>
        Publish(nameof(DataAdded), data, static (observer, added) => observer.DataAdded(added));

    public static void DataSet(ulong data, TypedValue? value) =>
        Publish(nameof(DataSet), (data, value), static (observer, d) => observer.DataSet(d.data, d.value));

    public static void DataReleased(ulong data) =>
        Publish(nameof(DataReleased), data, static (observer, released) => observer.DataReleased(released));

    /// <summary>For a hook that fails before it has anything to hand to the feed.</summary>
    public static void ReportFirstFailure(string where, Exception exception)
    {
        if (Failures.ShouldReport(where))
        {
            s_log?.LogWarning($"{where} failed and will stay silent from now on: {exception}");
        }
    }

    // Static lambdas and a state argument keep this free of allocations: it runs for every binding.
    private static void Publish<TState>(string what, TState state, Action<IInteropObserver, TState> tell)
    {
        foreach (var observer in s_observers)
        {
            try
            {
                tell(observer, state);
            }
            catch (Exception exception)
            {
                ReportFirstFailure($"{observer.GetType().Name}.{what}", exception);
            }
        }
    }
}
