using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Hiding;

/// <summary>What a channel the game's UI opened tells the screen about the inbox.</summary>
public enum ScreenRole
{
    None,

    /// <summary>The list Messages shows, which the global search also filters.</summary>
    InboxList,

    /// <summary>The messages that arrived since the manager last continued.</summary>
    IncomingList,

    /// <summary>The message Messages opens on.</summary>
    MessageToOpen,

    /// <summary>Where in the inbox list the newest unread message is; the list scrolls to it.</summary>
    UnreadPosition,
}

/// <summary>Something the screen must be given in place of what it has.</summary>
public abstract record ScreenUpdate(ulong Key);

/// <param name="Positions">The places, in the list the game last delivered on the channel, of the messages to show.</param>
public sealed record ListUpdate(ulong Key, IReadOnlyList<int> Positions) : ScreenUpdate(Key);

public sealed record MessageToOpenUpdate(ulong Key, long Message) : ScreenUpdate(Key);

public sealed record PositionUpdate(ulong Key, int Position) : ScreenUpdate(Key);

/// <summary>
/// Decides what the screen is given of the inbox: each list without the messages that are not
/// shown, and the two values that point into the inbox list moved to match. It remembers what the
/// game last said on each channel, so that when a verdict arrives it can say what the screen must
/// be given again.
/// </summary>
/// <remarks>
/// Each delivery method answers with the value to give in the game's place, or null to leave the
/// game's own value alone. Leaving alone is the answer whenever nothing would change.
/// </remarks>
public sealed class MessageScreen
{
    private static readonly (string Property, ScreenRole Role)[] Roles =
    {
        ("HumanPortalFilteredMessages", ScreenRole.InboxList),
        ("HumanIncomingMessages", ScreenRole.IncomingList),
        ("HumanPortalCurrentMessageToOpen", ScreenRole.MessageToOpen),
        ("HumanPortalNewestUnreadMessageIndex", ScreenRole.UnreadPosition),
    };

    private readonly MessageVisibility _visibility;
    private readonly ITraceSink _log;
    private readonly Dictionary<ulong, ScreenRole> _roles = new();
    private readonly Dictionary<ulong, ListState> _lists = new();
    private readonly Dictionary<ulong, (long Named, long Given)> _toOpen = new();
    private readonly Dictionary<ulong, (int Named, int Given)> _positions = new();
    private IReadOnlyList<long?> _inbox = Array.Empty<long?>();
    private int _versionGiven;
    private int _career;
    private bool _inboxChanged;

    public MessageScreen(MessageVisibility visibility, ITraceSink log)
    {
        _visibility = visibility;
        _log = log;
        _versionGiven = visibility.Version;
        _career = visibility.Career;
    }

    /// <summary>True once the plugin has given up changing what the screen is given.</summary>
    public bool Stopped { get; private set; }

    /// <summary>
    /// The UI opened a channel. The game prints a property as <c>Name:number</c>. This runs for
    /// every binding in the game, nearly all of them nothing to do with the inbox.
    /// </summary>
    public ScreenRole ChannelOpened(ulong key, string? printedProperty)
    {
        var role = RoleNamed(printedProperty);
        if (role == ScreenRole.None)
        {
            // The game reuses keys, so a key that carried the inbox may now carry something else.
            if (_roles.Remove(key))
            {
                Forget(key);
            }

            return ScreenRole.None;
        }

        // A channel just opened has had no delivery, whatever the key carried before.
        Forget(key);
        _roles[key] = role;
        return role;
    }

    public ScreenRole RoleOf(ulong key) => Stopped ? ScreenRole.None : _roles.GetValueOrDefault(key);

    /// <summary>
    /// The game delivered something on the channel that is not what the channel carries. What it
    /// delivered there before no longer stands, and if the channel carries the inbox list, there
    /// is no inbox list to point into until the next one arrives.
    /// </summary>
    public void NotUnderstood(ulong key)
    {
        Forget(key);
        if (RoleOf(key) == ScreenRole.InboxList)
        {
            _inbox = Array.Empty<long?>();
            _inboxChanged = true;
        }
    }

    /// <summary>
    /// Drops what the game last delivered on a channel, because the binding it fed no longer
    /// exists. Nothing is given on it until the game delivers again.
    /// </summary>
    public void Forget(ulong key)
    {
        _lists.Remove(key);
        _toOpen.Remove(key);
        _positions.Remove(key);
    }

    /// <param name="messages">The list's items in order: a message's number, or null for an item that is not a message.</param>
    /// <returns>The places of the items to show, or null to leave the game's list alone.</returns>
    public IReadOnlyList<int>? ListDelivered(ulong key, IReadOnlyList<long?> messages)
    {
        var role = RoleOf(key);
        if (role is not (ScreenRole.InboxList or ScreenRole.IncomingList))
        {
            return null;
        }

        LeaveOldCareerBehind();
        var shown = ShownPlaces(messages);
        _lists[key] = new ListState(role, messages, shown);
        if (role == ScreenRole.InboxList)
        {
            _inbox = messages;
            _inboxChanged = true;
        }

        if (shown.Length == messages.Count)
        {
            return null;
        }

        Record(key, role, shown.Length, messages.Count);
        return shown;
    }

    /// <returns>The message to open in its place, or null to leave the game's choice alone.</returns>
    public long? MessageToOpenDelivered(ulong key, long message)
    {
        if (RoleOf(key) != ScreenRole.MessageToOpen)
        {
            return null;
        }

        LeaveOldCareerBehind();

        // The game repeats itself, and a message given in the named one's place stays the answer
        // while it can be seen.
        var before = _toOpen.TryGetValue(key, out var earlier) && earlier.Named == message ? earlier.Given : message;
        var given = MessageToOpen(message, before);
        _toOpen[key] = (message, given);
        if (given == message)
        {
            return null;
        }

        _log.Write($"OPEN key={key} {message} -> {given}");
        return given;
    }

    /// <returns>The position to give in its place, or null to leave the game's alone.</returns>
    public int? PositionDelivered(ulong key, int position)
    {
        if (RoleOf(key) != ScreenRole.UnreadPosition)
        {
            return null;
        }

        LeaveOldCareerBehind();
        var given = Position(position);
        _positions[key] = (position, given);
        if (given == position)
        {
            return null;
        }

        _log.Write($"ANCHOR key={key} {position} -> {given}");
        return given;
    }

    /// <summary>
    /// Gives up for the rest of the session. From here on every value is the game's own; what the
    /// screen was already given stays until the game delivers it again.
    /// </summary>
    public void Stop(string where)
    {
        if (Stopped)
        {
            return;
        }

        Stopped = true;
        _lists.Clear();
        _toOpen.Clear();
        _positions.Clear();
        _log.Write($"NOTE hiding stopped: {where}");
    }

    /// <summary>
    /// What the screen must be given now, because what is shown has changed since the game's last
    /// delivery: lists first, then the values that point into them. Called on the game's tick.
    /// </summary>
    public IReadOnlyList<ScreenUpdate> Refresh()
    {
        if (Stopped)
        {
            return Array.Empty<ScreenUpdate>();
        }

        LeaveOldCareerBehind();
        foreach (var message in _visibility.ReleaseOverdue())
        {
            _log.Write($"NOTE released {message}");
        }

        if (_visibility.Version == _versionGiven && !_inboxChanged)
        {
            return Array.Empty<ScreenUpdate>();
        }

        _versionGiven = _visibility.Version;
        _inboxChanged = false;

        var updates = new List<ScreenUpdate>();
        foreach (var key in _lists.Keys.OrderBy(key => key).ToArray())
        {
            var list = _lists[key];
            var shown = ShownPlaces(list.Messages);
            if (!shown.AsSpan().SequenceEqual(list.Given))
            {
                _lists[key] = list with { Given = shown };
                Record(key, list.Role, shown.Length, list.Messages.Count);
                updates.Add(new ListUpdate(key, shown));
            }
        }

        foreach (var key in _positions.Keys.OrderBy(key => key).ToArray())
        {
            var (named, given) = _positions[key];
            var position = Position(named);
            if (position != given)
            {
                _positions[key] = (named, position);
                _log.Write($"ANCHOR key={key} {named} -> {position}");
                updates.Add(new PositionUpdate(key, position));
            }
        }

        foreach (var key in _toOpen.Keys.OrderBy(key => key).ToArray())
        {
            var (named, given) = _toOpen[key];
            var message = MessageToOpen(named, given);
            if (message != given)
            {
                _toOpen[key] = (named, message);
                _log.Write($"OPEN key={key} {named} -> {message}");
                updates.Add(new MessageToOpenUpdate(key, message));
            }
        }

        return updates;
    }

    // What the game delivered for another career is about other messages under the same numbers.
    // The screen is given nothing from it: the new career's values arrive on their own.
    private void LeaveOldCareerBehind()
    {
        if (_career == _visibility.Career)
        {
            return;
        }

        _career = _visibility.Career;
        _lists.Clear();
        _toOpen.Clear();
        _positions.Clear();
        _inbox = Array.Empty<long?>();
    }

    private static ScreenRole RoleNamed(string? printedProperty)
    {
        var name = printedProperty.AsSpan();
        var separator = name.IndexOf(':');
        name = separator < 0 ? name : name[..separator];
        foreach (var (property, role) in Roles)
        {
            if (name.SequenceEqual(property))
            {
                return role;
            }
        }

        return ScreenRole.None;
    }

    private bool IsShown(long? message) => message is not { } number || _visibility.IsShown(number);

    private int[] ShownPlaces(IReadOnlyList<long?> messages) =>
        Enumerable.Range(0, messages.Count).Where(place => IsShown(messages[place])).ToArray();

    // The game's choice stands when it can be seen. Otherwise the message given before stands
    // while it can be seen: the inbox list changes with every search, and the message to open must
    // not follow it around. Only then is the first shown message of the inbox offered, and when
    // the inbox shows nothing, the game's choice stands after all.
    private long MessageToOpen(long named, long given)
    {
        if (_visibility.IsShown(named))
        {
            return named;
        }

        return given != named && _visibility.IsShown(given)
            ? given
            : _inbox.FirstOrDefault(message => message is { } number && _visibility.IsShown(number)) ?? named;
    }

    // The game counts places in its own list. The screen's list is shorter by the messages not shown before that place.
    private int Position(int named) =>
        named - _inbox.Take(Math.Max(named, 0)).Count(message => !IsShown(message));

    private void Record(ulong key, ScreenRole role, int shown, int listed) =>
        _log.Write($"LIST {PropertyOf(role)} key={key} shown={shown} of {listed}");

    private static string PropertyOf(ScreenRole role) => Roles.First(known => known.Role == role).Property;

    private sealed record ListState(ScreenRole Role, IReadOnlyList<long?> Messages, int[] Given);
}
