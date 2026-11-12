using System.Reflection;
using FM.UI;
using HarmonyLib;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>
/// Sees the simulation core answering the channels that were opened. The answer arrives as a batch
/// of channel keys and values, and the keys are the ones <see cref="OpenChannelHook"/> saw.
/// </summary>
/// <remarks>
/// Hooked here, not where the values enter the binding tree (<c>Bindings.SetInternal</c>): that
/// method takes its key by reference, and BepInEx's Harmony support passes a by-reference struct as
/// if it were a pointer to a pointer.
/// </remarks>
internal static class ChannelDataHook
{
    public const string Name = "InteropDataHandler.OnChannelDataChanged";

    public static (MethodBase? Target, MethodInfo Prefix) Resolve() => (
        AccessTools.Method(typeof(InteropDataHandler), nameof(InteropDataHandler.OnChannelDataChanged)),
        AccessTools.Method(typeof(ChannelDataHook), nameof(Prefix)));

    private static void Prefix(
        Il2CppSystem.ReadOnlySpan<ulong> __0,
        Il2CppSystem.Collections.Generic.List<TypedValue> __2)
    {
        ulong[] keys;
        try
        {
            keys = __0.ToArray();
        }
        catch (Exception exception)
        {
            InteropFeed.ReportFirstFailure(Name, exception);
            return;
        }

        InteropFeed.ValuesDelivered(keys, __2);
    }
}
