namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Decides whether a message is in the inbox only because a player is shortlisted. Every rule but
/// the last keeps the message: when in doubt, the manager sees it.
/// </summary>
public static class QuietPolicy
{
    public static Verdict Judge(MessageFacts facts, uint? managerTeam)
    {
        if (facts.CanRespond is not { } canRespond
            || facts.Responded is not { } responded
            || facts.Priority is not { } priority
            || facts.Forwarded is not { } forwarded
            || facts.Title is null
            || facts.Shortlisted.Values.Any(answer => answer is null))
        {
            return Verdict.Keep("unreadable");
        }

        if (canRespond)
        {
            return Verdict.Keep("can respond");
        }

        if (responded)
        {
            return Verdict.Keep("responded");
        }

        if (priority)
        {
            return Verdict.Keep("priority");
        }

        if (!forwarded)
        {
            return Verdict.Keep("sent directly");
        }

        if (managerTeam is not { } team)
        {
            return Verdict.Keep("team unknown");
        }

        if (facts.Links.Any(link => link.Record == team))
        {
            return Verdict.Keep("own club");
        }

        return facts.Shortlisted.Values.Any(answer => answer == true)
            ? Verdict.Quiet
            : Verdict.Keep("no shortlisted player");
    }
}
