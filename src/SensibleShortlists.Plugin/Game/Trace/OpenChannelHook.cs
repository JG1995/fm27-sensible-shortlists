using System.Reflection;
using FM.GamePlugin;
using HarmonyLib;
using SI.Bindable;
using SI.Bindable.Reference.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>Observes the UI asking the simulation core for a property of a game object.</summary>
internal static class OpenChannelHook
{
    public const string Name = "GameInteropSubsystem.OpenChannel";

    public static (MethodBase? Target, MethodInfo Prefix) Resolve() => (
        AccessTools.Method(
            typeof(GameInteropSubsystem),
            nameof(GameInteropSubsystem.OpenChannel),
            new[] { typeof(InteropReference), typeof(PropertyID), typeof(Bindings.Key) }),
        AccessTools.Method(typeof(OpenChannelHook), nameof(Prefix)));

    private static void Prefix(InteropReference __0, PropertyID __1, Bindings.Key __2)
    {
        try
        {
            TraceHookContext.Session?.ChannelOpened(
                GameDescriber.KeyNumber(ref __2), GameDescriber.Reference(__0), GameDescriber.Property(__1));
        }
        catch (Exception exception)
        {
            TraceHookContext.ReportFirstFailure(Name, exception);
        }
    }
}
