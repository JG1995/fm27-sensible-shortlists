using System.Reflection;
using FM.GamePlugin;
using HarmonyLib;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>Observes every event and action the UI sends to the simulation core.</summary>
internal static class SendEventHook
{
    public const string Name = "GameInteropSubsystem.SendEvent";

    public static (MethodBase? Target, MethodInfo Prefix) Resolve() => (
        AccessTools.Method(
            typeof(GameInteropSubsystem),
            nameof(GameInteropSubsystem.SendEvent),
            new[] { typeof(uint), typeof(InteropReference), typeof(TypedValue) }),
        AccessTools.Method(typeof(SendEventHook), nameof(Prefix)));

    private static void Prefix(uint __0, InteropReference __1, TypedValue __2)
    {
        try
        {
            TraceHookContext.Session?.EventSent(
                GameDescriber.EventName(__0), __0, GameDescriber.Reference(__1), GameDescriber.Value(__2));
        }
        catch (Exception exception)
        {
            TraceHookContext.ReportFirstFailure(Name, exception);
        }
    }
}
