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
            || facts.Title is null)
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

        // The shortlist only matters once a message names someone who could be on it.
        var people = facts.Links.Where(link => link.IsPerson).ToArray();
        if (people.Length == 0)
        {
            return Verdict.Keep("no shortlisted player");
        }

        if (facts.Shortlist is not { } shortlist)
        {
            return Verdict.Keep("shortlist unreadable");
        }

        return people.Any(person => shortlist.Contains(person.Record))
            ? Verdict.Quiet
            : Verdict.Keep("no shortlisted player");
    }
}
