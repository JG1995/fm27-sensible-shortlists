using System.Collections;
using System.Reflection;
using FM.UI;
using HarmonyLib;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>
/// Observes the simulation core answering the channels the UI opened. The answer arrives as a batch
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
        try
        {
            ulong[] keys = __0.ToArray();
            TraceHookContext.Session?.ValuesDelivered(
                keys, new GameList(__2), static value => GameDescriber.Value(value));
        }
        catch (Exception exception)
        {
            TraceHookContext.ReportFirstFailure(Name, exception);
        }
    }

    /// <summary>Lets the core read the game's list in place instead of copying every batch.</summary>
    private sealed class GameList : IReadOnlyList<TypedValue>
    {
        private readonly Il2CppSystem.Collections.Generic.List<TypedValue> _list;

        public GameList(Il2CppSystem.Collections.Generic.List<TypedValue> list) => _list = list;

        public int Count => _list.Count;

        public TypedValue this[int index] => _list[index];

        public IEnumerator<TypedValue> GetEnumerator()
        {
            for (var i = 0; i < _list.Count; i++)
            {
                yield return _list[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
