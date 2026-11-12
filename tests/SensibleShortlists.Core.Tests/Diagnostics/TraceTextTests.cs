using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Tests.Diagnostics;

public class TraceTextTests
{
    [Fact]
    public void Null_is_written_as_a_marker()
    {
        Assert.Equal("<null>", TraceText.Sanitize(null));
    }

    [Fact]
    public void Line_breaks_and_tabs_are_escaped_so_a_record_stays_on_one_line()
    {
        Assert.Equal(@"a\nb\rc\td", TraceText.Sanitize("a\nb\rc\td"));
    }

    [Fact]
    public void Text_at_the_limit_is_unchanged()
    {
        var text = new string('x', TraceText.MaxLength);

        Assert.Equal(text, TraceText.Sanitize(text));
    }

    [Fact]
    public void Text_over_the_limit_is_cut_and_marked_with_the_number_of_removed_characters()
    {
        var text = new string('x', TraceText.MaxLength + 25);

        var sanitized = TraceText.Sanitize(text);

        Assert.Equal(new string('x', TraceText.MaxLength) + "…(+25)", sanitized);
    }

    [Fact]
    public void Describe_returns_the_description()
    {
        Assert.Equal("Int32:4", TraceText.Describe(4, number => $"Int32:{number}"));
    }

    [Fact]
    public void Describe_turns_a_failure_into_a_marker_naming_the_exception()
    {
        var description = TraceText.Describe<object?>(null, _ => throw new InvalidOperationException("boom"));

        Assert.Equal("<error: InvalidOperationException>", description);
    }
}
