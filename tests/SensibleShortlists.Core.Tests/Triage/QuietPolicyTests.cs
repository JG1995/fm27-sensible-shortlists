using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class QuietPolicyTests
{
    // A forwarded story about another club bidding for a shortlisted player: the case to quiet.
    private static readonly MessageFacts Noise = new(
        Id: 1,
        CanRespond: false,
        Responded: false,
        Priority: false,
        Forwarded: true,
        Title: "Brighton make offer for King",
        Links: new[] { new RecordLink(GameText.Brighton), new RecordLink(GameText.Player) },
        Shortlisted: new Dictionary<uint, bool?> { [GameText.Player] = true });

    private static string Judge(MessageFacts facts, uint? managerTeam = GameText.ManagerTeam) =>
        QuietPolicy.Judge(facts, managerTeam).ToString();

    [Fact]
    public void A_forwarded_story_about_a_shortlisted_player_elsewhere_is_quiet()
    {
        Assert.Equal("QUIET", Judge(Noise));
    }

    [Fact]
    public void An_unreadable_flag_keeps_the_message()
    {
        Assert.Equal("KEEP(unreadable)", Judge(Noise with { Forwarded = null }));
    }

    [Fact]
    public void An_unreadable_title_keeps_the_message()
    {
        Assert.Equal("KEEP(unreadable)", Judge(Noise with { Title = null }));
    }

    [Fact]
    public void An_unreadable_shortlist_answer_keeps_the_message()
    {
        var facts = Noise with { Shortlisted = new Dictionary<uint, bool?> { [GameText.Player] = null } };

        Assert.Equal("KEEP(unreadable)", Judge(facts));
    }

    [Fact]
    public void A_message_the_manager_can_respond_to_is_kept()
    {
        Assert.Equal("KEEP(can respond)", Judge(Noise with { CanRespond = true }));
    }

    [Fact]
    public void A_message_the_manager_responded_to_is_kept()
    {
        Assert.Equal("KEEP(responded)", Judge(Noise with { Responded = true }));
    }

    [Fact]
    public void A_priority_message_is_kept()
    {
        Assert.Equal("KEEP(priority)", Judge(Noise with { Priority = true }));
    }

    [Fact]
    public void A_message_sent_to_the_manager_directly_is_kept()
    {
        Assert.Equal("KEEP(sent directly)", Judge(Noise with { Forwarded = false }));
    }

    [Fact]
    public void Nothing_is_quiet_while_the_managers_team_is_unknown()
    {
        Assert.Equal("KEEP(team unknown)", Judge(Noise, managerTeam: null));
    }

    [Fact]
    public void A_story_that_links_the_managers_team_is_kept()
    {
        var facts = Noise with
        {
            Links = new[] { new RecordLink(GameText.ManagerTeam), new RecordLink(GameText.Player) },
        };

        Assert.Equal("KEEP(own club)", Judge(facts));
    }

    [Fact]
    public void A_story_with_no_shortlisted_player_is_kept()
    {
        var facts = Noise with { Shortlisted = new Dictionary<uint, bool?> { [GameText.Player] = false } };

        Assert.Equal("KEEP(no shortlisted player)", Judge(facts));
    }

    [Fact]
    public void A_story_without_links_is_kept()
    {
        var facts = Noise with { Links = Array.Empty<RecordLink>(), Shortlisted = new Dictionary<uint, bool?>() };

        Assert.Equal("KEEP(no shortlisted player)", Judge(facts));
    }

    [Fact]
    public void An_earlier_rule_wins_over_a_later_one()
    {
        var facts = Noise with { CanRespond = true, Priority = true, Forwarded = false };

        Assert.Equal("KEEP(can respond)", Judge(facts, managerTeam: null));
    }
}
