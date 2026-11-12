using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Tests.Diagnostics;

public class FirstFailureGateTests
{
    [Fact]
    public void The_first_failure_of_a_hook_is_reported()
    {
        var gate = new FirstFailureGate();

        Assert.True(gate.ShouldReport("SendEvent"));
    }

    [Fact]
    public void Later_failures_of_the_same_hook_are_silent()
    {
        var gate = new FirstFailureGate();
        gate.ShouldReport("SendEvent");

        Assert.False(gate.ShouldReport("SendEvent"));
    }

    [Fact]
    public void Each_hook_is_reported_once_on_its_own()
    {
        var gate = new FirstFailureGate();
        gate.ShouldReport("SendEvent");

        Assert.True(gate.ShouldReport("OpenChannel"));
    }
}
