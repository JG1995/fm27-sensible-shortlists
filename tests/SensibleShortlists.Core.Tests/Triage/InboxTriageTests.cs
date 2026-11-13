using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class InboxTriageTests
{
    private const int Game = 6068085;
    private const uint Manager = (1u << 24) | 316821;
    private const string Shortlist = "shortlist";

    private readonly FakeGame _game = new();
    private readonly List<string> _lines = new();
    private InboxTriage _triage;

    public InboxTriageTests() => _triage = Triage(TriageMode.Observe);

    private InboxTriage Triage(TriageMode mode) => new(_game, _game, new ListSink(_lines), mode);

    private static string NoiseTitle =>
        $"{GameText.Link(GameText.Brighton, "Brighton")} make offer for {GameText.Link(GameText.Player, "King")}";

    private void List(params long[] ids) => _triage.MessagesListed(new Listing(
        Game, Manager, ids.Select(id => new ListedMessage(id, $"message {id}")).ToArray()));

    private void AnswerFacts(long id, string title, bool canRespond = false, bool? forwarded = true, bool? read = true)
    {
        _game.Answer($"message {id}", "NewsItemHasBeenRead", read is { } isRead ? Flag(isRead) : null);
        _game.Answer($"message {id}", "CanRespond", Flag(canRespond));
        _game.Answer($"message {id}", "NewsHasResponded", Flag(false));
        _game.Answer($"message {id}", "NewsItemIsPriority", Flag(false));
        _game.Answer($"message {id}", "IsForwarded", forwarded is { } value ? Flag(value) : null);
        _game.Answer($"message {id}", "NewsTitleString", new GameValue(title));
    }

    private void AnswerShortlist(params uint[] people)
    {
        _game.Answer(Shortlist, "PersonSearchIsShortlist", Flag(true));
        _game.Answer(Shortlist, "PersonList", new GameValue("List", people));
    }

    private static GameValue Flag(bool value) => new(value ? "True" : "False");

    [Fact]
    public void A_story_about_a_shortlisted_player_is_judged_quiet_and_recorded()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(12849);
        AnswerFacts(12849, NoiseTitle);
        AnswerShortlist(GameText.OtherPlayer, GameText.Player);

        Assert.Equal(
            new[]
            {
                "TEAM 24:565",
                "SHORTLIST 2 people",
                "MESSAGE 12849 QUIET forwarded=True respond=False responded=False priority=False read=True "
                + "links=24:504,1:155126* title=\"Brighton make offer for King\"",
            },
            _lines);
    }

    [Fact]
    public void Each_person_a_judged_message_links_is_asked_every_candidate_property_once()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1, 2);
        AnswerFacts(1, NoiseTitle);
        AnswerFacts(2, NoiseTitle);
        AnswerShortlist(GameText.Player);

        Assert.Equal(
            new[]
            {
                "PlayerStatusFlagsForTeam", "PlayerStatusFlagsForRecruitment", "PlayerStatus", "DaysLeftOnShortlist",
                "IsShortlisted", "IsBeingScouted", "PersonIsTransferTarget",
            },
            _game.PropertiesAskedOf(GameText.Player));
    }

    [Fact]
    public void An_answer_about_a_person_is_recorded_as_the_games_text_or_as_the_flags_it_holds()
    {
        List(1);
        AnswerFacts(1, NoiseTitle);
        _lines.Clear();

        _game.Answer(GameText.Player, "DaysLeftOnShortlist", new GameValue("27"));
        _game.Answer(
            GameText.Player,
            "PlayerStatusFlagsForRecruitment",
            new GameValue("List`1", Flags: new[] { "IsOnHumanShortlist", "Scouted" }));
        _game.Answer(GameText.Player, "PlayerStatus", new GameValue("List`1", Flags: Array.Empty<string>()));

        Assert.Equal(
            new[]
            {
                "PERSON 1:155126 DaysLeftOnShortlist=27",
                "PERSON 1:155126 PlayerStatusFlagsForRecruitment=[IsOnHumanShortlist,Scouted]",
                "PERSON 1:155126 PlayerStatus=[]",
            },
            _lines);
    }

    [Fact]
    public void An_answer_about_a_person_that_cannot_be_read_is_recorded_as_a_question_mark()
    {
        List(1);
        AnswerFacts(1, NoiseTitle);

        _game.Answer(GameText.Player, "IsShortlisted", null);

        Assert.Equal("PERSON 1:155126 IsShortlisted=?", _lines.Last());
    }

    private void ListAQuietMessage(long id, bool? read)
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(id);
        AnswerFacts(id, NoiseTitle, read: read);
        AnswerShortlist(GameText.Player);
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
    }

    [Fact]
    public void A_kept_message_is_not_marked_even_when_it_is_unread()
    {
        _triage = Triage(TriageMode.Quiet);
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle, canRespond: true, read: false);
        AnswerShortlist(GameText.Player);

        Assert.Contains(_lines, line => line.StartsWith("MESSAGE 1 KEEP(can respond) "));
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

    [Fact]
    public void The_shortlist_is_asked_of_the_manager_and_team_the_listing_belongs_to()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);

        List(1);

        Assert.Equal((Manager, GameText.ManagerTeam), _game.ShortlistAskedOf);
    }

    [Fact]
    public void A_message_waits_for_its_own_facts_and_for_the_shortlist()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        _lines.Clear();

        AnswerShortlist(GameText.Player);
        Assert.Equal(new[] { "SHORTLIST 1 people" }, _lines);

        AnswerFacts(1, NoiseTitle);
        Assert.StartsWith("MESSAGE 1 QUIET", _lines.Last());
    }

    [Fact]
    public void The_shortlist_is_read_once_for_all_the_messages_of_a_listing()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);

        List(1, 2, 3);

        Assert.Equal(2, _game.ReadsOf(Shortlist));
    }

    [Fact]
    public void A_listing_with_nothing_new_reads_nothing()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        var reads = _game.ReadCount;

        List(1);

        Assert.Equal(reads, _game.ReadCount);
    }

    [Fact]
    public void A_later_listing_reads_the_shortlist_again()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        AnswerShortlist();

        List(2);
        AnswerFacts(2, NoiseTitle);
        AnswerShortlist(GameText.Player);

        Assert.StartsWith("MESSAGE 1 KEEP(no shortlisted player)", _lines.Single(line => line.StartsWith("MESSAGE 1 ")));
        Assert.StartsWith("MESSAGE 2 QUIET", _lines.Last());
    }

    [Theory]
    [InlineData("False")]
    [InlineData(null)]
    public void A_list_the_game_does_not_confirm_as_the_shortlist_is_unreadable(string? isShortlist)
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        _game.Answer(Shortlist, "PersonSearchIsShortlist", isShortlist is null ? null : new GameValue(isShortlist));
        _game.Answer(Shortlist, "PersonList", new GameValue("List", new[] { GameText.Player }));

        Assert.Equal(
            new[]
            {
                "SHORTLIST ?",
                "MESSAGE 1 KEEP(shortlist unreadable) forwarded=True respond=False responded=False priority=False read=True "
                + "links=24:504,1:155126? title=\"Brighton make offer for King\"",
            },
            _lines.Skip(1));
    }

    [Fact]
    public void A_list_without_people_in_it_is_unreadable()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle);
        _game.Answer(Shortlist, "PersonSearchIsShortlist", Flag(true));
        _game.Answer(Shortlist, "PersonList", new GameValue("Object:"));

        Assert.StartsWith("MESSAGE 1 KEEP(shortlist unreadable)", _lines.Last());
    }

    [Fact]
    public void Without_the_managers_team_the_shortlist_is_not_read()
    {
        List(1);
        AnswerFacts(1, NoiseTitle);

        Assert.Equal(0, _game.ReadsOf(Shortlist));
        Assert.Equal(
            new[]
            {
                "SHORTLIST ?",
                "MESSAGE 1 KEEP(team unknown) forwarded=True respond=False responded=False priority=False read=True "
                + "links=24:504,1:155126? title=\"Brighton make offer for King\"",
            },
            _lines);
    }

    [Fact]
    public void A_team_learned_later_serves_later_messages_and_leaves_earlier_verdicts_alone()
    {
        List(1);
        AnswerFacts(1, NoiseTitle);

        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(2);
        AnswerFacts(2, NoiseTitle);
        AnswerShortlist(GameText.Player);

        Assert.Equal(
            new[] { "SHORTLIST ?", "MESSAGE 1 KEEP(team unknown)", "TEAM 24:565", "SHORTLIST 1 people", "MESSAGE 2 QUIET" },
            _lines.Select(line => line.Split(" forwarded=")[0]));
    }

    [Fact]
    public void An_unreadable_fact_is_recorded_as_a_question_mark_and_keeps_the_message()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFacts(1, NoiseTitle, forwarded: null);
        AnswerShortlist(GameText.Player);

        Assert.Equal(
            "MESSAGE 1 KEEP(unreadable) forwarded=? respond=False responded=False priority=False read=True "
            + "links=24:504,1:155126* title=\"Brighton make offer for King\"",
            _lines.Last());
    }

    [Fact]
    public void An_unreadable_title_is_recorded_as_a_question_mark()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        _game.Answer("message 1", "CanRespond", Flag(false));
        _game.Answer("message 1", "NewsHasResponded", Flag(false));
        _game.Answer("message 1", "NewsItemIsPriority", Flag(false));
        _game.Answer("message 1", "IsForwarded", Flag(true));
        _game.Answer("message 1", "NewsItemHasBeenRead", Flag(true));
        _game.Answer("message 1", "NewsTitleString", null);
        AnswerShortlist();

        Assert.EndsWith("links= title=?", _lines.Last());
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
        _game.AnswerImmediatelyWith = null;
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        _lines.Clear();

        _triage.MessagesListed(new Listing(Game + 1, Manager, new[] { new ListedMessage(1, "message 1") }));
        _triage.ManagerTeamSeen(GameText.ManagerTeam);

        Assert.Equal(
            new[] { $"NOTE game {Game + 1}", "SHORTLIST ?", "MESSAGE 1 KEEP(unreadable)", "TEAM 24:565" },
            _lines.Select(line => line.Split(" forwarded=")[0]));
    }

    [Fact]
    public void A_game_that_answers_at_once_still_gets_one_judgement()
    {
        _game.AnswerImmediatelyWith = null;
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        _lines.Clear();

        List(1);

        Assert.Equal(new[] { "SHORTLIST ?", "MESSAGE 1 KEEP(unreadable)" }, _lines.Select(line => line.Split(" forwarded=")[0]));
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
        private static readonly object NoImmediateAnswer = new();

        private readonly List<(object Target, string Property, Action<GameValue?> OnValue)> _waiting = new();
        private readonly List<(object Target, string Property)> _asked = new();

        public object? AnswerImmediatelyWith { get; set; } = NoImmediateAnswer;

        public int ReadCount => _asked.Count;

        public (uint Manager, uint Team)? ShortlistAskedOf { get; private set; }

        public List<object> Marked { get; } = new();

        public void MarkRead(object message) => Marked.Add(message);

        public void ReadMessage(object message, string property, Action<GameValue?> onValue) =>
            Read(message, property, onValue);

        public void ReadShortlist(uint manager, uint team, string property, Action<GameValue?> onValue)
        {
            ShortlistAskedOf = (manager, team);
            Read(Shortlist, property, onValue);
        }

        public void ReadPerson(uint person, string property, Action<GameValue?> onValue) =>
            Read(person, property, onValue);

        public int ReadsOf(object target) => _asked.Count(asked => asked.Target.Equals(target));

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
            if (!ReferenceEquals(AnswerImmediatelyWith, NoImmediateAnswer))
            {
                onValue((GameValue?)AnswerImmediatelyWith);
                return;
            }

            _waiting.Add((target, property, onValue));
        }
    }
}
