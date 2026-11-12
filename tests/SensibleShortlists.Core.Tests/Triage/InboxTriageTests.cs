using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class InboxTriageTests
{
    private readonly FakeGame _game = new();
    private readonly List<string> _lines = new();
    private readonly InboxTriage _triage;

    public InboxTriageTests() => _triage = new InboxTriage(_game, new ListSink(_lines));

    private static string NoiseTitle =>
        $"{GameText.Link(GameText.Brighton, "Brighton")} make offer for {GameText.Link(GameText.Player, "King")}";

    private void List(params long[] ids) =>
        _triage.MessagesListed(ids.Select(id => new ListedMessage(id, $"message {id}")).ToArray());

    private void AnswerFlags(long id, bool canRespond = false, bool? forwarded = true)
    {
        _game.Answer($"message {id}", "CanRespond", Flag(canRespond));
        _game.Answer($"message {id}", "NewsHasResponded", Flag(false));
        _game.Answer($"message {id}", "NewsItemIsPriority", Flag(false));
        _game.Answer($"message {id}", "IsForwarded", forwarded is { } value ? Flag(value) : null);
    }

    private static GameValue Flag(bool value) => new(value ? "True" : "False", null);

    [Fact]
    public void A_story_about_a_shortlisted_player_is_judged_quiet_and_recorded()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(12849);
        AnswerFlags(12849);
        _game.Answer("message 12849", "NewsTitleString", new GameValue(NoiseTitle, null));
        _game.Answer(GameText.Player, "IsShortlisted", Flag(true));

        Assert.Equal(
            new[]
            {
                "TEAM 24:565",
                "MESSAGE 12849 QUIET forwarded=True respond=False responded=False priority=False "
                + "links=24:504,1:155126* title=\"Brighton make offer for King\"",
            },
            _lines);
    }

    [Fact]
    public void A_message_is_not_judged_before_every_fact_has_arrived()
    {
        List(1);
        AnswerFlags(1);
        _game.Answer("message 1", "NewsTitleString", new GameValue(NoiseTitle, null));

        Assert.Empty(_lines);
        Assert.Equal(new[] { (object)GameText.Player }, _game.Waiting("IsShortlisted"));
    }

    [Fact]
    public void A_message_without_person_links_is_judged_without_asking_about_anyone()
    {
        List(1);
        AnswerFlags(1, forwarded: false);
        _game.Answer("message 1", "NewsTitleString", new GameValue("Coaching Advice", null));

        Assert.Equal(
            new[]
            {
                "MESSAGE 1 KEEP(sent directly) forwarded=False respond=False responded=False priority=False "
                + "links= title=\"Coaching Advice\"",
            },
            _lines);
        Assert.Empty(_game.Waiting("IsShortlisted"));
    }

    [Fact]
    public void An_unreadable_fact_is_recorded_as_a_question_mark_and_keeps_the_message()
    {
        _triage.ManagerTeamSeen(GameText.ManagerTeam);
        List(1);
        AnswerFlags(1, forwarded: null);
        _game.Answer("message 1", "NewsTitleString", new GameValue(NoiseTitle, null));
        _game.Answer(GameText.Player, "IsShortlisted", null);

        Assert.Equal(
            "MESSAGE 1 KEEP(unreadable) forwarded=? respond=False responded=False priority=False "
            + "links=24:504,1:155126? title=\"Brighton make offer for King\"",
            _lines.Last());
    }

    [Fact]
    public void An_unreadable_title_is_recorded_as_a_question_mark()
    {
        List(1);
        AnswerFlags(1);
        _game.Answer("message 1", "NewsTitleString", null);

        Assert.EndsWith("links= title=?", _lines.Single());
    }

    [Fact]
    public void A_message_listed_again_is_not_read_again()
    {
        List(1);
        var readsAfterFirstListing = _game.ReadCount;

        List(1, 1);

        Assert.Equal(readsAfterFirstListing, _game.ReadCount);
    }

    [Fact]
    public void A_person_linked_twice_is_asked_about_once()
    {
        List(1);
        AnswerFlags(1);
        var title = $"{GameText.Link(GameText.Player, "King")} and {GameText.Link(GameText.Player, "King")} again";
        _game.Answer("message 1", "NewsTitleString", new GameValue(title, null));

        Assert.Single(_game.Waiting("IsShortlisted"));
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
    public void A_game_that_answers_at_once_still_gets_one_judgement()
    {
        _game.AnswerImmediatelyWith = null;

        List(1);

        Assert.Single(_lines);
        Assert.StartsWith("MESSAGE 1 KEEP(unreadable)", _lines[0]);
    }

    private sealed class ListSink : ITraceSink
    {
        private readonly List<string> _lines;

        public ListSink(List<string> lines) => _lines = lines;

        public void Write(string line) => _lines.Add(line);
    }

    /// <summary>Stands in for the game: collects reads and lets a test answer them later.</summary>
    private sealed class FakeGame : IGameReader
    {
        private static readonly object NoImmediateAnswer = new();

        private readonly List<(object Target, string Property, Action<GameValue?> OnValue)> _waiting = new();

        public object? AnswerImmediatelyWith { get; set; } = NoImmediateAnswer;

        public int ReadCount { get; private set; }

        public void ReadMessage(object message, string property, Action<GameValue?> onValue) =>
            Read(message, property, onValue);

        public void ReadPerson(uint record, string property, Action<GameValue?> onValue) =>
            Read(record, property, onValue);

        public object[] Waiting(string property) =>
            _waiting.Where(read => read.Property == property).Select(read => read.Target).ToArray();

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
            ReadCount++;
            if (!ReferenceEquals(AnswerImmediatelyWith, NoImmediateAnswer))
            {
                onValue((GameValue?)AnswerImmediatelyWith);
                return;
            }

            _waiting.Add((target, property, onValue));
        }
    }
}
