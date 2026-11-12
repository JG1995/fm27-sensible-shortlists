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

    // The kind every reference to a database record carries, person and team alike.
    private const int DatabaseRecordKind = 28;

    /// <summary>A binding key is one 64-bit number in a struct; reading it directly avoids a native call.</summary>
    public static ulong KeyNumber(ref Bindings.Key key) => Unsafe.As<Bindings.Key, ulong>(ref key);

    public static Bindings.Key Key(ulong number) => Unsafe.As<ulong, Bindings.Key>(ref number);

    public static bool TryGetRecord(InteropReference reference, out uint record)
    {
        var found = reference.TryGetValue(RecordKey, out var value);
        record = unchecked((uint)value);
        return found;
    }

    /// <summary>A reference to a person in the running game, from the record a link points to.</summary>
    public static InteropReference Person(uint record, int game)
    {
        var ids = new Il2CppSystem.Collections.Generic.List<InteropReference.Pair>();
        ids.Add(new InteropReference.Pair { m_id = KindKey, m_value = DatabaseRecordKind });
        ids.Add(new InteropReference.Pair { m_id = GameKey, m_value = game });
        ids.Add(new InteropReference.Pair { m_id = RecordKey, m_value = unchecked((int)record) });
        return new PersonReference(ids);
    }

    /// <summary>
    /// The messages in a list value, with the running game's identifier. The game wraps each list
    /// item in a value of its own. Each message is copied: the plugin reads from it on a later tick,
    /// and the game is free to reuse the objects it delivered.
    /// </summary>
    public static (int Game, ListedMessage[] Messages) Messages(TypedValue? list)
    {
        var boxed = list?.Get();
        var items = boxed?.TryCast<Il2CppSystem.Collections.IList>();
        var count = boxed?.TryCast<Il2CppSystem.Collections.ICollection>()?.Count ?? 0;
        var messages = new List<ListedMessage>(count);
        var game = 0;
        for (var i = 0; items is not null && i < count; i++)
        {
            var message = items[i]?.TryCast<TypedValue>()?.Get()?.TryCast<InteropReference>();
            if (message is not null
                && message.TryGetValue(MessageNumberKey, out var number)
                && message.TryGetValue(GameKey, out game))
            {
                messages.Add(new ListedMessage(number, new NewsItemReference(CopyOf(message.m_id))));
            }
        }

        return (game, messages.ToArray());
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
