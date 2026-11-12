using System.Reflection;
using FM.GamePlugin;
using HarmonyLib;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>
/// Runs once a frame, before the game's interop layer does its own work. Calls the plugin makes
/// into the game are made from here, never from inside another hook, so the plugin does not change
/// the interop layer's state while the game is in the middle of using it.
/// </summary>
internal static class TickHook
{
    public const string Name = "GameInteropSubsystem.Tick";

    public static (MethodBase? Target, MethodInfo Prefix) Resolve() => (
        AccessTools.Method(typeof(GameInteropSubsystem), nameof(GameInteropSubsystem.Tick), Type.EmptyTypes),
        AccessTools.Method(typeof(TickHook), nameof(Prefix)));

    private static void Prefix() => InteropFeed.Tick();
}
