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
    [InlineData("Off")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Quite")]
    [InlineData("Observe,Quiet")]
    [InlineData("1")]
    public void Anything_else_is_off(string? setting)
    {
        Assert.Equal(TriageMode.Off, TriageModes.Parse(setting));
    }
}
