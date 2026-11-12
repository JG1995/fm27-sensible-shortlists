using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class EmbeddedTextTests
{
    [Fact]
    public void Text_without_links_is_returned_as_it_is()
    {
        var text = EmbeddedText.Parse("Recruitment Focus Update");

        Assert.Equal("Recruitment Focus Update", text.Plain);
        Assert.Empty(text.Links);
    }

    [Fact]
    public void A_link_is_replaced_by_its_visible_text_and_yields_its_record()
    {
        var text = EmbeddedText.Parse(
            $"{GameText.Link(GameText.Brighton, "Brighton")} make offer for {GameText.Link(GameText.Player, "King")}");

        Assert.Equal("Brighton make offer for King", text.Plain);
        Assert.Equal(new[] { new RecordLink(GameText.Brighton), new RecordLink(GameText.Player) }, text.Links);
    }

    [Fact]
    public void A_record_splits_into_kind_and_number()
    {
        var link = new RecordLink(GameText.Brighton);

        Assert.Equal(24u, link.Kind);
        Assert.Equal(504u, link.Number);
        Assert.False(link.IsPerson);
        Assert.True(new RecordLink(GameText.Player).IsPerson);
    }

    [Theory]
    [InlineData("\u0001not base64 at all!\u0002Brighton\u0001 sign a player")]
    [InlineData("\u0001QUJDRA==\u0002Brighton\u0001 sign a player")]
    public void A_link_that_cannot_be_decoded_keeps_its_text_and_yields_no_record(string raw)
    {
        var text = EmbeddedText.Parse(raw);

        Assert.Equal("Brighton sign a player", text.Plain);
        Assert.Empty(text.Links);
    }

    [Fact]
    public void A_stray_link_edge_does_not_swallow_the_link_after_it()
    {
        var text = EmbeddedText.Parse($"Odd \u0001 text {GameText.Link(GameText.Player, "King")} signs");

        Assert.Equal("Odd \u0001 text King signs", text.Plain);
        Assert.Equal(new[] { new RecordLink(GameText.Player) }, text.Links);
    }

    [Fact]
    public void An_unfinished_link_is_left_in_the_text()
    {
        var text = EmbeddedText.Parse("Before \u0001abc");

        Assert.Equal("Before \u0001abc", text.Plain);
        Assert.Empty(text.Links);
    }
}
