using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Triage;

/// <summary>A message as the game lists it: its number and the game's own handle for it.</summary>
public readonly record struct ListedMessage(long Id, object Handle);

/// <summary>
/// Gathers the facts of each listed message from the game, judges it once, and records the
/// verdict. The game answers reads later and in any order, or at once, so every step here is
/// driven by an answer arriving.
/// </summary>
/// <remarks>Not thread-safe: the game calls its UI on one thread, and so everything arrives on it.</remarks>
public sealed class InboxTriage
{
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

    /// <param name="game">The running game's identifier, which every reference to its objects carries.</param>
    public void MessagesListed(long game, IReadOnlyList<ListedMessage> messages)
    {
        // Another career was loaded: its messages reuse the same numbers, and its manager may run another team.
        if (_runningGame is { } known && known != game)
        {
            _started.Clear();
            _managerTeam = null;
            _log.Write($"NOTE game {game}");
        }

        _runningGame = game;
        foreach (var message in messages)
        {
            if (_started.Add(message.Id))
            {
                new Gathering(this, message).Start();
            }
        }
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

    private static string Mark(MessageFacts facts, RecordLink link) =>
        facts.Shortlisted.TryGetValue(link.Record, out var shortlisted)
            ? shortlisted switch { true => "*", false => string.Empty, null => "?" }
            : string.Empty;

    private static string Show(bool? fact) => fact?.ToString() ?? "?";

    /// <summary>One message on its way to being judged: asks its questions and counts the answers.</summary>
    private sealed class Gathering
    {
        private readonly InboxTriage _triage;
        private readonly ListedMessage _message;
        private readonly Dictionary<uint, bool?> _shortlisted = new();
        private bool? _canRespond;
        private bool? _responded;
        private bool? _priority;
        private bool? _forwarded;
        private EmbeddedText? _title;
        private int _waitingFor;

        public Gathering(InboxTriage triage, ListedMessage message)
        {
            _triage = triage;
            _message = message;
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
                    AskAboutPeople();
                }
            });

        private void AskAboutPeople()
        {
            var people = (_title?.Links ?? Array.Empty<RecordLink>())
                .Where(link => link.IsPerson)
                .Select(link => link.Record)
                .Distinct()
                .ToArray();
            if (people.Length == 0)
            {
                Finish();
                return;
            }

            _waitingFor = people.Length;
            foreach (var person in people)
            {
                _triage._game.ReadPerson(person, "IsShortlisted", value =>
                {
                    _shortlisted[person] = value?.AsFlag();
                    if (--_waitingFor == 0)
                    {
                        Finish();
                    }
                });
            }
        }

        private void Finish() => _triage.Judge(new MessageFacts(
            _message.Id,
            _canRespond,
            _responded,
            _priority,
            _forwarded,
            _title?.Plain,
            _title?.Links ?? Array.Empty<RecordLink>(),
            _shortlisted));
    }
}
