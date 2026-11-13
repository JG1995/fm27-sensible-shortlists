using System.Reflection;
using FM.GamePlugin;
using HarmonyLib;
using SI.Bindable;
using SI.Bindable.Reference.Core;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>
/// Sees the UI mirroring its binding tree into the simulation: nodes added and removed, nodes
/// pointed at data, and the data itself. This is how the simulation learns what a property hangs
/// off, including the context a property is asked in.
/// </summary>
/// <remarks>Every argument here is a struct passed by value or an object, which BepInEx converts correctly.</remarks>
internal static class NodeGraphHooks
{
    public static readonly (string Name, Func<(MethodBase? Target, MethodInfo Prefix)> Resolve)[] All =
    {
        Hook(
            nameof(GameInteropSubsystem.AddNode),
            nameof(OnAddNode),
            typeof(Bindings.Key),
            typeof(Bindings.Key),
            typeof(PropertyID)),
        Hook(nameof(GameInteropSubsystem.RemoveNode), nameof(OnRemoveNode), typeof(Bindings.Key)),
        Hook(nameof(GameInteropSubsystem.SetTarget), nameof(OnSetTarget), typeof(Bindings.Key), typeof(Bindings.DataKey)),
        Hook(nameof(GameInteropSubsystem.AddData), nameof(OnAddData), typeof(Bindings.DataKey)),
        Hook(nameof(GameInteropSubsystem.SetData), nameof(OnSetData), typeof(Bindings.DataKey), typeof(TypedValue)),
        Hook(nameof(GameInteropSubsystem.ReleaseData), nameof(OnReleaseData), typeof(Bindings.DataKey)),
    };

    private static (string, Func<(MethodBase?, MethodInfo)>) Hook(string method, string prefix, params Type[] parameters) => (
        $"{nameof(GameInteropSubsystem)}.{method}",
        () => (
            AccessTools.Method(typeof(GameInteropSubsystem), method, parameters),
            AccessTools.Method(typeof(NodeGraphHooks), prefix)));

    private static void OnAddNode(Bindings.Key __0, Bindings.Key __1, PropertyID __2) =>
        InteropFeed.NodeAdded(GameReferences.KeyNumber(ref __0), GameReferences.KeyNumber(ref __1), __2);

    private static void OnRemoveNode(Bindings.Key __0) => InteropFeed.NodeRemoved(GameReferences.KeyNumber(ref __0));

    private static void OnSetTarget(Bindings.Key __0, Bindings.DataKey __1) =>
        InteropFeed.TargetSet(GameReferences.KeyNumber(ref __0), GameReferences.DataKeyNumber(ref __1));

    private static void OnAddData(Bindings.DataKey __0) => InteropFeed.DataAdded(GameReferences.DataKeyNumber(ref __0));

    private static void OnSetData(Bindings.DataKey __0, TypedValue __1) =>
        InteropFeed.DataSet(GameReferences.DataKeyNumber(ref __0), __1);

    private static void OnReleaseData(Bindings.DataKey __0) =>
        InteropFeed.DataReleased(GameReferences.DataKeyNumber(ref __0));
}
