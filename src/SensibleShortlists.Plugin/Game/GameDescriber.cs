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

    /// <summary>The game prints a property as <c>Name:number</c>, which is all the trace needs.</summary>
    public static string? Property(PropertyID property) =>
        TraceText.Describe(property, static id => id.ToString());

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

        // References and lists print as their type name only, and they are what the trace is for:
        // which message, which player, which messages are in the inbox. Failing to expand one must
        // not lose the line.
        try
        {
            var boxed = value.Get();
            if (boxed?.TryCast<InteropReference>() is { } reference)
            {
                return DescribeReference(reference);
            }

            var items = boxed?.TryCast<Il2CppSystem.Collections.IList>();
            var collection = boxed?.TryCast<Il2CppSystem.Collections.ICollection>();
            return items is null || collection is null ? text : $"{text} {DescribeItems(items, collection.Count)}";
        }
        catch (Exception exception)
        {
            return $"{text} <error: {exception.GetType().Name}>";
        }
    }

    // The interop wrapper for IList does not carry ICollection's members, so the count comes separately.
    private static string DescribeItems(Il2CppSystem.Collections.IList items, int count)
    {
        var text = new StringBuilder().Append(count).Append(" items {");
        for (var i = 0; i < count && i < MaxListItems; i++)
        {
            text.Append(i == 0 ? string.Empty : "; ").Append(DescribeItem(items[i]));
        }

        return text.Append(count > MaxListItems ? "; …}" : "}").ToString();
    }

    // The game's lists hold values wrapped in TypedValue, one level down from the list itself.
    private static string DescribeItem(Il2CppSystem.Object? item)
    {
        if (item?.TryCast<TypedValue>() is { } value)
        {
            return DescribeValue(value);
        }

        return item?.TryCast<InteropReference>() is { } reference
            ? DescribeReference(reference)
            : item?.ToString() ?? "null";
    }
}
