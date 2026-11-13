using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class QuietPolicyTests
{
    // A forwarded story about another club bidding for a player at a third club: the case to quiet.
    private static readonly MessageFacts Noise = new(
        Id: 1,
        Responded: false,
        Priority: false,
        Forwarded: true,
        Read: null,
        Title: "Brighton make offer for King",
        Links: new[] { new RecordLink(GameText.Brighton), new RecordLink(GameText.Player) },
        Clubs: ClubsOf((GameText.Player, new[] { GameText.BrightonClub })));

    private static IReadOnlyDictionary<uint, IReadOnlySet<uint>?> ClubsOf(params (uint Person, uint[]? Clubs)[] people) =>
        people.ToDictionary(person => person.Person, person => (IReadOnlySet<uint>?)person.Clubs?.ToHashSet());

    private static string Judge(
        MessageFacts facts,
        uint? managerTeam = GameText.ManagerTeam,
        uint? managerClub = GameText.ManagerClub) =>
        QuietPolicy.Judge(facts, managerTeam, managerClub).ToString();

    [Fact]
    public void A_forwarded_story_about_a_player_at_another_club_is_quiet()
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
    public void Nothing_is_quiet_while_the_managers_club_is_unknown()
    {
        Assert.Equal("KEEP(club unknown)", Judge(Noise, managerClub: null));
    }

    [Theory]
    [InlineData(GameText.ManagerTeam)]
    [InlineData(GameText.ManagerClub)]
    public void A_story_that_links_the_managers_team_or_club_is_kept(uint own)
    {
        var facts = Noise with { Links = new[] { new RecordLink(own), new RecordLink(GameText.Player) } };

        Assert.Equal("KEEP(own club)", Judge(facts));
    }

    [Fact]
    public void A_story_that_links_no_person_is_kept()
    {
        var facts = Noise with { Links = new[] { new RecordLink(GameText.Brighton) }, Clubs = ClubsOf() };

        Assert.Equal("KEEP(no player)", Judge(facts));
    }

    [Fact]
    public void A_story_about_a_person_whose_clubs_are_unreadable_is_kept()
    {
        Assert.Equal("KEEP(player's club unreadable)", Judge(Noise with { Clubs = ClubsOf((GameText.Player, null)) }));
    }

    [Fact]
    public void A_story_about_a_person_whose_clubs_were_never_gathered_is_kept()
    {
        Assert.Equal("KEEP(player's club unreadable)", Judge(Noise with { Clubs = ClubsOf() }));
    }

    [Fact]
    public void A_story_about_one_of_the_managers_own_players_is_kept()
    {
        // On loan elsewhere: the club played for is another club, the contract is the manager's club's.
        var facts = Noise with { Clubs = ClubsOf((GameText.Player, new[] { GameText.BrightonClub, GameText.ManagerClub })) };

        Assert.Equal("KEEP(own player)", Judge(facts));
    }

    [Fact]
    public void A_story_about_a_player_without_a_club_is_quiet()
    {
        Assert.Equal("QUIET", Judge(Noise with { Clubs = ClubsOf((GameText.Player, Array.Empty<uint>())) }));
    }

    [Fact]
    public void One_own_player_among_the_people_a_story_links_keeps_it()
    {
        var facts = Noise with
        {
            Links = new[] { new RecordLink(GameText.Player), new RecordLink(GameText.OtherPlayer) },
            Clubs = ClubsOf(
                (GameText.Player, new[] { GameText.BrightonClub }),
                (GameText.OtherPlayer, new[] { GameText.ManagerClub })),
        };

        Assert.Equal("KEEP(own player)", Judge(facts));
    }

    [Fact]
    public void A_message_sent_directly_is_not_blamed_on_unreadable_clubs()
    {
        var facts = Noise with { Forwarded = false, Clubs = ClubsOf((GameText.Player, null)) };

        Assert.Equal("KEEP(sent directly)", Judge(facts, managerClub: null));
    }

    [Fact]
    public void An_earlier_rule_wins_over_a_later_one()
    {
        var facts = Noise with { Responded = true, Priority = true, Forwarded = false };

        Assert.Equal("KEEP(responded)", Judge(facts, managerTeam: null));
    }
}
