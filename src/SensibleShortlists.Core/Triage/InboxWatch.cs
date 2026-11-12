namespace SensibleShortlists.Core.Triage;

public enum ChannelRole
{
    None,
    MessageList,
    ManagerTeam,
}

/// <summary>
/// Recognises, among all the property channels the game's UI opens, the few that tell the plugin
/// what is in the inbox and which team the manager runs. The plugin learns both by listening to
/// what the UI asks for, so it does not have to know how to ask.
/// </summary>
public sealed class InboxWatch
{
    private static readonly HashSet<string> MessageLists = new(StringComparer.Ordinal)
    {
        "HumanPortalFilteredMessages", "HumanIncomingMessages",
    };

    // The portal asks for this on the manager's own team, and the portal is the first screen of a career.
    private const string ManagerTeamProperty = "CompetitionForPortal";

    private readonly HashSet<ulong> _messageKeys = new();

    /// <summary>The game prints a property as <c>Name:number</c>.</summary>
    public static string NameOf(string? printedProperty)
    {
        var separator = printedProperty?.IndexOf(':') ?? -1;
        return separator < 0 ? printedProperty ?? string.Empty : printedProperty![..separator];
    }

    public ChannelRole ChannelOpened(ulong key, string? printedProperty)
    {
        var name = NameOf(printedProperty);
        if (MessageLists.Contains(name))
        {
            _messageKeys.Add(key);
            return ChannelRole.MessageList;
        }

        // The game reuses keys, so a key that carried messages may now carry something else.
        _messageKeys.Remove(key);
        return name == ManagerTeamProperty ? ChannelRole.ManagerTeam : ChannelRole.None;
    }

    public bool CarriesMessages(ulong key) => _messageKeys.Contains(key);
}
