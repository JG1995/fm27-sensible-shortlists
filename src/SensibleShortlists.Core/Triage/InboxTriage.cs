using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Triage;

/// <summary>A message as the game lists it: its number and the game's own handle for it.</summary>
public readonly record struct ListedMessage(long Id, object Handle);

/// <summary>One delivery of the inbox: the running game, the manager it belongs to, and its messages.</summary>
/// <param name="Game">The running game's identifier, which every reference to its objects carries.</param>
/// <param name="Manager">The manager's database record.</param>
public sealed record Listing(long Game, uint Manager, IReadOnlyList<ListedMessage> Messages);

/// <summary>
/// Gathers the facts of each listed message from the game, judges it once, and records the
/// verdict. The game answers reads later and in any order, or at once, so every step here is
/// driven by an answer arriving.
/// </summary>
/// <remarks>Not thread-safe: the game calls its UI on one thread, and so everything arrives on it.</remarks>
public sealed class InboxTriage
{
    // The person-search object of type 0 is the one behind the Shortlist screen.
    private const string IsShortlistProperty = "PersonSearchIsShortlist";
    private const string PeopleProperty = "PersonList";

    private readonly IGameReader _game;
    private readonly ITraceSink _log;
    private readonly HashSet<long> _started = new();
    private uint? _managerTeam;
    private long? _runningGame;

    public InboxTriage(IGameReader game, ITraceSink log)
    {
        _game = game;
        _log = log;
    }

    public void ManagerTeamSeen(uint record)
    {
        // The channel that reveals the team is recognised by name only; make sure it was asked of a team.
        if (_managerTeam == record || !new RecordLink(record).IsTeam)
        {
            return;
        }

        _managerTeam = record;
        _log.Write($"TEAM {new RecordLink(record)}");
    }

    public void MessagesListed(Listing listing)
    {
        // Another career was loaded: its messages reuse the same numbers, and its manager may run another team.
        if (_runningGame is { } known && known != listing.Game)
        {
            _started.Clear();
            _managerTeam = null;
            _log.Write($"NOTE game {listing.Game}");
        }

        _runningGame = listing.Game;

        var unseen = listing.Messages.Where(message => _started.Add(message.Id)).ToArray();
        if (unseen.Length == 0)
        {
            return;
        }

        // One read of the shortlist serves every message of the listing. It is read again for the
        // next listing, because the manager changes it between them.
        var shortlist = new ShortlistRead(this, listing.Manager);
        foreach (var message in unseen)
        {
            new Gathering(this, message, shortlist).Start();
        }

        shortlist.Start();
    }

    private void Judge(MessageFacts facts)
    {
        var verdict = QuietPolicy.Judge(facts, _managerTeam);
        var links = string.Join(",", facts.Links.Select(link => link + Mark(facts, link)));
        var title = facts.Title is null ? "?" : $"\"{TraceText.Sanitize(facts.Title)}\"";
        _log.Write(
            $"MESSAGE {facts.Id} {verdict} forwarded={Show(facts.Forwarded)} respond={Show(facts.CanRespond)} "
            + $"responded={Show(facts.Responded)} priority={Show(facts.Priority)} links={links} title={title}");
    }

    private static string Mark(MessageFacts facts, RecordLink link)
    {
        if (!link.IsPerson)
        {
            return string.Empty;
        }

        return facts.Shortlist is null ? "?" : facts.Shortlist.Contains(link.Record) ? "*" : string.Empty;
    }

    private static string Show(bool? fact) => fact?.ToString() ?? "?";

    /// <summary>
    /// The shortlist as it stands for one listing. The game must confirm that the list it hands
    /// over is the shortlist; a list of anything else would quiet the wrong messages.
    /// </summary>
    private sealed class ShortlistRead
    {
        private readonly InboxTriage _triage;
        private readonly uint _manager;
        private readonly List<Action<IReadOnlySet<uint>?>> _waiting = new();
        private bool? _isShortlist;
        private IReadOnlyList<uint>? _people;
        private int _waitingFor;
        private bool _done;
        private IReadOnlySet<uint>? _shortlist;

        public ShortlistRead(InboxTriage triage, uint manager)
        {
            _triage = triage;
            _manager = manager;
        }

        public void Start()
        {
            if (_triage._managerTeam is not { } team)
            {
                Finish();
                return;
            }

            // Counted before asking: a game that answers at once must not see zero too early.
            _waitingFor = 2;
            Ask(team, IsShortlistProperty, value => _isShortlist = value?.AsFlag());
            Ask(team, PeopleProperty, value => _people = value?.Records);
        }

        public void WhenRead(Action<IReadOnlySet<uint>?> use)
        {
            if (_done)
            {
                use(_shortlist);
                return;
            }

            _waiting.Add(use);
        }

        private void Ask(uint team, string property, Action<GameValue?> keep) =>
            _triage._game.ReadShortlist(_manager, team, property, value =>
            {
                keep(value);
                if (--_waitingFor == 0)
                {
                    Finish();
                }
            });

        private void Finish()
        {
            _shortlist = _isShortlist == true && _people is not null ? _people.ToHashSet() : null;
            _done = true;
            _triage._log.Write(_shortlist is null ? "SHORTLIST ?" : $"SHORTLIST {_shortlist.Count} people");
            foreach (var use in _waiting)
            {
                use(_shortlist);
            }

            _waiting.Clear();
        }
    }

    /// <summary>One message on its way to being judged: asks its questions and counts the answers.</summary>
    private sealed class Gathering
    {
        private readonly InboxTriage _triage;
        private readonly ListedMessage _message;
        private readonly ShortlistRead _shortlist;
        private bool? _canRespond;
        private bool? _responded;
        private bool? _priority;
        private bool? _forwarded;
        private EmbeddedText? _title;
        private int _waitingFor;

        public Gathering(InboxTriage triage, ListedMessage message, ShortlistRead shortlist)
        {
            _triage = triage;
            _message = message;
            _shortlist = shortlist;
        }

        public void Start()
        {
            // Counted before asking: a game that answers at once must not see zero too early.
            _waitingFor = 5;
            Ask("CanRespond", value => _canRespond = value?.AsFlag());
            Ask("NewsHasResponded", value => _responded = value?.AsFlag());
            Ask("NewsItemIsPriority", value => _priority = value?.AsFlag());
            Ask("IsForwarded", value => _forwarded = value?.AsFlag());
            Ask("NewsTitleString", value => _title = value?.Text is { } text ? EmbeddedText.Parse(text) : null);
        }

        private void Ask(string property, Action<GameValue?> keep) =>
            _triage._game.ReadMessage(_message.Handle, property, value =>
            {
                keep(value);
                if (--_waitingFor == 0)
                {
                    _shortlist.WhenRead(Finish);
                }
            });

        private void Finish(IReadOnlySet<uint>? shortlist) => _triage.Judge(new MessageFacts(
            _message.Id,
            _canRespond,
            _responded,
            _priority,
            _forwarded,
            _title?.Plain,
            _title?.Links ?? Array.Empty<RecordLink>(),
            shortlist));
    }
}
