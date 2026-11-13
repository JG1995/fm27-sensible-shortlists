using SensibleShortlists.Core.Hiding;
using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Hiding;

public class MessageVisibilityTests
{
    private readonly MessageVisibility _visibility;
    private TimeSpan _now = TimeSpan.Zero;

    public MessageVisibilityTests() => _visibility = new MessageVisibility(TimeSpan.FromSeconds(20), () => _now);

    private void Wait(double seconds) => _now += TimeSpan.FromSeconds(seconds);

    [Fact]
    public void A_message_nobody_has_mentioned_is_shown()
    {
        Assert.True(_visibility.IsShown(1));
    }

    [Fact]
    public void A_message_awaiting_its_verdict_is_held_back()
    {
        _visibility.Awaiting(1);

        Assert.False(_visibility.IsShown(1));
    }

    [Fact]
    public void A_message_judged_quiet_is_hidden()
    {
        _visibility.Awaiting(1);

        _visibility.Judged(1, Verdict.Quiet);

        Assert.False(_visibility.IsShown(1));
    }

    [Fact]
    public void A_message_judged_keep_is_shown()
    {
        _visibility.Awaiting(1);

        _visibility.Judged(1, Verdict.Keep("sent directly"));

        Assert.True(_visibility.IsShown(1));
    }

    [Fact]
    public void A_verdict_left_at_its_default_shows_the_message()
    {
        _visibility.Awaiting(1);

        _visibility.Judged(1, default);

        Assert.True(_visibility.IsShown(1));
    }

    [Fact]
    public void A_message_without_a_verdict_is_released_when_patience_runs_out()
    {
        _visibility.Awaiting(1);

        Wait(19.9);
        Assert.Empty(_visibility.ReleaseOverdue());
        Assert.False(_visibility.IsShown(1));

        Wait(0.1);
        Assert.Equal(new long[] { 1 }, _visibility.ReleaseOverdue());
        Assert.True(_visibility.IsShown(1));
    }

    [Fact]
    public void Each_message_has_its_own_patience()
    {
        _visibility.Awaiting(1);
        Wait(5);
        _visibility.Awaiting(2);
        Wait(15);

        Assert.Equal(new long[] { 1 }, _visibility.ReleaseOverdue());
        Wait(5);
        Assert.Equal(new long[] { 2 }, _visibility.ReleaseOverdue());
    }

    [Fact]
    public void A_judged_message_is_never_released()
    {
        _visibility.Awaiting(1);
        _visibility.Judged(1, Verdict.Quiet);
        Wait(60);

        Assert.Empty(_visibility.ReleaseOverdue());
        Assert.False(_visibility.IsShown(1));
    }

    [Fact]
    public void A_quiet_verdict_that_arrives_after_the_release_still_hides_the_message()
    {
        _visibility.Awaiting(1);
        Wait(20);
        _visibility.ReleaseOverdue();

        _visibility.Judged(1, Verdict.Quiet);

        Assert.False(_visibility.IsShown(1));
    }

    [Fact]
    public void Forgetting_shows_everything_again_and_releases_nothing_later()
    {
        _visibility.Awaiting(1);
        _visibility.Awaiting(2);
        _visibility.Judged(2, Verdict.Quiet);

        _visibility.Forget();
        Wait(60);

        Assert.True(_visibility.IsShown(1));
        Assert.True(_visibility.IsShown(2));
        Assert.Empty(_visibility.ReleaseOverdue());
    }

    [Fact]
    public void A_message_awaited_again_after_forgetting_has_fresh_patience()
    {
        _visibility.Awaiting(1);
        Wait(15);
        _visibility.Forget();
        _visibility.Awaiting(1);
        Wait(10);

        Assert.Empty(_visibility.ReleaseOverdue());
        Assert.False(_visibility.IsShown(1));
    }

    [Fact]
    public void Only_forgetting_starts_another_career()
    {
        var career = _visibility.Career;

        _visibility.Awaiting(1);
        _visibility.Judged(1, Verdict.Quiet);
        Assert.Equal(career, _visibility.Career);

        _visibility.Forget();
        Assert.NotEqual(career, _visibility.Career);
    }

    [Fact]
    public void The_version_moves_only_when_what_is_shown_may_have_changed()
    {
        var start = _visibility.Version;

        _visibility.ReleaseOverdue();
        Assert.Equal(start, _visibility.Version);

        _visibility.Awaiting(1);
        var awaited = _visibility.Version;
        Assert.NotEqual(start, awaited);

        _visibility.Judged(1, Verdict.Quiet);
        var judged = _visibility.Version;
        Assert.NotEqual(awaited, judged);

        _visibility.Awaiting(2);
        Wait(20);
        var beforeRelease = _visibility.Version;
        _visibility.ReleaseOverdue();
        Assert.NotEqual(beforeRelease, _visibility.Version);

        var beforeForget = _visibility.Version;
        _visibility.Forget();
        Assert.NotEqual(beforeForget, _visibility.Version);
    }
}
