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
        Hook("AddNode", nameof(OnAddNode), static () => new[] { typeof(Bindings.Key), typeof(Bindings.Key), typeof(PropertyID) }),
        Hook("RemoveNode", nameof(OnRemoveNode), static () => new[] { typeof(Bindings.Key) }),
        Hook("SetTarget", nameof(OnSetTarget), static () => new[] { typeof(Bindings.Key), typeof(Bindings.DataKey) }),
        Hook("AddData", nameof(OnAddData), static () => new[] { typeof(Bindings.DataKey) }),
        Hook("SetData", nameof(OnSetData), static () => new[] { typeof(Bindings.DataKey), typeof(TypedValue) }),
        Hook("ReleaseData", nameof(OnReleaseData), static () => new[] { typeof(Bindings.DataKey) }),
    };

    // The game's types are named only inside the functions the installer calls under its guard,
    // so a type missing from a new game build costs the hooks that use it and nothing else.
    private static (string, Func<(MethodBase?, MethodInfo)>) Hook(string method, string prefix, Func<Type[]> parameters) => (
        $"GameInteropSubsystem.{method}",
        () => (
            AccessTools.Method(typeof(GameInteropSubsystem), method, parameters()),
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
