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
    private static readonly uint SearchManagerKey = Code("Hhum");
    private static readonly uint SearchTeamKey = Code("Ttea");
    private static readonly uint SearchTypeKey = Code("type");

    // The kind every reference to a database record carries, person and team alike.
    private const int DatabaseRecordKind = 28;

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

    /// <summary>
    /// A person in the running game, from the record a link points to, optionally carrying another
    /// record under a key of its own, the way some of the game's references carry their context.
    /// </summary>
    public static InteropReference Person(uint record, int game, string? contextCode, uint context)
    {
        var pairs = new List<(uint Key, int Value)>
        {
            (KindKey, DatabaseRecordKind), (GameKey, game), (RecordKey, unchecked((int)record)),
        };
        if (contextCode is not null)
        {
            pairs.Add((Code(contextCode), unchecked((int)context)));
        }

        // The game's own references list their pairs in key order.
        var ids = new Il2CppSystem.Collections.Generic.List<InteropReference.Pair>();
        foreach (var (key, value) in pairs.OrderBy(pair => pair.Key))
        {
            ids.Add(new InteropReference.Pair { m_id = key, m_value = value });
        }

        return new PersonReference(ids);
    }

    /// <summary>A key or identifier as the four characters it is made of.</summary>
    public static string CodeName(uint code) =>
        new(new[] { (char)(code >> 24), (char)((code >> 16) & 0xFF), (char)((code >> 8) & 0xFF), (char)(code & 0xFF) });

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
