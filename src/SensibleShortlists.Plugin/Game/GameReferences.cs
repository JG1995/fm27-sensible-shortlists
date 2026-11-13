using System.Runtime.CompilerServices;
using FM.UI;
using SensibleShortlists.Core.Triage;
using SI.Bindable;
using SI.Bindable.Reference.Core;
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
    private static readonly uint SearchManagerKey = Code("Hhum");
    private static readonly uint SearchTeamKey = Code("Ttea");
    private static readonly uint SearchTypeKey = Code("type");

    // The property of a status flag's record that holds the flag's number.
    private const string StatusFlagProperty = "StatusFlag";

    // The kind every reference to a database record carries, person and team alike.
    private const int DatabaseRecordKind = 28;

    // The kind a reference to an inbox message carries.
    private const int NewsItemKind = 70;

    // The kind a person-search reference carries, and the search type of the Shortlist screen.
    private const int PersonSearchKind = 73;
    private const int ShortlistSearchType = 0;

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

    /// <summary>The object behind the Shortlist screen of one manager at one team.</summary>
    public static InteropReference Shortlist(uint manager, uint team, int game)
    {
        var ids = new Il2CppSystem.Collections.Generic.List<InteropReference.Pair>();
        ids.Add(new InteropReference.Pair { m_id = KindKey, m_value = PersonSearchKind });
        ids.Add(new InteropReference.Pair { m_id = GameKey, m_value = game });
        ids.Add(new InteropReference.Pair { m_id = SearchManagerKey, m_value = unchecked((int)manager) });
        ids.Add(new InteropReference.Pair { m_id = SearchTeamKey, m_value = unchecked((int)team) });
        ids.Add(new InteropReference.Pair { m_id = SearchTypeKey, m_value = ShortlistSearchType });
        return new PersonSearchReference(ids);
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
            if (message.TryGetValue(MessageNumberKey, out var number)
                && message.TryGetValue(GameKey, out game)
                && message.TryGetValue(MessageManagerKey, out manager))
            {
                messages.Add(new ListedMessage(number, new NewsItemReference(CopyOf(message.m_id))));
            }
        }

        return messages.Count == 0 ? null : new Listing(game, unchecked((uint)manager), messages);
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

    /// <summary>
    /// The status flags in a list value, as the game names them. The game hands a player's status
    /// over as a list of records, one per flag, and its own screens read the flag's number from the
    /// record's <c>StatusFlag</c> property. Null when the value is not such a list.
    /// </summary>
    public static IReadOnlyList<string>? StatusFlags(TypedValue value)
    {
        var boxed = value.Get();
        if (boxed?.TryCast<Il2CppSystem.Collections.IList>() is not { } items
            || boxed.TryCast<Il2CppSystem.Collections.ICollection>() is not { } collection)
        {
            return null;
        }

        var flags = new List<string>();
        for (var i = 0; i < collection.Count; i++)
        {
            if (items[i]?.TryCast<TypedValue>()?.Get()?.TryCast<Record>() is not { } record || StatusFlag(record) is not { } flag)
            {
                return null;
            }

            flags.Add(flag);
        }

        return flags;
    }

    // Names come from the game's own enum, so they follow the game when it updates. A number the
    // enum does not know is given as the number.
    private static string? StatusFlag(Record record)
    {
        var entries = record.GetEnumerator();
        while (entries.MoveNext())
        {
            var entry = entries.Current;
            if (InboxWatch.NameOf(GameDescriber.Property(new PropertyID { id = entry.Key })) != StatusFlagProperty)
            {
                continue;
            }

            var text = entry.Value?.AsString();
            return int.TryParse(text, out var number) && Enum.IsDefined(typeof(PlayerStatusFlag), number)
                ? ((PlayerStatusFlag)number).ToString()
                : text;
        }

        return null;
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
