using System.Runtime.CompilerServices;
using FM.UI;
using SensibleShortlists.Core.Triage;
using SI.Bindable;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game;

/// <summary>
/// Reads and builds the game's object references. A reference is a list of (key, value) pairs,
/// and each key is a four-character code. The codes below were read from a live trace; the game
/// exposes no names for them.
/// </summary>
internal static class GameReferences
{
    private static readonly uint KindKey = Code("110 ");
    private static readonly uint GameKey = Code("Guid");
    private static readonly uint RecordKey = Code("dref");
    private static readonly uint MessageNumberKey = Code("unid");
    private static readonly uint MessageManagerKey = Code("huma");

    // What this pair says is not known. It is 0 on every message the inbox lists and on every
    // message the game has been seen to mark read.
    private static readonly uint MessagePeayKey = Code("peay");

    // The kind every reference to a database record carries, person and team alike.
    private const int DatabaseRecordKind = 28;

    // The kind a reference to an inbox message carries.
    private const int NewsItemKind = 70;


    /// <summary>A binding key is one 64-bit number in a struct; reading it directly avoids a native call.</summary>
    public static ulong KeyNumber(ref Bindings.Key key) => Unsafe.As<Bindings.Key, ulong>(ref key);

    public static Bindings.Key Key(ulong number) => Unsafe.As<ulong, Bindings.Key>(ref number);

    public static bool TryGetRecord(InteropReference reference, out uint record)
    {
        var found = reference.TryGetValue(RecordKey, out var value);
        record = unchecked((uint)value);
        return found;
    }

    /// <summary>A person in the running game, from the record a link points to.</summary>
    public static InteropReference Person(uint record, int game)
    {
        var ids = new Il2CppSystem.Collections.Generic.List<InteropReference.Pair>();
        ids.Add(new InteropReference.Pair { m_id = KindKey, m_value = DatabaseRecordKind });
        ids.Add(new InteropReference.Pair { m_id = GameKey, m_value = game });
        ids.Add(new InteropReference.Pair { m_id = RecordKey, m_value = unchecked((int)record) });
        return new PersonReference(ids);
    }

    /// <summary>
    /// The messages in a list value, with the running game and the manager they belong to. The game
    /// wraps each list item in a value of its own. Each message is copied: the plugin reads from it
    /// on a later tick, and the game is free to reuse the objects it delivered.
    /// </summary>
    public static Listing? ListingFrom(TypedValue? list)
    {
        var messages = new List<ListedMessage>();
        var game = 0;
        var manager = 0;
        foreach (var message in References(list, out _) ?? Array.Empty<InteropReference>())
        {
            if (IsListedMessage(message, out var number, out var itsGame, out var itsManager))
            {
                game = itsGame;
                manager = itsManager;
                messages.Add(new ListedMessage(number, new NewsItemReference(CopyOf(message.m_id))));
            }
        }

        return messages.Count == 0 ? null : new Listing(game, unchecked((uint)manager), messages);
    }

    /// <summary>
    /// The number of the inbox message a value holds. Null when the value holds anything else,
    /// which includes a message the triage's listing would not take.
    /// </summary>
    public static long? MessageNumber(TypedValue? value) =>
        value?.Get()?.TryCast<InteropReference>() is { } reference && IsListedMessage(reference, out var number, out _, out _)
            ? number
            : null;

    /// <summary>
    /// The items of a list value, each still wrapped as the game delivered it. Null when the value
    /// is not a list, or holds an item that is not a value.
    /// </summary>
    public static IReadOnlyList<TypedValue>? Items(TypedValue? value)
    {
        var boxed = value?.Get();
        var items = boxed?.TryCast<Il2CppSystem.Collections.IList>();
        var collection = boxed?.TryCast<Il2CppSystem.Collections.ICollection>();
        if (items is null || collection is null)
        {
            return null;
        }

        var wrapped = new List<TypedValue>(collection.Count);
        for (var i = 0; i < collection.Count; i++)
        {
            if (items[i]?.TryCast<TypedValue>() is not { } item)
            {
                return null;
            }

            wrapped.Add(item);
        }

        return wrapped;
    }

    // A message as the inbox lists it says which message, in which game, for which manager.
    private static bool IsListedMessage(InteropReference reference, out int number, out int game, out int manager)
    {
        game = 0;
        manager = 0;
        return reference.TryGetValue(MessageNumberKey, out number)
            && reference.TryGetValue(GameKey, out game)
            && reference.TryGetValue(MessageManagerKey, out manager);
    }

    /// <summary>
    /// A listed message in the form the game sends its "mark as read" event to: its kind, the
    /// running game, the manager, and the message's number, and nothing else the listing may have
    /// added. Null when the listed message does not carry all three.
    /// </summary>
    public static InteropReference? MessageToMark(InteropReference listed)
    {
        if (!listed.TryGetValue(GameKey, out var game)
            || !listed.TryGetValue(MessageManagerKey, out var manager)
            || !listed.TryGetValue(MessageNumberKey, out var number))
        {
            return null;
        }

        // In key order, as the game's own references are.
        var ids = new Il2CppSystem.Collections.Generic.List<InteropReference.Pair>();
        ids.Add(new InteropReference.Pair { m_id = KindKey, m_value = NewsItemKind });
        ids.Add(new InteropReference.Pair { m_id = GameKey, m_value = game });
        ids.Add(new InteropReference.Pair { m_id = MessageManagerKey, m_value = manager });
        ids.Add(new InteropReference.Pair { m_id = MessagePeayKey, m_value = 0 });
        ids.Add(new InteropReference.Pair { m_id = MessageNumberKey, m_value = number });
        return new NewsItemReference(ids);
    }

    /// <summary>
    /// The database records a value stands for: one for a game object, one each for a list of them.
    /// Null for any other value, and for a list that has items but no records, which is a list of
    /// something the plugin does not understand.
    /// </summary>
    public static IReadOnlyList<uint>? Records(TypedValue value)
    {
        if (References(value, out var itemCount) is not { } references)
        {
            return null;
        }

        var records = new List<uint>(references.Count);
        foreach (var reference in references)
        {
            if (TryGetRecord(reference, out var record))
            {
                records.Add(record);
            }
        }

        return itemCount > 0 && records.Count == 0 ? null : records;
    }

    // The game objects a value holds: itself, or those among the items of a list. Null when it is
    // neither. The item count tells a list of other things apart from an empty list.
    private static IReadOnlyList<InteropReference>? References(TypedValue? value, out int itemCount)
    {
        itemCount = 1;
        var boxed = value?.Get();
        if (boxed?.TryCast<InteropReference>() is { } single)
        {
            return new[] { single };
        }

        var items = boxed?.TryCast<Il2CppSystem.Collections.IList>();
        var collection = boxed?.TryCast<Il2CppSystem.Collections.ICollection>();
        if (items is null || collection is null)
        {
            return null;
        }

        itemCount = collection.Count;
        var references = new List<InteropReference>(collection.Count);
        for (var i = 0; i < collection.Count; i++)
        {
            if (items[i]?.TryCast<TypedValue>()?.Get()?.TryCast<InteropReference>() is { } reference)
            {
                references.Add(reference);
            }
        }

        return references;
    }

    private static Il2CppSystem.Collections.Generic.List<InteropReference.Pair> CopyOf(
        Il2CppSystem.Collections.Generic.List<InteropReference.Pair> ids)
    {
        var copy = new Il2CppSystem.Collections.Generic.List<InteropReference.Pair>();
        for (var i = 0; i < ids.Count; i++)
        {
            copy.Add(ids[i]);
        }

        return copy;
    }

    private static uint Code(string fourCharacters) =>
        ((uint)fourCharacters[0] << 24)
        | ((uint)fourCharacters[1] << 16)
        | ((uint)fourCharacters[2] << 8)
        | fourCharacters[3];
}
