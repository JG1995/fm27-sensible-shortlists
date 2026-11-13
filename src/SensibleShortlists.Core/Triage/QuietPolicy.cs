namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Decides whether a message is in the inbox only because the manager follows a player at another
/// club. Every rule but the last keeps the message: when in doubt, the manager sees it.
/// </summary>
/// <remarks>
/// No rule asks whether the player is on a shortlist. The game forwards a story about a player
/// because the manager follows that player, and it offers no way to ask which shortlist, if any,
/// a player is on.
/// </remarks>
public static class QuietPolicy
{
    public static Verdict Judge(MessageFacts facts, uint? managerTeam, uint? managerClub)
    {
        if (facts.Responded is not { } responded
            || facts.Priority is not { } priority
            || facts.Forwarded is not { } forwarded
            || facts.Title is null)
        {
            return Verdict.Keep("unreadable");
        }

        if (responded)
        {
            return Verdict.Keep("responded");
        }

        if (priority)
        {
            return Verdict.Keep("priority");
        }

        // The manager's own business arrives directly. Only a forwarded story can be noise.
        if (!forwarded)
        {
            return Verdict.Keep("sent directly");
        }

        if (managerTeam is not { } team)
        {
            return Verdict.Keep("team unknown");
        }

        if (managerClub is not { } club)
        {
            return Verdict.Keep("club unknown");
        }

        if (facts.Links.Any(link => link.Record == team || link.Record == club))
        {
            return Verdict.Keep("own club");
        }

        var people = facts.Links.Where(link => link.IsPerson).ToArray();
        if (people.Length == 0)
        {
            return Verdict.Keep("no player");
        }

        // A person whose clubs were not gathered is as unknown as one the game would not answer for.
        var clubs = people.Select(person => facts.Clubs.GetValueOrDefault(person.Record)).ToArray();
        if (clubs.Any(theirs => theirs is null))
        {
            return Verdict.Keep("player's club unreadable");
        }

        return clubs.Any(theirs => theirs!.Contains(club)) ? Verdict.Keep("own player") : Verdict.Quiet;
    }
}
