using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class TriageModeTests
{
    [Theory]
    [InlineData("Observe")]
    [InlineData(" observe ")]
    public void Observe_is_recognised_whatever_its_case_or_spacing(string setting)
    {
        Assert.Equal(TriageMode.Observe, TriageModes.Parse(setting));
    }

    [Theory]
    [InlineData("Quiet")]
    [InlineData(" quiet ")]
    public void Quiet_is_recognised_whatever_its_case_or_spacing(string setting)
    {
        Assert.Equal(TriageMode.Quiet, TriageModes.Parse(setting));
    }

    [Theory]
    [InlineData("Hide")]
    [InlineData(" hide ")]
    public void Hide_is_recognised_whatever_its_case_or_spacing(string setting)
    {
        Assert.Equal(TriageMode.Hide, TriageModes.Parse(setting));
    }

    [Theory]
    [InlineData(TriageMode.Off, false)]
    [InlineData(TriageMode.Observe, false)]
    [InlineData(TriageMode.Quiet, true)]
    [InlineData(TriageMode.Hide, true)]
    public void Quiet_and_hide_mark_quiet_messages_read(TriageMode mode, bool marks)
    {
        Assert.Equal(marks, mode.MarksRead());
    }

    [Theory]
    [InlineData(TriageMode.Off, false)]
    [InlineData(TriageMode.Observe, false)]
    [InlineData(TriageMode.Quiet, false)]
    [InlineData(TriageMode.Hide, true)]
    public void Only_hide_changes_what_the_screen_is_given(TriageMode mode, bool hides)
    {
        Assert.Equal(hides, mode.Hides());
    }

    [Theory]
    [InlineData("Off")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Quite")]
    [InlineData("Hidden")]
    [InlineData("Observe,Quiet")]
    [InlineData("1")]
    public void Anything_else_is_off(string? setting)
    {
        Assert.Equal(TriageMode.Off, TriageModes.Parse(setting));
    }
}
