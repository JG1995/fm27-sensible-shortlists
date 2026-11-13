using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Tests.Diagnostics;

public class InteropTraceSessionTests
{
    private readonly RecordingSink _sink = new();

    private InteropTraceSession Session(string filter = "shortlist", int capacity = 100) =>
        new(TraceFilter.Parse(filter), _sink, capacity);

    [Fact]
    public void An_event_is_recorded_with_name_number_target_and_data()
    {
        Session().EventSent("EventFmxMarkPortalMessageAsRead", 75, "PersonReference[1:2]", "Int32:4");

        Assert.Equal(
            new[] { "EVENT EventFmxMarkPortalMessageAsRead(75) target=PersonReference[1:2] data=Int32:4" },
            _sink.Lines);
    }

    [Fact]
    public void An_event_is_recorded_even_when_it_matches_no_filter_term()
    {
        Session(filter: "shortlist").EventSent("EventFmxContinue", 1, "<null>", "<null>");

        Assert.Single(_sink.Lines);
    }

    [Fact]
    public void A_channel_whose_label_matches_the_filter_is_recorded()
    {
        Session().ChannelOpened(42, "PersonReference[1:2]", "HumanShortlists");

        Assert.Equal(new[] { "CHANNEL key=42 PersonReference[1:2].HumanShortlists" }, _sink.Lines);
    }

    [Fact]
    public void A_channel_whose_label_matches_no_term_is_not_recorded()
    {
        Session().ChannelOpened(42, "TeamReference[3:4]", "TrainingSchedule");

        Assert.Empty(_sink.Lines);
    }

    [Fact]
    public void A_missing_reference_or_property_is_marked_in_the_label()
    {
        Session(filter: "").ChannelOpened(42, null, null);

        Assert.Equal(new[] { "CHANNEL key=42 <null>.<null>" }, _sink.Lines);
    }

    [Fact]
    public void A_value_is_described_outside_the_sessions_lock()
    {
        var session = Session();
        session.ChannelOpened(42, "PersonReference[1:2]", "HumanShortlists");
        _sink.Lines.Clear();

        // Describing calls into the game, which may re-enter the session from another thread.
        session.ValueChanged(42, session, inner =>
        {
            var other = Task.Run(() => inner.EventSent("Nested", 1, null, null));
            return other.Wait(TimeSpan.FromSeconds(5)) ? "described" : "deadlocked";
        });

        Assert.Equal("VALUE key=42 PersonReference[1:2].HumanShortlists = described", _sink.Lines.Last());
    }

    [Fact]
    public void A_value_for_a_traced_channel_is_recorded_with_the_channels_label()
    {
        var session = Session();
        session.ChannelOpened(42, "PersonReference[1:2]", "HumanShortlists");
        _sink.Lines.Clear();

        session.ValueChanged(42, 3, count => $"List`1:{count} items");

        Assert.Equal(
            new[] { "VALUE key=42 PersonReference[1:2].HumanShortlists = List`1:3 items" },
            _sink.Lines);
    }

    [Fact]
    public void A_value_for_an_untraced_channel_is_neither_recorded_nor_inspected()
    {
        var session = Session();
        session.ChannelOpened(42, "TeamReference[3:4]", "TrainingSchedule");
        var inspected = false;

        session.ValueChanged(42, "anything", value =>
        {
            inspected = true;
            return value;
        });

        Assert.Empty(_sink.Lines);
        Assert.False(inspected);
    }

    [Fact]
    public void Reopening_a_key_replaces_its_label()
    {
        var session = Session();
        session.ChannelOpened(42, "PersonReference[1:2]", "HumanShortlists");
        session.ChannelOpened(42, "PersonReference[1:2]", "HumanShortlistsCount");
        _sink.Lines.Clear();

        session.ValueChanged(42, "Int32:3", value => value);

        Assert.Equal(
            new[] { "VALUE key=42 PersonReference[1:2].HumanShortlistsCount = Int32:3" },
            _sink.Lines);
    }

    [Fact]
    public void Reopening_a_traced_key_for_a_channel_that_matches_no_term_stops_tracing_it()
    {
        var session = Session();
        session.ChannelOpened(42, "PersonReference[1:2]", "HumanShortlists");
        session.ChannelOpened(42, "TeamReference[3:4]", "TrainingSchedule");
        _sink.Lines.Clear();
        var inspected = false;

        session.ValueChanged(42, "anything", value =>
        {
            inspected = true;
            return value;
        });

        Assert.Empty(_sink.Lines);
        Assert.False(inspected);
    }

    [Fact]
    public void Reaching_the_capacity_forgets_every_traced_channel_and_notes_it()
    {
        var session = Session(capacity: 2);
        session.ChannelOpened(1, "A", "Shortlist1");
        session.ChannelOpened(2, "A", "Shortlist2");
        _sink.Lines.Clear();

        session.ChannelOpened(3, "A", "Shortlist3");
        session.ValueChanged(1, "old", value => value);
        session.ValueChanged(3, "new", value => value);

        Assert.Equal(
            new[]
            {
                "NOTE channel capacity 2 reached; forgot all traced channels",
                "CHANNEL key=3 A.Shortlist3",
                "VALUE key=3 A.Shortlist3 = new",
            },
            _sink.Lines);
    }

    [Fact]
    public void Reopening_a_traced_key_at_capacity_does_not_forget_the_others()
    {
        var session = Session(capacity: 2);
        session.ChannelOpened(1, "A", "Shortlist1");
        session.ChannelOpened(2, "A", "Shortlist2");
        _sink.Lines.Clear();

        session.ChannelOpened(2, "A", "Shortlist2b");
        session.ValueChanged(1, "kept", value => value);

        Assert.Equal(
            new[] { "CHANNEL key=2 A.Shortlist2b", "VALUE key=1 A.Shortlist1 = kept" },
            _sink.Lines);
    }

    [Fact]
    public void A_value_that_cannot_be_described_is_still_recorded()
    {
        var session = Session();
        session.ChannelOpened(42, "PersonReference[1:2]", "HumanShortlists");
        _sink.Lines.Clear();

        session.ValueChanged<object?>(42, null, _ => throw new InvalidOperationException("boom"));

        Assert.Equal(
            new[] { "VALUE key=42 PersonReference[1:2].HumanShortlists = <error: InvalidOperationException>" },
            _sink.Lines);
    }

    [Fact]
    public void A_batch_pairs_each_key_with_the_value_at_the_same_position()
    {
        var session = Session();
        session.ChannelOpened(1, "A", "Shortlist1");
        session.ChannelOpened(3, "A", "Shortlist3");
        _sink.Lines.Clear();
        var inspected = new List<string>();

        session.ValuesDelivered(new ulong[] { 1, 2, 3 }, new[] { "one", "two", "three" }, value =>
        {
            inspected.Add(value);
            return value;
        });

        Assert.Equal(new[] { "VALUE key=1 A.Shortlist1 = one", "VALUE key=3 A.Shortlist3 = three" }, _sink.Lines);
        Assert.Equal(new[] { "one", "three" }, inspected);
    }

    [Theory]
    [InlineData(new ulong[] { 1, 2 }, new[] { "one" })]
    [InlineData(new ulong[] { 1 }, new[] { "one", "two" })]
    public void A_batch_with_unequal_counts_is_noted_and_paired_as_far_as_it_goes(ulong[] keys, string[] values)
    {
        var session = Session();
        session.ChannelOpened(1, "A", "Shortlist1");
        _sink.Lines.Clear();

        session.ValuesDelivered(keys, values, value => value);

        Assert.Equal(
            new[]
            {
                $"NOTE value batch has {keys.Length} keys and {values.Length} values; paired by position",
                "VALUE key=1 A.Shortlist1 = one",
            },
            _sink.Lines);
    }

    [Fact]
    public void A_batch_with_unequal_counts_and_no_traced_key_is_not_noted()
    {
        var session = Session();

        session.ValuesDelivered(new ulong[] { 7, 8 }, new[] { "one" }, value => value);

        Assert.Empty(_sink.Lines);
    }

    [Fact]
    public void A_node_is_recorded_with_its_parent_and_property_whatever_the_filter()
    {
        Session(filter: "shortlist").NodeAdded(7, 3, "TrainingSchedule:99");

        Assert.Equal(new[] { "NODE key=7 parent=3 property=TrainingSchedule:99" }, _sink.Lines);
    }

    [Fact]
    public void The_rest_of_the_node_graph_traffic_is_recorded_one_line_each()
    {
        var session = Session();

        session.NodeRemoved(7);
        session.TargetSet(7, 12);
        session.DataAdded(12);
        session.DataSet(12, 4, count => $"List`1:{count} items");
        session.DataReleased(12);

        Assert.Equal(
            new[]
            {
                "UNNODE key=7",
                "TARGET key=7 data=12",
                "DATA key=12",
                "DATA key=12 = List`1:4 items",
                "UNDATA key=12",
            },
            _sink.Lines);
    }

    [Fact]
    public void Data_that_cannot_be_described_is_still_recorded()
    {
        Session().DataSet<object?>(12, null, _ => throw new InvalidOperationException("boom"));

        Assert.Equal(new[] { "DATA key=12 = <error: InvalidOperationException>" }, _sink.Lines);
    }

    [Fact]
    public void Recorded_text_is_sanitized()
    {
        Session().EventSent("Name", 1, "target\nwith break", null);

        Assert.Equal(new[] { @"EVENT Name(1) target=target\nwith break data=<null>" }, _sink.Lines);
    }

    private sealed class RecordingSink : ITraceSink
    {
        public List<string> Lines { get; } = new();

        public void Write(string line) => Lines.Add(line);
    }
}
