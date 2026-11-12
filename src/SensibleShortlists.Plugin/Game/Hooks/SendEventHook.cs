using System.Reflection;
using FM.GamePlugin;
using HarmonyLib;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>Sees every event and action the UI sends to the simulation core.</summary>
internal static class SendEventHook
{
    public const string Name = "GameInteropSubsystem.SendEvent";

    public static (MethodBase? Target, MethodInfo Prefix) Resolve() => (
        AccessTools.Method(
            typeof(GameInteropSubsystem),
            nameof(GameInteropSubsystem.SendEvent),
            new[] { typeof(uint), typeof(InteropReference), typeof(TypedValue) }),
        AccessTools.Method(typeof(SendEventHook), nameof(Prefix)));

    private static void Prefix(uint __0, InteropReference __1, TypedValue __2) =>
        InteropFeed.EventSent(__0, __1, __2);
}
