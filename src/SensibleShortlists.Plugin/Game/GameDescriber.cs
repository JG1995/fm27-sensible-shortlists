using System.Runtime.CompilerServices;
using System.Text;
using FM.GamePlugin;
using SensibleShortlists.Core.Diagnostics;
using SI.Bindable;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game;

/// <summary>
/// Turns game objects into text for the trace. Every read of a game object goes through
/// <see cref="TraceText.Describe{TState}"/>, because these objects are undocumented and any of them
/// may throw on a build the plugin has not seen.
/// </summary>
internal static class GameDescriber
{
    private const int MaxListItems = 30;

    /// <summary>Names come from the game's own enums, so they follow the game when it updates.</summary>
    public static string? EventName(uint number) =>
        TraceText.Describe(number, static n =>
        {
            if (Enum.IsDefined(typeof(InteropEventIDs), n))
            {
                return ((InteropEventIDs)n).ToString();
            }

            return Enum.IsDefined(typeof(ActionID), n) ? ((ActionID)n).ToString() : "?";
        });

    /// <summary>A binding key is one 64-bit number in a struct; reading it directly avoids a native call.</summary>
    public static ulong KeyNumber(ref Bindings.Key key) => Unsafe.As<Bindings.Key, ulong>(ref key);

    public static string? Property(PropertyID property) =>
        TraceText.Describe(property, static id => $"{id.ToString()}#{id.id}");

    public static string? Reference(InteropReference? reference) =>
        TraceText.Describe(reference, static r => r is null ? null : DescribeReference(r));

    public static string? Value(TypedValue? value) =>
        TraceText.Describe(value, static v => v is null ? null : DescribeValue(v));

    private static string DescribeReference(InteropReference reference)
    {
        var text = new StringBuilder(reference.GetIl2CppType().Name).Append('[');
        var ids = reference.m_id;
        for (var i = 0; ids is not null && i < ids.Count; i++)
        {
            var pair = ids[i];
            text.Append(i == 0 ? string.Empty : ",").Append(pair.m_id).Append(':').Append(pair.m_value);
        }

        return text.Append(']').ToString();
    }

    private static string DescribeValue(TypedValue value)
    {
        var text = $"{value.DataType?.Name ?? "?"}:{value.AsString()}";

        // Lists print as their type name only, and the lists are what the trace is for: the messages
        // in the inbox and the players on a shortlist. Failing to expand one must not lose the line.
        try
        {
            var boxed = value.Get();
            var items = boxed?.TryCast<Il2CppSystem.Collections.IList>();
            var collection = boxed?.TryCast<Il2CppSystem.Collections.ICollection>();
            return items is null || collection is null ? text : $"{text} {DescribeItems(items, collection.Count)}";
        }
        catch (Exception exception)
        {
            return $"{text} <items unreadable: {exception.GetType().Name}>";
        }
    }

    // The interop wrapper for IList does not carry ICollection's members, so the count comes separately.
    private static string DescribeItems(Il2CppSystem.Collections.IList items, int count)
    {
        var text = new StringBuilder().Append(count).Append(" items {");
        for (var i = 0; i < count && i < MaxListItems; i++)
        {
            var item = items[i];
            var reference = item?.TryCast<InteropReference>();
            text.Append(i == 0 ? string.Empty : "; ")
                .Append(reference is null ? item?.ToString() ?? "null" : DescribeReference(reference));
        }

        return text.Append(count > MaxListItems ? "; …}" : "}").ToString();
    }
}
