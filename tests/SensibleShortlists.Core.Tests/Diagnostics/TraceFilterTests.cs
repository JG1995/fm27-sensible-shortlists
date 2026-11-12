using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Tests.Diagnostics;

public class TraceFilterTests
{
    [Theory]
    [InlineData("PersonReference[1:2].HumanShortlists")]
    [InlineData("personreference[1:2].humanSHORTLISTs")]
    [InlineData("NewsItemReference[7:9].NewsItemId")]
    public void Matches_a_label_that_contains_a_term_ignoring_case(string label)
    {
        var filter = TraceFilter.Parse("shortlist,news");

        Assert.True(filter.Matches(label));
    }

    [Fact]
    public void Does_not_match_a_label_that_contains_no_term()
    {
        var filter = TraceFilter.Parse("shortlist,news");

        Assert.False(filter.Matches("TeamReference[3:4].TrainingSchedule"));
    }

    [Fact]
    public void Trims_whitespace_around_terms()
    {
        var filter = TraceFilter.Parse("  shortlist ,  portal  ");

        // Untrimmed, the term "  portal  " would not be contained in the label.
        Assert.True(filter.Matches("x.HumanPortalMessage"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ,, ")]
    public void A_filter_without_terms_matches_every_label(string? terms)
    {
        var filter = TraceFilter.Parse(terms);

        Assert.True(filter.Matches("TeamReference[3:4].TrainingSchedule"));
    }
}
