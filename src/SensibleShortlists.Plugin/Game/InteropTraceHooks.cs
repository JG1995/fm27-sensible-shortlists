using System.Reflection;
using BepInEx.Logging;
using FM.GamePlugin;
using SensibleShortlists.Core.Diagnostics;
using SI.Bindable;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game;

/// <summary>
/// Observes the game's interop layer for the trace. The hooks only read: each turns its arguments
/// into plain values, hands them to the session, and lets the game call proceed unchanged.
/// </summary>
internal static class InteropTraceHooks
{
    private static readonly FirstFailureGate Failures = new();

    private static InteropTraceSession? s_session;
    private static ManualLogSource? s_log;

    public static (int Installed, int Total) Install(HookInstaller hooks, InteropTraceSession session, ManualLogSource log)
    {
        s_session = session;
        s_log = log;

        // Values reach the UI's binding tree through either of these, depending on how they were requested.
        var setValueParameters = new[]
        {
            typeof(Bindings.Key).MakeByRefType(), typeof(TypedValue), typeof(Bindings.SetFlags),
        };

        var installed = new[]
        {
            hooks.TryInstallPrefix(
                typeof(GameInteropSubsystem),
                nameof(GameInteropSubsystem.SendEvent),
                new[] { typeof(uint), typeof(InteropReference), typeof(TypedValue) },
                Hook(nameof(OnSendEvent))),
            hooks.TryInstallPrefix(
                typeof(GameInteropSubsystem),
                nameof(GameInteropSubsystem.OpenChannel),
                new[] { typeof(InteropReference), typeof(PropertyID), typeof(Bindings.Key) },
                Hook(nameof(OnOpenChannel))),
            hooks.TryInstallPrefix(
                typeof(Bindings), nameof(Bindings.SetInternal), setValueParameters, Hook(nameof(OnSetValue))),
            hooks.TryInstallPrefix(
                typeof(Bindings), nameof(Bindings.SetPrefetched), setValueParameters, Hook(nameof(OnSetValue))),
        };

        return (installed.Count(wasInstalled => wasInstalled), installed.Length);
    }

    private static MethodInfo Hook(string name) =>
        typeof(InteropTraceHooks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void OnSendEvent(uint __0, InteropReference __1, TypedValue __2)
    {
        try
        {
            s_session!.EventSent(
                GameDescriber.EventName(__0), __0, GameDescriber.Reference(__1), GameDescriber.Value(__2));
        }
        catch (Exception exception)
        {
            ReportFirstFailure(nameof(OnSendEvent), exception);
        }
    }

    private static void OnOpenChannel(InteropReference __0, PropertyID __1, Bindings.Key __2)
    {
        try
        {
            s_session!.ChannelOpened(
                GameDescriber.KeyNumber(ref __2), GameDescriber.Reference(__0), GameDescriber.Property(__1));
        }
        catch (Exception exception)
        {
            ReportFirstFailure(nameof(OnOpenChannel), exception);
        }
    }

    private static void OnSetValue(ref Bindings.Key __0, TypedValue __1)
    {
        try
        {
            s_session!.ValueChanged(GameDescriber.KeyNumber(ref __0), __1, static value => GameDescriber.Value(value));
        }
        catch (Exception exception)
        {
            ReportFirstFailure(nameof(OnSetValue), exception);
        }
    }

    // Nothing may escape into the game: an exception thrown into IL2CPP code can take the process down.
    private static void ReportFirstFailure(string hook, Exception exception)
    {
        if (Failures.ShouldReport(hook))
        {
            s_log?.LogWarning($"Trace hook {hook} failed and will stay silent from now on: {exception}");
        }
    }
}
