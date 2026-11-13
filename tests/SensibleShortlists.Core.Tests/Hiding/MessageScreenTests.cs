using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Hiding;
using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Hiding;

public class MessageScreenTests
{
    private const ulong Inbox = 10;
    private const ulong Incoming = 11;
    private const ulong ToOpen = 12;
    private const ulong Position = 13;

    private readonly MessageVisibility _visibility;
    private readonly MessageScreen _screen;
    private readonly List<string> _lines = new();
    private TimeSpan _now = TimeSpan.Zero;

    public MessageScreenTests()
    {
        _visibility = new MessageVisibility(TimeSpan.FromSeconds(20), () => _now);
        _screen = new MessageScreen(_visibility, new ListSink(_lines));
        _screen.ChannelOpened(Inbox, "HumanPortalFilteredMessages:1968194392");
        _screen.ChannelOpened(Incoming, "HumanIncomingMessages:1145402680");
        _screen.ChannelOpened(ToOpen, "HumanPortalCurrentMessageToOpen:1467173177");
        _screen.ChannelOpened(Position, "HumanPortalNewestUnreadMessageIndex:1414688888");
    }

    private void Quiet(params long[] ids) => Array.ForEach(ids, id => _visibility.Judged(id, Verdict.Quiet));

    private void Keep(params long[] ids) => Array.ForEach(ids, id => _visibility.Judged(id, Verdict.Keep("sent directly")));

    private static long?[] Messages(params long[] ids) => ids.Select(id => (long?)id).ToArray();

    [Theory]
    [InlineData("HumanPortalFilteredMessages:1968194392", ScreenRole.InboxList)]
    [InlineData("HumanIncomingMessages:1145402680", ScreenRole.IncomingList)]
    [InlineData("HumanPortalCurrentMessageToOpen:1467173177", ScreenRole.MessageToOpen)]
    [InlineData("HumanPortalNewestUnreadMessageIndex:1414688888", ScreenRole.UnreadPosition)]
    [InlineData("NewsTitleString:1953068140", ScreenRole.None)]
    [InlineData(null, ScreenRole.None)]
    public void A_channel_is_recognised_by_its_property_name(string? property, ScreenRole role)
    {
        Assert.Equal(role, _screen.ChannelOpened(7, property));
        Assert.Equal(role, _screen.RoleOf(7));
    }

    [Fact]
    public void A_key_reopened_for_another_property_loses_its_role()
    {
        Quiet(1);

        _screen.ChannelOpened(Inbox, "NewsTitleString:1953068140");

        Assert.Equal(ScreenRole.None, _screen.RoleOf(Inbox));
        Assert.Null(_screen.ListDelivered(Inbox, Messages(1)));
    }

    [Fact]
    public void A_list_is_given_without_its_hidden_messages_in_the_same_order()
    {
        Keep(3, 1);
        Quiet(2);

        Assert.Equal(new[] { 0, 2 }, _screen.ListDelivered(Inbox, Messages(3, 2, 1)));
        Assert.Equal(new[] { "LIST HumanPortalFilteredMessages key=10 shown=2 of 3" }, _lines);
    }

    [Fact]
    public void A_message_awaiting_its_verdict_is_held_back_from_a_list()
    {
        Keep(1);
        _visibility.Awaiting(2);

        Assert.Equal(new[] { 1 }, _screen.ListDelivered(Incoming, Messages(2, 1)));
        Assert.Equal(new[] { "LIST HumanIncomingMessages key=11 shown=1 of 2" }, _lines);
    }

    [Fact]
    public void A_list_with_nothing_to_remove_is_left_to_the_game()
    {
        Keep(1, 2);

        Assert.Null(_screen.ListDelivered(Inbox, Messages(2, 1)));
        Assert.Empty(_lines);
    }

    [Fact]
    public void An_item_that_is_not_a_message_stays_in_the_list()
    {
        Quiet(1);

        Assert.Equal(new[] { 0 }, _screen.ListDelivered(Inbox, new long?[] { null, 1 }));
    }

    [Fact]
    public void A_list_in_which_everything_is_hidden_is_given_empty()
    {
        Quiet(1, 2);

        Assert.Equal(Array.Empty<int>(), _screen.ListDelivered(Inbox, Messages(2, 1)));
    }

    [Fact]
    public void An_empty_list_is_left_to_the_game()
    {
        Assert.Null(_screen.ListDelivered(Inbox, Messages()));
    }

    [Fact]
    public void A_list_on_a_channel_that_carries_no_list_is_left_to_the_game()
    {
        Quiet(1);

        Assert.Null(_screen.ListDelivered(ToOpen, Messages(1)));
        Assert.Null(_screen.ListDelivered(99, Messages(1)));
    }

    [Fact]
    public void A_verdict_after_the_delivery_gives_the_screen_the_list_again()
    {
        Keep(1);
        _visibility.Awaiting(2);
        _visibility.Awaiting(3);
        _screen.ListDelivered(Inbox, Messages(3, 2, 1));
        _lines.Clear();

        Keep(3);
        Quiet(2);

        Assert.Equal(new ScreenUpdate[] { new ListUpdate(Inbox, new[] { 0, 2 }) }, _screen.Refresh(), UpdateComparer.Instance);
        Assert.Equal(new[] { "LIST HumanPortalFilteredMessages key=10 shown=2 of 3" }, _lines);
    }

    [Fact]
    public void A_verdict_that_changes_no_list_gives_the_screen_nothing()
    {
        Keep(1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Quiet(2);

        Assert.Empty(_screen.Refresh());
    }

    [Fact]
    public void Each_channel_that_carries_the_message_is_given_its_own_list()
    {
        _visibility.Awaiting(2);
        Keep(1);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _screen.ListDelivered(Incoming, Messages(2));

        Keep(2);

        Assert.Equal(
            new ScreenUpdate[] { new ListUpdate(Inbox, new[] { 0, 1 }), new ListUpdate(Incoming, new[] { 0 }) },
            _screen.Refresh(),
            UpdateComparer.Instance);
    }

    [Fact]
    public void Nothing_is_given_twice_for_the_same_change()
    {
        _visibility.Awaiting(1);
        _screen.ListDelivered(Inbox, Messages(1));
        Keep(1);
        _screen.Refresh();

        Assert.Empty(_screen.Refresh());
    }

    [Fact]
    public void A_message_released_without_a_verdict_joins_its_lists_and_is_noted()
    {
        _visibility.Awaiting(1);
        _screen.ListDelivered(Inbox, Messages(1));
        _lines.Clear();

        _now += TimeSpan.FromSeconds(20);

        Assert.Equal(new ScreenUpdate[] { new ListUpdate(Inbox, new[] { 0 }) }, _screen.Refresh(), UpdateComparer.Instance);
        Assert.Equal(new[] { "NOTE released 1", "LIST HumanPortalFilteredMessages key=10 shown=1 of 1" }, _lines);
    }

    [Fact]
    public void A_later_delivery_replaces_what_the_channel_remembers()
    {
        _visibility.Awaiting(1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(1));
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Keep(1);

        Assert.Equal(new ScreenUpdate[] { new ListUpdate(Inbox, new[] { 1 }) }, _screen.Refresh(), UpdateComparer.Instance);
    }

    [Fact]
    public void A_reopened_channel_has_had_no_delivery()
    {
        _visibility.Awaiting(1);
        _screen.ListDelivered(Inbox, Messages(1));
        _screen.ChannelOpened(Inbox, "HumanPortalFilteredMessages:1968194392");

        Keep(1);

        Assert.Empty(_screen.Refresh());
    }

    [Fact]
    public void A_channel_told_to_forget_is_given_nothing_until_the_game_delivers_again()
    {
        Keep(1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _screen.MessageToOpenDelivered(ToOpen, 2);
        _screen.PositionDelivered(Position, 1);
        _screen.Forget(Inbox);
        _screen.Forget(ToOpen);
        _screen.Forget(Position);

        Keep(2);

        Assert.Empty(_screen.Refresh());
        Assert.Equal(ScreenRole.InboxList, _screen.RoleOf(Inbox));
    }

    [Fact]
    public void Nothing_delivered_for_one_career_is_given_to_the_screen_in_another()
    {
        Keep(1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _screen.ListDelivered(Incoming, Messages(2));
        _screen.MessageToOpenDelivered(ToOpen, 2);
        _screen.PositionDelivered(Position, 1);

        _visibility.Forget();

        Assert.Empty(_screen.Refresh());
    }

    [Fact]
    public void Another_careers_inbox_does_not_stand_in_for_this_ones()
    {
        Quiet(2);
        Keep(1);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _visibility.Forget();
        Quiet(2);

        Assert.Null(_screen.MessageToOpenDelivered(ToOpen, 2));
        Assert.Null(_screen.PositionDelivered(Position, 1));
    }

    [Fact]
    public void A_message_given_in_anothers_place_stays_while_it_is_shown_whatever_the_inbox_lists()
    {
        Quiet(3);
        Keep(2, 1);
        _screen.ListDelivered(Inbox, Messages(3, 2, 1));
        _screen.MessageToOpenDelivered(ToOpen, 3);
        _screen.Refresh();

        // The global search narrows the inbox list, then finds nothing.
        _screen.ListDelivered(Inbox, Messages(1));
        Assert.Empty(_screen.Refresh());
        _screen.ListDelivered(Inbox, Messages());
        Assert.Empty(_screen.Refresh());

        // The game repeats its choice while the search is on.
        Assert.Equal(2, _screen.MessageToOpenDelivered(ToOpen, 3));
    }

    [Fact]
    public void A_message_given_in_anothers_place_gives_way_when_it_is_hidden_itself()
    {
        Quiet(3);
        Keep(1);
        _visibility.Awaiting(2);
        _now += TimeSpan.FromSeconds(20);
        _screen.Refresh();
        _screen.ListDelivered(Inbox, Messages(3, 2, 1));
        Assert.Equal(2, _screen.MessageToOpenDelivered(ToOpen, 3));

        Quiet(2);

        Assert.Contains(new MessageToOpenUpdate(ToOpen, 1), _screen.Refresh(), UpdateComparer.Instance);
    }

    [Fact]
    public void A_hidden_message_to_open_is_replaced_by_the_first_shown_message_of_the_inbox()
    {
        Quiet(4, 3);
        Keep(2, 1);
        _screen.ListDelivered(Inbox, Messages(4, 3, 2, 1));
        _lines.Clear();

        Assert.Equal(2, _screen.MessageToOpenDelivered(ToOpen, 4));
        Assert.Equal(new[] { "OPEN key=12 4 -> 2" }, _lines);
    }

    [Fact]
    public void A_held_back_message_to_open_is_replaced_until_it_is_judged()
    {
        Keep(1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Assert.Equal(1, _screen.MessageToOpenDelivered(ToOpen, 2));

        Keep(2);
        _lines.Clear();
        Assert.Contains(new MessageToOpenUpdate(ToOpen, 2), _screen.Refresh(), UpdateComparer.Instance);
        Assert.Contains("OPEN key=12 2 -> 2", _lines);
    }

    [Fact]
    public void A_shown_message_to_open_is_left_to_the_game()
    {
        Keep(1, 2);
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Assert.Null(_screen.MessageToOpenDelivered(ToOpen, 1));
        Assert.Empty(_lines);
    }

    [Fact]
    public void A_hidden_message_to_open_is_left_to_the_game_when_the_inbox_shows_nothing()
    {
        Quiet(1, 2);
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Assert.Null(_screen.MessageToOpenDelivered(ToOpen, 2));
    }

    [Fact]
    public void The_incoming_list_does_not_stand_in_for_the_inbox()
    {
        Quiet(2);
        Keep(1);
        _screen.ListDelivered(Incoming, Messages(2, 1));

        Assert.Null(_screen.MessageToOpenDelivered(ToOpen, 2));
    }

    [Fact]
    public void A_message_to_open_named_before_the_inbox_arrived_is_replaced_once_it_has()
    {
        Quiet(2);
        Keep(1);
        _screen.MessageToOpenDelivered(ToOpen, 2);

        _screen.ListDelivered(Inbox, Messages(2, 1));

        Assert.Equal(new ScreenUpdate[] { new MessageToOpenUpdate(ToOpen, 1) }, _screen.Refresh(), UpdateComparer.Instance);
    }

    [Fact]
    public void A_message_to_open_on_a_channel_that_carries_none_is_left_to_the_game()
    {
        Quiet(2);
        Keep(1);
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Assert.Null(_screen.MessageToOpenDelivered(Position, 2));
    }

    [Fact]
    public void The_unread_position_is_reduced_by_the_messages_not_shown_before_it()
    {
        Quiet(5, 4);
        Keep(3, 1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(5, 4, 3, 2, 1));
        _lines.Clear();

        Assert.Equal(0, _screen.PositionDelivered(Position, 2));
        Assert.Equal(1, _screen.PositionDelivered(Position, 4));
        Assert.Equal(new[] { "ANCHOR key=13 2 -> 0", "ANCHOR key=13 4 -> 1" }, _lines);
    }

    [Fact]
    public void An_unread_position_with_nothing_hidden_before_it_is_left_to_the_game()
    {
        Keep(3);
        Quiet(2);
        _screen.ListDelivered(Inbox, Messages(3, 2));

        Assert.Null(_screen.PositionDelivered(Position, 0));
        Assert.Null(_screen.PositionDelivered(Position, 1));
    }

    [Fact]
    public void An_unread_position_beyond_the_inbox_counts_the_whole_inbox()
    {
        Quiet(2);
        Keep(1);
        _screen.ListDelivered(Inbox, Messages(2, 1));

        Assert.Equal(8, _screen.PositionDelivered(Position, 9));
    }

    [Fact]
    public void A_verdict_moves_an_unread_position_already_given()
    {
        Keep(1);
        _visibility.Awaiting(2);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _screen.PositionDelivered(Position, 1);

        Keep(2);

        Assert.Contains(new PositionUpdate(Position, 1), _screen.Refresh(), UpdateComparer.Instance);
    }

    [Fact]
    public void A_newer_inbox_moves_an_unread_position_already_given()
    {
        Quiet(3);
        Keep(2, 1);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _screen.PositionDelivered(Position, 1);

        _screen.ListDelivered(Inbox, Messages(3, 2, 1));

        Assert.Equal(new ScreenUpdate[] { new PositionUpdate(Position, 0) }, _screen.Refresh(), UpdateComparer.Instance);
    }

    [Fact]
    public void Lists_are_given_before_the_position_and_the_message_to_open()
    {
        Quiet(2);
        _visibility.Awaiting(3);
        _visibility.Awaiting(1);
        _screen.ListDelivered(Inbox, Messages(3, 2, 1));
        _screen.MessageToOpenDelivered(ToOpen, 2);
        _screen.PositionDelivered(Position, 2);

        Keep(3, 1);

        Assert.Equal(
            new ScreenUpdate[]
            {
                new ListUpdate(Inbox, new[] { 0, 2 }),
                new PositionUpdate(Position, 1),
                new MessageToOpenUpdate(ToOpen, 3),
            },
            _screen.Refresh(),
            UpdateComparer.Instance);
    }

    [Fact]
    public void Once_stopped_the_screen_is_given_only_what_the_game_gives()
    {
        Quiet(2);
        Keep(1);
        _screen.ListDelivered(Inbox, Messages(2, 1));
        _visibility.Awaiting(3);
        _screen.ListDelivered(Incoming, Messages(3));
        _lines.Clear();

        _screen.Stop("ScreenValues.List");
        _screen.Stop("ScreenWriter.Set");
        Keep(3);

        Assert.True(_screen.Stopped);
        Assert.Empty(_screen.Refresh());
        Assert.Null(_screen.ListDelivered(Inbox, Messages(2, 1)));
        Assert.Null(_screen.MessageToOpenDelivered(ToOpen, 2));
        Assert.Null(_screen.PositionDelivered(Position, 1));
        Assert.Equal(new[] { "NOTE hiding stopped: ScreenValues.List" }, _lines);
    }

    private sealed class ListSink : ITraceSink
    {
        private readonly List<string> _lines;

        public ListSink(List<string> lines) => _lines = lines;

        public void Write(string line) => _lines.Add(line);
    }

    // A list update carries a list, which a record compares by reference.
    private sealed class UpdateComparer : IEqualityComparer<ScreenUpdate>
    {
        public static readonly UpdateComparer Instance = new();

        public bool Equals(ScreenUpdate? x, ScreenUpdate? y) => (x, y) switch
        {
            (ListUpdate a, ListUpdate b) => a.Key == b.Key && a.Positions.SequenceEqual(b.Positions),
            _ => object.Equals(x, y),
        };

        public int GetHashCode(ScreenUpdate update) => update.Key.GetHashCode();
    }
}
