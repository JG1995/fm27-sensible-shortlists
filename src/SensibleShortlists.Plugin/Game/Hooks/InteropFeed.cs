using BepInEx.Logging;
using FM.UI;
using SensibleShortlists.Core.Diagnostics;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>
/// The one place the game is hooked. The hooks hand what they see to this feed, and the feed hands
/// it to each observer, in the order the observers were given. Nothing may escape from here into the game: an exception thrown into
/// IL2CPP code can take the process down, so each observer fails on its own and is reported once.
/// </summary>
internal static class InteropFeed
{
    private static readonly FirstFailureGate Failures = new();

    private static IInteropObserver[] s_observers = Array.Empty<IInteropObserver>();
    private static ManualLogSource? s_log;

    public static (int Installed, int Total) Start(
        HookInstaller hooks,
        ManualLogSource log,
        IReadOnlyCollection<IInteropObserver> observers)
    {
        s_log = log;
        s_observers = observers.ToArray();

        var installed = new[]
        {
            hooks.TryInstallPrefix(SendEventHook.Name, SendEventHook.Resolve),
            hooks.TryInstallPrefix(OpenChannelHook.Name, OpenChannelHook.Resolve),
            hooks.TryInstallPrefix(ChannelDataHook.Name, ChannelDataHook.Resolve),
            hooks.TryInstallPrefix(TickHook.Name, TickHook.Resolve),
        };

        return (installed.Count(wasInstalled => wasInstalled), installed.Length);
    }

    public static void EventSent(uint number, InteropReference? target, TypedValue? data) =>
        Publish(nameof(EventSent), (number, target, data), static (observer, e) => observer.EventSent(e.number, e.target, e.data));

    public static void ChannelOpened(InteropReference? reference, PropertyID property, ulong key) =>
        Publish(
            nameof(ChannelOpened),
            (reference, property, key),
            static (observer, c) => observer.ChannelOpened(c.reference, c.property, c.key));

    public static void ValuesDelivered(
        InteropDataHandler handler,
        ulong[] keys,
        Il2CppSystem.Collections.Generic.List<TypedValue> values) =>
        Publish(
            nameof(ValuesDelivered),
            (handler, keys, values),
            static (observer, v) => observer.ValuesDelivered(v.handler, v.keys, v.values));

    public static void Tick()
    {
        Publish(nameof(Tick), 0, static (observer, _) => observer.Tick());

        // A second round, so an observer can see what every observer queued in the first.
        Publish(nameof(IInteropObserver.BeforeSend), 0, static (observer, _) => observer.BeforeSend());
    }

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
