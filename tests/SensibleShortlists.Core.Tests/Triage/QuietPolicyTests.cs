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
        Read: null,
        Title: "Brighton make offer for King",
        Links: new[] { new RecordLink(GameText.Brighton), new RecordLink(GameText.Player) },
        Shortlist: new HashSet<uint> { GameText.Player });

    private static string Judge(MessageFacts facts, uint? managerTeam = GameText.ManagerTeam) =>
        QuietPolicy.Judge(facts, managerTeam).ToString();

    [Fact]
    public void A_forwarded_story_about_a_shortlisted_player_elsewhere_is_quiet()
    {
        Assert.Equal("QUIET", Judge(Noise));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Whether_the_message_was_read_does_not_change_the_verdict(bool? read)
    {
        Assert.Equal("QUIET", Judge(Noise with { Read = read }));
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
    public void A_story_that_links_no_person_is_kept_whatever_the_shortlist()
    {
        var facts = Noise with { Links = new[] { new RecordLink(GameText.Brighton) }, Shortlist = null };

        Assert.Equal("KEEP(no shortlisted player)", Judge(facts));
    }

    [Fact]
    public void A_story_about_a_person_is_kept_while_the_shortlist_is_unreadable()
    {
        Assert.Equal("KEEP(shortlist unreadable)", Judge(Noise with { Shortlist = null }));
    }

    [Fact]
    public void A_story_about_a_person_who_is_not_shortlisted_is_kept()
    {
        var facts = Noise with { Shortlist = new HashSet<uint> { GameText.OtherPlayer } };

        Assert.Equal("KEEP(no shortlisted player)", Judge(facts));
    }

    [Fact]
    public void An_empty_shortlist_keeps_every_story()
    {
        Assert.Equal("KEEP(no shortlisted player)", Judge(Noise with { Shortlist = new HashSet<uint>() }));
    }

    [Fact]
    public void A_message_sent_directly_is_not_blamed_on_an_unreadable_shortlist()
    {
        Assert.Equal("KEEP(sent directly)", Judge(Noise with { Forwarded = false, Shortlist = null }));
    }

    [Fact]
    public void An_earlier_rule_wins_over_a_later_one()
    {
        var facts = Noise with { CanRespond = true, Priority = true, Forwarded = false };

        Assert.Equal("KEEP(can respond)", Judge(facts, managerTeam: null));
    }
}
