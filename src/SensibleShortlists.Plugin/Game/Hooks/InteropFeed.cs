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

    public static void EventSent(uint number, InteropReference? target, TypedValue? data)
    {
        foreach (var observer in s_observers)
        {
            try
            {
                observer.EventSent(number, target, data);
            }
            catch (Exception exception)
            {
                ReportFirstFailure($"{observer.GetType().Name}.{nameof(EventSent)}", exception);
            }
        }
    }

    public static void ChannelOpened(InteropReference? reference, PropertyID property, ulong key)
    {
        foreach (var observer in s_observers)
        {
            try
            {
                observer.ChannelOpened(reference, property, key);
            }
            catch (Exception exception)
            {
                ReportFirstFailure($"{observer.GetType().Name}.{nameof(ChannelOpened)}", exception);
            }
        }
    }

    public static void ValuesDelivered(ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values)
    {
        foreach (var observer in s_observers)
        {
            try
            {
                observer.ValuesDelivered(keys, values);
            }
            catch (Exception exception)
            {
                ReportFirstFailure($"{observer.GetType().Name}.{nameof(ValuesDelivered)}", exception);
            }
        }
    }

    public static void Tick()
    {
        foreach (var observer in s_observers)
        {
            try
            {
                observer.Tick();
            }
            catch (Exception exception)
            {
                ReportFirstFailure($"{observer.GetType().Name}.{nameof(Tick)}", exception);
            }
        }
    }

    /// <summary>For a hook that fails before it has anything to hand to the feed.</summary>
    public static void ReportFirstFailure(string where, Exception exception)
    {
        if (Failures.ShouldReport(where))
        {
            s_log?.LogWarning($"{where} failed and will stay silent from now on: {exception}");
        }
    }
}
