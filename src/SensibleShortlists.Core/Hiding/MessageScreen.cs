using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Triage;

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
    private static readonly Dictionary<string, ScreenRole> Roles = new(StringComparer.Ordinal)
    {
        ["HumanPortalFilteredMessages"] = ScreenRole.InboxList,
        ["HumanIncomingMessages"] = ScreenRole.IncomingList,
        ["HumanPortalCurrentMessageToOpen"] = ScreenRole.MessageToOpen,
        ["HumanPortalNewestUnreadMessageIndex"] = ScreenRole.UnreadPosition,
    };

    private readonly MessageVisibility _visibility;
    private readonly ITraceSink _log;
    private readonly Dictionary<ulong, ScreenRole> _roles = new();
    private readonly Dictionary<ulong, ListState> _lists = new();
    private readonly Dictionary<ulong, (long Named, long Given)> _toOpen = new();
    private readonly Dictionary<ulong, (int Named, int Given)> _positions = new();
    private IReadOnlyList<long?> _inbox = Array.Empty<long?>();
    private int _versionGiven;
    private bool _inboxChanged;

    public MessageScreen(MessageVisibility visibility, ITraceSink log)
    {
        _visibility = visibility;
        _log = log;
        _versionGiven = visibility.Version;
    }

    /// <summary>True once the plugin has given up changing what the screen is given.</summary>
    public bool Stopped { get; private set; }

    /// <summary>The UI opened a channel. The game prints a property as <c>Name:number</c>.</summary>
    public ScreenRole ChannelOpened(ulong key, string? printedProperty)
    {
        // A channel just opened has had no delivery, whatever the key carried before.
        Forget(key);
        if (Roles.TryGetValue(InboxWatch.NameOf(printedProperty), out var role))
        {
            _roles[key] = role;
            return role;
        }

        // The game reuses keys, so a key that carried the inbox may now carry something else.
        _roles.Remove(key);
        return ScreenRole.None;
    }

    public ScreenRole RoleOf(ulong key) => Stopped ? ScreenRole.None : _roles.GetValueOrDefault(key);

    /// <param name="messages">The list's items in order: a message's number, or null for an item that is not a message.</param>
    /// <returns>The places of the items to show, or null to leave the game's list alone.</returns>
    public IReadOnlyList<int>? ListDelivered(ulong key, IReadOnlyList<long?> messages)
    {
        var role = RoleOf(key);
        if (role is not (ScreenRole.InboxList or ScreenRole.IncomingList))
        {
            return null;
        }

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

        var given = MessageToOpen(message);
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

        var given = Position(position);
        _positions[key] = (position, given);
        if (given == position)
        {
            return null;
        }

        _log.Write($"ANCHOR key={key} {position} -> {given}");
        return given;
    }

    /// <summary>The game delivered something on the channel that is not what the channel carries.</summary>
    public void NotUnderstood(ulong key) => Forget(key);

    /// <summary>The binding the channel fed no longer exists.</summary>
    public void BindingGone(ulong key) => Forget(key);

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
            var message = MessageToOpen(named);
            if (message != given)
            {
                _toOpen[key] = (named, message);
                _log.Write($"OPEN key={key} {named} -> {message}");
                updates.Add(new MessageToOpenUpdate(key, message));
            }
        }

        return updates;
    }

    private void Forget(ulong key)
    {
        _lists.Remove(key);
        _toOpen.Remove(key);
        _positions.Remove(key);
    }

    private bool IsShown(long? message) => message is not { } number || _visibility.IsShown(number);

    private int[] ShownPlaces(IReadOnlyList<long?> messages) =>
        Enumerable.Range(0, messages.Count).Where(place => IsShown(messages[place])).ToArray();

    // The game's choice stands when it can be seen, and when the inbox shows nothing to offer instead.
    private long MessageToOpen(long named) =>
        _visibility.IsShown(named)
            ? named
            : _inbox.FirstOrDefault(message => message is { } number && _visibility.IsShown(number)) ?? named;

    // The game counts places in its own list. The screen's list is shorter by the messages not shown before that place.
    private int Position(int named) =>
        named - _inbox.Take(Math.Max(named, 0)).Count(message => !IsShown(message));

    private void Record(ulong key, ScreenRole role, int shown, int listed) =>
        _log.Write($"LIST {PropertyOf(role)} key={key} shown={shown} of {listed}");

    private static string PropertyOf(ScreenRole role) => Roles.First(pair => pair.Value == role).Key;

    private sealed record ListState(ScreenRole Role, IReadOnlyList<long?> Messages, int[] Given);
}
