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
/// verdict. In quiet mode it also acts on the verdict: an unread message judged quiet is marked
/// read. The game answers reads later and in any order, or at once, so every step here is driven
/// by an answer arriving.
/// </summary>
/// <remarks>Not thread-safe: the game calls its UI on one thread, and so everything arrives on it.</remarks>
public sealed class InboxTriage
{
    private const string ReadProperty = "NewsItemHasBeenRead";

    // The club a person plays for, and the club that holds the person's contract. A player out on
    // loan belongs to both.
    private static readonly string[] ClubProperties = { "Club", "ContractClub" };

    // For a manager, the club managed.
    private const string ManagerClubProperty = "Club";

    private readonly IGameReader _game;
    private readonly IGameCommands _commands;
    private readonly ITraceSink _log;
    private readonly TriageMode _mode;
    private readonly HashSet<long> _started = new();
    private uint? _managerTeam;
    private long? _runningGame;

    public InboxTriage(IGameReader game, IGameCommands commands, ITraceSink log, TriageMode mode)
    {
        _game = game;
        _commands = commands;
        _log = log;
        _mode = mode;
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

        // One read of the manager's club serves every message of the listing. It is read again for
        // the next listing: managers change clubs.
        var club = new ManagerClubRead(this, listing.Manager);
        foreach (var message in unseen)
        {
            new Gathering(this, message, club).Start();
        }

        club.Start();
    }

    private void Judge(ListedMessage message, MessageFacts facts, uint? managerClub)
    {
        var verdict = QuietPolicy.Judge(facts, _managerTeam, managerClub);
        var links = string.Join(",", facts.Links.Select(link => link + Mark(facts, link, managerClub)));
        var title = facts.Title is null ? "?" : $"\"{TraceText.Sanitize(facts.Title)}\"";
        _log.Write(
            $"MESSAGE {facts.Id} {verdict} forwarded={Show(facts.Forwarded)} responded={Show(facts.Responded)} "
            + $"priority={Show(facts.Priority)} read={Show(facts.Read)} links={links} title={title}");

        // Only a message known to be unread is touched: an unknown is not a no.
        if (_mode == TriageMode.Quiet && verdict.IsQuiet && facts.Read == false)
        {
            MarkRead(message);
        }
    }

    // The game is asked afterwards what it now says, so the record shows whether it obeyed.
    private void MarkRead(ListedMessage message)
    {
        _commands.MarkRead(message.Handle);
        _game.ReadMessage(message.Handle, ReadProperty, value => _log.Write($"MARK {message.Id} read={Show(value?.AsFlag())}"));
    }

    // "!" after a person who belongs to the manager's club, "?" after one whose clubs are unreadable.
    private static string Mark(MessageFacts facts, RecordLink link, uint? managerClub)
    {
        if (!link.IsPerson)
        {
            return string.Empty;
        }

        if (facts.Clubs.GetValueOrDefault(link.Record) is not { } clubs)
        {
            return "?";
        }

        return managerClub is { } club && clubs.Contains(club) ? "!" : string.Empty;
    }

    private static string Show(bool? fact) => fact?.ToString() ?? "?";

    // The club records in an answer. A value that is not a game object holds none, which is how
    // the game answers for a person without a club.
    private static IEnumerable<uint> ClubsIn(GameValue value) =>
        (value.Records ?? Array.Empty<uint>()).Where(record => new RecordLink(record).IsClub);

    /// <summary>The manager's club as it stands for one listing.</summary>
    private sealed class ManagerClubRead
    {
        private readonly InboxTriage _triage;
        private readonly uint _manager;
        private readonly List<Action<uint?>> _waiting = new();
        private bool _done;
        private uint? _club;

        public ManagerClubRead(InboxTriage triage, uint manager)
        {
            _triage = triage;
            _manager = manager;
        }

        public void Start() => _triage._game.ReadPerson(_manager, ManagerClubProperty, value =>
        {
            _club = value is null ? null : ClubsIn(value).Cast<uint?>().FirstOrDefault();
            _done = true;
            _triage._log.Write(_club is { } club ? $"CLUB {new RecordLink(club)}" : "CLUB ?");
            foreach (var use in _waiting)
            {
                use(_club);
            }

            _waiting.Clear();
        });

        public void WhenRead(Action<uint?> use)
        {
            if (_done)
            {
                use(_club);
                return;
            }

            _waiting.Add(use);
        }
    }

    /// <summary>One message on its way to being judged: asks its questions and counts the answers.</summary>
    private sealed class Gathering
    {
        private readonly InboxTriage _triage;
        private readonly ListedMessage _message;
        private readonly ManagerClubRead _managerClub;
        private readonly Dictionary<uint, IReadOnlySet<uint>?> _clubs = new();
        private bool? _responded;
        private bool? _priority;
        private bool? _forwarded;
        private bool? _read;
        private EmbeddedText? _title;
        private int _waitingFor;

        public Gathering(InboxTriage triage, ListedMessage message, ManagerClubRead managerClub)
        {
            _triage = triage;
            _message = message;
            _managerClub = managerClub;
        }

        public void Start()
        {
            // Counted before asking: a game that answers at once must not see zero too early.
            _waitingFor = 5;
            Ask("NewsHasResponded", value => _responded = value?.AsFlag());
            Ask("NewsItemIsPriority", value => _priority = value?.AsFlag());
            Ask("IsForwarded", value => _forwarded = value?.AsFlag());
            Ask(ReadProperty, value => _read = value?.AsFlag());
            Ask("NewsTitleString", value =>
            {
                _title = value?.Text is { } text ? EmbeddedText.Parse(text) : null;
                AskForClubs();
            });
        }

        private void Ask(string property, Action<GameValue?> keep) =>
            _triage._game.ReadMessage(_message.Handle, property, value =>
            {
                keep(value);
                Answered();
            });

        // Who the title names is known only once the title has arrived, so these reads follow it.
        // They are counted while the title's own answer is still being counted, so the count
        // cannot reach zero in between.
        private void AskForClubs()
        {
            var people = (_title?.Links ?? Array.Empty<RecordLink>())
                .Where(link => link.IsPerson)
                .Select(link => link.Record)
                .Distinct()
                .ToArray();
            _waitingFor += people.Length * ClubProperties.Length;
            foreach (var person in people)
            {
                var clubs = new HashSet<uint>();
                _clubs[person] = clubs;
                foreach (var property in ClubProperties)
                {
                    _triage._game.ReadPerson(person, property, value =>
                    {
                        // One unreadable answer makes the person's clubs unreadable, whatever the other says.
                        if (value is null)
                        {
                            _clubs[person] = null;
                        }
                        else
                        {
                            clubs.UnionWith(ClubsIn(value));
                        }

                        Answered();
                    });
                }
            }
        }

        private void Answered()
        {
            if (--_waitingFor == 0)
            {
                _managerClub.WhenRead(Finish);
            }
        }

        private void Finish(uint? managerClub) => _triage.Judge(
            _message,
            new MessageFacts(
                _message.Id,
                _responded,
                _priority,
                _forwarded,
                _read,
                _title?.Plain,
                _title?.Links ?? Array.Empty<RecordLink>(),
                _clubs),
            managerClub);
    }
}
