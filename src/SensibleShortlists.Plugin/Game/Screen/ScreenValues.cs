using System.Globalization;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Screen;

/// <summary>
/// Reads the inbox values the game delivers and builds the ones the screen is given instead. It
/// keeps the game's own value for each channel, to copy from when the screen has to be given
/// something else later.
/// </summary>
/// <remarks>
/// Every value handed out is a copy made for that occasion, wrapper and items alike: a value the
/// screen holds is the game's to keep or reuse. The values remembered here are only ever copied
/// from. One of them is also held by the screen when the game's own value was left in place.
/// </remarks>
internal sealed class ScreenValues
{
    private readonly Dictionary<ulong, (TypedValue List, IReadOnlyList<TypedValue> Items)> _lists = new();
    private readonly Dictionary<ulong, (long Message, TypedValue Value)> _named = new();

    // The inbox item last given on a channel in the named message's place. The inbox list moves on
    // with every search, and the same message may have to be given again after it has left the list.
    private readonly Dictionary<ulong, (long Message, TypedValue Value)> _standIns = new();

    // By message number, from the inbox list the game delivered last.
    private Dictionary<long, TypedValue> _inbox = new();

    /// <summary>
    /// Remembers a delivered list and says which message each item is: its number, or null for an
    /// item that is not an inbox message. Null when the value is not a list.
    /// </summary>
    /// <param name="isInbox">The list is the inbox itself, which the other inbox values point into.</param>
    public IReadOnlyList<long?>? RememberList(ulong key, TypedValue? value, bool isInbox)
    {
        if (value is null || GameReferences.Items(value) is not { } items)
        {
            _lists.Remove(key);
            if (isInbox)
            {
                _inbox = new Dictionary<long, TypedValue>();
            }

            return null;
        }

        var messages = items.Select(GameReferences.MessageNumber).ToArray();
        _lists[key] = (value, items);
        if (isInbox)
        {
            _inbox = new Dictionary<long, TypedValue>();
            for (var i = 0; i < items.Count; i++)
            {
                if (messages[i] is { } message)
                {
                    _inbox[message] = items[i];
                }
            }
        }

        return messages;
    }

    /// <summary>Remembers a delivered message and gives its number. Null when the value is not an inbox message.</summary>
    public long? RememberMessage(ulong key, TypedValue? value)
    {
        if (value is null || GameReferences.MessageNumber(value) is not { } message)
        {
            _named.Remove(key);
            _standIns.Remove(key);
            return null;
        }

        _named[key] = (message, value);
        return message;
    }

    /// <summary>The position a delivered value holds. Null when it is not a whole number.</summary>
    public static int? PositionIn(TypedValue? value) =>
        int.TryParse(value?.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var position)
            ? position
            : null;

    public void Forget(ulong key)
    {
        _lists.Remove(key);
        _named.Remove(key);
        _standIns.Remove(key);
    }

    /// <summary>The list last delivered on the channel, reduced to the items at the given places.</summary>
    public TypedValue List(ulong key, IReadOnlyList<int> places)
    {
        var (list, items) = _lists[key];
        var shown = new Il2CppSystem.Collections.Generic.List<TypedValue>();
        foreach (var place in places)
        {
            shown.Add(TypedValue.CreateCopyFrom(items[place]));
        }

        // The copy is of the game's own list value, so it is the kind of value the screen expects;
        // only what it holds is replaced.
        var value = TypedValue.CreateCopyFrom(list);
        value.SetValue((Il2CppSystem.Object)shown);
        return value;
    }

    /// <summary>
    /// A value naming a message: the one the game named on the channel, the one given there in its
    /// place before, or one from the inbox list.
    /// </summary>
    public TypedValue Message(ulong key, long message)
    {
        _named.TryGetValue(key, out var named);
        if (named.Value is not null && named.Message == message)
        {
            return TypedValue.CreateCopyFrom(named.Value);
        }

        if (!_standIns.TryGetValue(key, out var standIn) || standIn.Message != message)
        {
            standIn = _inbox.TryGetValue(message, out var listed)
                ? (message, listed)
                : throw new InvalidOperationException($"Message {message} is in no inbox list the game has delivered.");
            _standIns[key] = standIn;
        }

        // Another career's messages carry the same numbers. A message from one must never be given
        // where the game named a message of the other.
        if (named.Value is not null && !GameReferences.SameGame(named.Value, standIn.Value))
        {
            throw new InvalidOperationException($"Message {message} was listed for another career.");
        }

        return TypedValue.CreateCopyFrom(standIn.Value);
    }

    // The game makes its own delivered numbers with this same call.
    public static TypedValue Position(int position) => TypedValue.Create(position);
}
