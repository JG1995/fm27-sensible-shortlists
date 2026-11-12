using System.Reflection;
using HarmonyLib;
using SI.Bindable;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>
/// Observes values arriving in the UI's binding tree. They come in through either of two methods,
/// depending on how the value was requested, so both are hooked.
/// </summary>
internal static class SetValueHooks
{
    public const string SetInternalName = "Bindings.SetInternal";
    public const string SetPrefetchedName = "Bindings.SetPrefetched";

    public static (MethodBase? Target, MethodInfo Prefix) ResolveSetInternal() =>
        (Target(nameof(Bindings.SetInternal)), AccessTools.Method(typeof(SetValueHooks), nameof(SetInternalPrefix)));

    public static (MethodBase? Target, MethodInfo Prefix) ResolveSetPrefetched() =>
        (Target(nameof(Bindings.SetPrefetched)), AccessTools.Method(typeof(SetValueHooks), nameof(SetPrefetchedPrefix)));

    private static MethodBase? Target(string method) =>
        AccessTools.Method(
            typeof(Bindings),
            method,
            new[] { typeof(Bindings.Key).MakeByRefType(), typeof(TypedValue), typeof(Bindings.SetFlags) });

    private static void SetInternalPrefix(ref Bindings.Key __0, TypedValue __1) =>
        Trace(SetInternalName, ref __0, __1);

    private static void SetPrefetchedPrefix(ref Bindings.Key __0, TypedValue __1) =>
        Trace(SetPrefetchedName, ref __0, __1);

    // This runs for every bound value in the game. The static describer keeps it allocation-free
    // for the values that are not traced, which is nearly all of them.
    private static void Trace(string hook, ref Bindings.Key key, TypedValue value)
    {
        try
        {
            TraceHookContext.Session?.ValueChanged(
                GameDescriber.KeyNumber(ref key), value, static traced => GameDescriber.Value(traced));
        }
        catch (Exception exception)
        {
            TraceHookContext.ReportFirstFailure(hook, exception);
        }
    }
}
