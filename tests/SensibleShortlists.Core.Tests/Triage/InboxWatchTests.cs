using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class InboxWatchTests
{
    private readonly InboxWatch _watch = new();

    [Theory]
    [InlineData("HumanPortalFilteredMessages:1968194392")]
    [InlineData("HumanIncomingMessages:1145402680")]
    public void The_inbox_lists_are_recognised_by_property_name(string property)
    {
        Assert.Equal(ChannelRole.MessageList, _watch.ChannelOpened(7, property));
        Assert.True(_watch.CarriesMessages(7));
    }

    [Fact]
    public void The_portals_competition_channel_reveals_the_managers_team()
    {
        Assert.Equal(ChannelRole.ManagerTeam, _watch.ChannelOpened(7, "CompetitionForPortal:1815442251"));
        Assert.False(_watch.CarriesMessages(7));
    }

    [Theory]
    [InlineData("NewsTitleString:1953068140")]
    [InlineData("HumanPortalFilteredMessagesCount:1")]
    [InlineData(null)]
    public void Other_channels_have_no_role(string? property)
    {
        Assert.Equal(ChannelRole.None, _watch.ChannelOpened(7, property));
        Assert.False(_watch.CarriesMessages(7));
    }

    [Fact]
    public void A_key_reopened_for_another_property_stops_carrying_messages()
    {
        _watch.ChannelOpened(7, "HumanIncomingMessages:1145402680");

        _watch.ChannelOpened(7, "NewsTitleString:1953068140");

        Assert.False(_watch.CarriesMessages(7));
    }

    [Theory]
    [InlineData("IsShortlisted:42", "IsShortlisted")]
    [InlineData("IsShortlisted", "IsShortlisted")]
    [InlineData(null, "")]
    public void A_property_name_is_what_the_game_prints_before_the_colon(string? printed, string name)
    {
        Assert.Equal(name, InboxWatch.NameOf(printed));
    }
}
