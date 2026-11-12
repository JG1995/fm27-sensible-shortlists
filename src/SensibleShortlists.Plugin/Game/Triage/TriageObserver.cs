using SensibleShortlists.Core.Triage;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Triage;

/// <summary>
/// Connects the inbox triage to the game. It listens for the channels that carry the inbox and the
/// manager's team, hands what arrives on them to the triage, and routes the answers to the
/// plugin's own reads back to whoever asked.
/// </summary>
internal sealed class TriageObserver : IInteropObserver
{
    private readonly InboxWatch _watch = new();
    private readonly Dictionary<uint, string> _printedProperties = new();
    private readonly InboxTriage _triage;
    private readonly ChannelReader _reader;

    public TriageObserver(InboxTriage triage, ChannelReader reader)
    {
        _triage = triage;
        _reader = reader;
    }

    public void ChannelOpened(InteropReference? reference, PropertyID property, ulong key)
    {
        var role = _watch.ChannelOpened(key, Printed(property));
        if (role == ChannelRole.ManagerTeam
            && reference is not null
            && GameReferences.TryGetRecord(reference, out var team))
        {
            _triage.ManagerTeamSeen(team);
        }
    }

    public void ValuesDelivered(ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values)
    {
        for (var i = 0; i < keys.Length && i < values.Count; i++)
        {
            if (!_reader.TryDeliver(keys[i], values[i]) && _watch.CarriesMessages(keys[i]))
            {
                var (game, messages) = GameReferences.Messages(values[i]);
                if (messages.Length > 0)
                {
                    _reader.Game = game;
                    _triage.MessagesListed(game, messages);
                }
            }
        }
    }

    public void Tick() => _reader.Flush();

    // Printing a property is a call into the game, and channels open by the thousand.
    private string Printed(PropertyID property)
    {
        if (!_printedProperties.TryGetValue(property.id, out var printed))
        {
            printed = property.ToString();
            _printedProperties[property.id] = printed;
        }

        return printed;
    }
}
