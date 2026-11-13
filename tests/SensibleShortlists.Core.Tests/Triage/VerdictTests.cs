using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class VerdictTests
{
    [Fact]
    public void A_verdict_nobody_gave_is_not_quiet()
    {
        // A quiet verdict is acted on. A verdict left at its default must never be one.
        var nobodyGaveThis = default(Verdict);

        Assert.False(nobodyGaveThis.IsQuiet);
        Assert.Equal("KEEP(no verdict)", nobodyGaveThis.ToString());
    }

    [Fact]
    public void Quiet_and_keep_say_what_they_are()
    {
        Assert.True(Verdict.Quiet.IsQuiet);
        Assert.Equal("QUIET", Verdict.Quiet.ToString());
        Assert.False(Verdict.Keep("own club").IsQuiet);
        Assert.Equal("KEEP(own club)", Verdict.Keep("own club").ToString());
    }
}
