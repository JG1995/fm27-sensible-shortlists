using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class InboxTriageTests
{
    private const int Game = 6068085;
    private const uint Manager = (1u << 24) | 316821;

    private readonly FakeGame _game = new();
    private readonly List<string> _lines = new();
    private InboxTriage _triage;

    public InboxTriageTests() => _triage = Triage(TriageMode.Observe);

    private static string NoiseTitle =>
        $"{GameText.Link(GameText.Brighton, "Brighton")} make offer for {GameText.Link(GameText.Player, "King")}";

    private InboxTriage Triage(TriageMode mode) => new(_game, _game, new ListSink(_lines), mode);

    private void List(params long[] ids) => _triage.MessagesListed(new Listing(
        Game, Manager, ids.Select(id => new ListedMessage(id, $"message {id}")).ToArray()));

    private void AnswerFacts(long id, string? title, bool? forwarded = true, bool? read = true)
    {
        _game.Answer($"message {id}", "NewsItemHasBeenRead", read is { } isRead ? Flag(isRead) : null);
        _game.Answer($"message {id}", "NewsHasResponded", Flag(false));
        _game.Answer($"message {id}", "NewsItemIsPriority", Flag(false));
        _game.Answer($"message {id}", "IsForwarded", forwarded is { } value ? Flag(value) : null);
        _game.Answer($"message {id}", "NewsTitleString", title is null ? null : new GameValue(title));
    }

    private void AnswerManagerClub(uint? club = GameText.ManagerClub) =>
        _game.Answer(Manager, "Club", club is { } known ? Club(known) : null);

    private void AnswerClubs(uint person, uint? club = GameText.BrightonClub, uint? contractClub = GameText.BrightonClub)
    {
        _game.Answer(person, "Club", Club(club));
        _game.Answer(person, "ContractClub", Club(contractClub));
    }

    /// <summary>A message about a player at another club, with everything the triage asks answered.</summary>
    private void ListAQuietMessage(long id, bool? read = true)
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(id);
        AnswerFacts(id, NoiseTitle, read: read);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();
    }

    private static GameValue Flag(bool value) => new(value ? "True" : "False");

    // The game answers a club as a game object, and "no club" as a value that is not one.
    private static GameValue Club(uint? club) =>
        club is { } record ? new GameValue("ClubReference", new[] { record }) : new GameValue("NullReference");

    [Fact]
    public void A_forwarded_story_about_a_player_at_another_club_is_judged_quiet_and_recorded()
    {
        ListAQuietMessage(12849);

        Assert.Equal(
            new[]
            {
                "TEAM 24:565",
                "CLUB 3:439",
                "MESSAGE 12849 QUIET forwarded=True responded=False priority=False read=True "
                + "links=24:504,1:155126 title=\"Brighton make offer for King\"",
            },
            _lines);
    }

    [Fact]
    public void Each_person_a_title_links_is_asked_for_both_clubs_once()
    {
        List(1);

        AnswerFacts(1, $"{GameText.Link(GameText.Player, "King")} and {GameText.Link(GameText.Player, "King")} again");

        Assert.Equal(new[] { "Club", "ContractClub" }, _game.PropertiesAskedOf(GameText.Player));
    }

    [Fact]
    public void A_story_about_one_of_the_managers_own_players_is_kept_and_the_player_is_marked()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player, club: GameText.BrightonClub, contractClub: GameText.ManagerClub);
        AnswerManagerClub();

        Assert.Equal(
            "MESSAGE 1 KEEP(own player) forwarded=True responded=False priority=False read=True "
            + "links=24:504,1:155126! title=\"Brighton make offer for King\"",
            _lines.Last());
    }

    [Fact]
    public void A_person_whose_club_cannot_be_read_keeps_the_story_and_is_marked()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        _game.Answer(GameText.Player, "Club", Club(GameText.BrightonClub));
        _game.Answer(GameText.Player, "ContractClub", null);
        AnswerManagerClub();

        Assert.Equal(
            "MESSAGE 1 KEEP(player's club unreadable) forwarded=True responded=False priority=False read=True "
            + "links=24:504,1:155126? title=\"Brighton make offer for King\"",
            _lines.Last());
    }

    [Fact]
    public void A_player_without_a_club_does_not_keep_the_story()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player, club: null, contractClub: null);
        AnswerManagerClub();

        Assert.StartsWith("MESSAGE 1 QUIET ", _lines.Last());
    }

    [Fact]
    public void The_managers_club_is_asked_of_the_manager_the_listing_belongs_to_once_for_all_its_messages()
    {
        List(1, 2, 3);

        Assert.Equal(new[] { "Club" }, _game.PropertiesAskedOf(Manager));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_managers_club_that_cannot_be_read_or_is_no_club_keeps_every_story(bool answered)
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player);
        _game.Answer(Manager, "Club", answered ? Club(null) : null);

        Assert.Equal("CLUB ?", _lines[1]);
        Assert.StartsWith("MESSAGE 1 KEEP(club unknown) ", _lines.Last());
    }

    [Fact]
    public void A_message_waits_for_its_own_facts_its_peoples_clubs_and_the_managers_club()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        _lines.Clear();

        AnswerManagerClub();
        Assert.Equal(new[] { "CLUB 3:439" }, _lines);

        AnswerFacts(1, NoiseTitle);
        Assert.Single(_lines);

        AnswerClubs(GameText.Player);
        Assert.StartsWith("MESSAGE 1 QUIET", _lines.Last());
    }

    [Fact]
    public void A_listing_with_nothing_new_reads_nothing()
    {
        List(1);
        var reads = _game.ReadCount;

        List(1);

        Assert.Equal(reads, _game.ReadCount);
    }

    [Fact]
    public void A_later_listing_reads_the_managers_club_again()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player);
        AnswerManagerClub(null);

        List(2);
        AnswerFacts(2, NoiseTitle);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        Assert.StartsWith("MESSAGE 1 KEEP(club unknown)", _lines.Single(line => line.StartsWith("MESSAGE 1 ")));
        Assert.StartsWith("MESSAGE 2 QUIET", _lines.Last());
    }

    [Fact]
    public void Without_the_managers_team_a_story_is_kept()
    {
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        Assert.StartsWith("MESSAGE 1 KEEP(team unknown) ", _lines.Last());
    }

    [Fact]
    public void A_team_learned_later_serves_later_messages_and_leaves_earlier_verdicts_alone()
    {
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        ListAQuietMessage(2);

        Assert.Equal(
            new[] { "CLUB 3:439", "MESSAGE 1 KEEP(team unknown)", "TEAM 24:565", "CLUB 3:439", "MESSAGE 2 QUIET" },
            _lines.Select(line => line.Split(" forwarded=")[0]));
    }

    [Fact]
    public void An_unreadable_fact_is_recorded_as_a_question_mark_and_keeps_the_message()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle, forwarded: null);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        Assert.Equal(
            "MESSAGE 1 KEEP(unreadable) forwarded=? responded=False priority=False read=True "
            + "links=24:504,1:155126 title=\"Brighton make offer for King\"",
            _lines.Last());
    }

    [Fact]
    public void An_unreadable_title_is_recorded_as_a_question_mark_and_asks_about_nobody()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, title: null);
        AnswerManagerClub();

        Assert.EndsWith("links= title=?", _lines.Last());
        Assert.Empty(_game.PropertiesAskedOf(GameText.Player));
    }

    [Fact]
    public void The_managers_team_is_recorded_when_it_becomes_known_and_when_it_changes()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        _triage.ManagerTeamSeen(GameText.Brighton);

        Assert.Equal(new[] { "TEAM 24:565", "TEAM 24:504" }, _lines);
    }

    [Fact]
    public void A_record_that_is_not_a_team_is_not_taken_as_the_managers_team()
    {
        _triage.ManagerTeamSeen(GameText.Player);

        Assert.Empty(_lines);
    }

    [Fact]
    public void Loading_another_game_forgets_the_team_and_the_messages_already_judged()
    {
        ListAQuietMessage(1);
        _lines.Clear();

        _triage.MessagesListed(new Listing(Game + 1, Manager, new[] { new ListedMessage(1, "message 1") }));
        AnswerFacts(1, NoiseTitle);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        Assert.Equal(
            new[] { $"NOTE game {Game + 1}", "CLUB 3:439", "MESSAGE 1 KEEP(team unknown)" },
            _lines.Select(line => line.Split(" forwarded=")[0]));
    }

    [Fact]
    public void A_game_that_answers_at_once_is_judged_just_the_same()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        _game.AnswerImmediately = (target, property) => (target, property) switch
        {
            (uint person, _) when person == Manager => Club(GameText.ManagerClub),
            (uint, _) => Club(GameText.BrightonClub),
            (_, "NewsTitleString") => new GameValue(NoiseTitle),
            (_, "IsForwarded") => Flag(true),
            _ => Flag(false),
        };

        List(1);

        Assert.StartsWith("MESSAGE 1 QUIET ", _lines.Last());
        Assert.Single(_lines, line => line.StartsWith("MESSAGE "));
    }

    [Fact]
    public void In_quiet_mode_an_unread_quiet_message_is_marked_read_once()
    {
        _triage = Triage(TriageMode.Quiet);

        ListAQuietMessage(12849, read: false);
        List(12849);

        Assert.Equal(new object[] { "message 12849" }, _game.Marked);
        Assert.Contains(_lines, line => line.StartsWith("MESSAGE 12849 QUIET ") && line.Contains(" read=False "));
    }

    [Theory]
    [InlineData(true, "MARK 12849 read=True")]
    [InlineData(false, "MARK 12849 read=False")]
    [InlineData(null, "MARK 12849 read=?")]
    public void What_the_game_says_about_a_marked_message_afterwards_is_recorded(bool? readAfterwards, string line)
    {
        _triage = Triage(TriageMode.Quiet);
        ListAQuietMessage(12849, read: false);
        _lines.Clear();

        _game.Answer("message 12849", "NewsItemHasBeenRead", readAfterwards is { } read ? Flag(read) : null);

        Assert.Equal(new[] { line }, _lines);
    }

    [Theory]
    [InlineData(true, " read=True ")]
    [InlineData(null, " read=? ")]
    public void A_quiet_message_that_is_read_or_whose_read_state_is_unknown_is_not_marked(bool? read, string shown)
    {
        _triage = Triage(TriageMode.Quiet);

        ListAQuietMessage(12849, read);

        Assert.Empty(_game.Marked);
        Assert.Contains(_lines, line => line.StartsWith("MESSAGE 12849 QUIET ") && line.Contains(shown));
        Assert.DoesNotContain(_lines, line => line.StartsWith("MARK "));
    }

    [Fact]
    public void Every_unread_quiet_message_of_a_backlog_is_marked()
    {
        _triage = Triage(TriageMode.Quiet);
        _triage.ManagerTeamSeen(GameText.ManagerTeam);

        List(1, 2, 3);
        AnswerFacts(1, NoiseTitle, read: false);
        AnswerClubs(GameText.Player);
        AnswerFacts(2, NoiseTitle, read: true);
        AnswerClubs(GameText.Player);
        AnswerFacts(3, NoiseTitle, read: false);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        Assert.Equal(new object[] { "message 1", "message 3" }, _game.Marked);
    }

    [Fact]
    public void A_message_number_marked_in_one_career_is_marked_again_in_another()
    {
        _triage = Triage(TriageMode.Quiet);
        ListAQuietMessage(12849, read: false);
        _game.Answer("message 12849", "NewsItemHasBeenRead", Flag(true));

        // The other career's first listing is what reveals it. Its team becomes known after that.
        var other = Game + 1;
        _triage.MessagesListed(new Listing(other, Manager, new[] { new ListedMessage(7, "message 7") }));
        AnswerManagerClub();
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        _triage.MessagesListed(new Listing(
            other, Manager, new[] { new ListedMessage(7, "message 7"), new ListedMessage(12849, "message 12849") }));
        AnswerFacts(12849, NoiseTitle, read: false);
        AnswerClubs(GameText.Player);
        AnswerManagerClub();

        Assert.Equal(new object[] { "message 12849", "message 12849" }, _game.Marked);
    }

    [Fact]
    public void A_kept_message_is_not_marked_even_when_it_is_unread()
    {
        _triage = Triage(TriageMode.Quiet);
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle, read: false);
        AnswerClubs(GameText.Player, contractClub: GameText.ManagerClub);
        AnswerManagerClub();

        Assert.Contains(_lines, line => line.StartsWith("MESSAGE 1 KEEP(own player) "));
        Assert.Empty(_game.Marked);
    }

    [Fact]
    public void In_observe_mode_nothing_is_marked()
    {
        ListAQuietMessage(12849, read: false);

        Assert.Contains(_lines, line => line.StartsWith("MESSAGE 12849 QUIET "));
        Assert.Empty(_game.Marked);
        Assert.DoesNotContain(_lines, line => line.StartsWith("MARK "));
    }

    private sealed class ListSink : ITraceSink
    {
        private readonly List<string> _lines;

        public ListSink(List<string> lines) => _lines = lines;

        public void Write(string line) => _lines.Add(line);
    }

    /// <summary>Stands in for the game: collects reads and lets a test answer them later.</summary>
    private sealed class FakeGame : IGameReader, IGameCommands
    {
        private readonly List<(object Target, string Property, Action<GameValue?> OnValue)> _waiting = new();
        private readonly List<(object Target, string Property)> _asked = new();

        /// <summary>When set, every read is answered at once with what this gives.</summary>
        public Func<object, string, GameValue?>? AnswerImmediately { get; set; }

        public int ReadCount => _asked.Count;

        public List<object> Marked { get; } = new();

        public void MarkRead(object message) => Marked.Add(message);

        public void ReadMessage(object message, string property, Action<GameValue?> onValue) =>
            Read(message, property, onValue);

        public void ReadPerson(uint person, string property, Action<GameValue?> onValue) =>
            Read(person, property, onValue);

        public IReadOnlyList<string> PropertiesAskedOf(object target) =>
            _asked.Where(asked => asked.Target.Equals(target)).Select(asked => asked.Property).ToList();

        public void Answer(object target, string property, GameValue? value)
        {
            var index = _waiting.FindIndex(read => read.Target.Equals(target) && read.Property == property);
            Assert.True(index >= 0, $"The triage never asked for {property} of {target}.");
            var read = _waiting[index];
            _waiting.RemoveAt(index);
            read.OnValue(value);
        }

        private void Read(object target, string property, Action<GameValue?> onValue)
        {
            _asked.Add((target, property));
            if (AnswerImmediately is { } answer)
            {
                onValue(answer(target, property));
                return;
            }

            _waiting.Add((target, property, onValue));
        }
    }
}
