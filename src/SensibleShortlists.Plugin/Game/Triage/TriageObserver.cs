using FM.UI;
using SensibleShortlists.Core.Triage;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Triage;

/// <summary>
/// Connects the inbox triage to the game. It listens for the channels that carry the inbox and the
/// manager's team, hands what arrives on them to the triage, routes the answers to the plugin's
/// own reads back to whoever asked, and sends the triage's commands on the game's tick.
/// </summary>
internal sealed class TriageObserver : IInteropObserver
{
    private readonly InboxWatch _watch = new();
    private readonly InboxTriage _triage;
    private readonly ChannelReader _reader;
    private readonly GameCommands _commands;

    public TriageObserver(InboxTriage triage, ChannelReader reader, GameCommands commands)
    {
        _triage = triage;
        _reader = reader;
        _commands = commands;
    }

    public void ChannelOpened(InteropReference? reference, PropertyID property, ulong key)
    {
        var role = _watch.ChannelOpened(key, GameDescriber.Property(property));
        if (role == ChannelRole.ManagerTeam
            && reference is not null
            && GameReferences.TryGetRecord(reference, out var team))
        {
            _triage.ManagerTeamSeen(team);
        }
    }

    public void ValuesDelivered(InteropDataHandler handler, ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values)
    {
        for (var i = 0; i < keys.Length && i < values.Count; i++)
        {
            if (!_reader.TryDeliver(keys[i], values[i]) && _watch.CarriesMessages(keys[i]))
            {
                if (GameReferences.ListingFrom(values[i]) is { } listing)
                {
                    _reader.Game = (int)listing.Game;
                    _triage.MessagesListed(listing);
                }
            }
        }
    }

    // Commands first: a read asked for after a command must see what the command changed.
    public void Tick()
    {
        _commands.Flush();
        _reader.Flush();
    }
}
