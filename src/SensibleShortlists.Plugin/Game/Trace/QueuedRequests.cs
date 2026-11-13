using FM.GamePlugin;
using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Plugin.Game.Hooks;
using SI.Bindable.Reference.Core;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>
/// Records the requests the UI has queued for the simulation: nodes added and removed, nodes
/// pointed at data, and the data itself. The game sends the queue once a frame and then empties it.
/// </summary>
/// <remarks>
/// The queue is read, not hooked. Hooking the methods that fill it crashed the game: one of them,
/// <c>SetData</c>, begins with a near conditional jump, and BepInEx's detour library moves such a
/// jump without correcting where it lands. Reading needs no new hook at all.
/// </remarks>
internal sealed class QueuedRequests
{
    private readonly InteropTraceSession _session;
    private bool _valuesReadable = true;

    public QueuedRequests(InteropTraceSession session) => _session = session;

    public void Record()
    {
        if (!GameSubsystems.TryGet<GameInteropSubsystem>(out var interop) || interop.m_batchedRequests is not { } requests)
        {
            return;
        }

        var count = requests.Count;
        if (count == 0)
        {
            return;
        }

        var values = ValuesByPosition(interop);
        for (var i = 0; i < count; i++)
        {
            var request = requests[i];
            var kind = request.mode.ToString();
            switch (request.mode)
            {
                case ChannelActionMode.SetPropertyID:
                    var property = new PropertyID { id = (uint)request.additionalKey };
                    _session.RequestQueued(kind, request.key, request.additionalKey, "property", GameDescriber.Property(property));
                    break;

                case ChannelActionMode.SetData:
                case ChannelActionMode.UpdateChannelData:
                    var value = values is null ? "<unreadable>" : GameDescriber.Value(values.GetValueOrDefault(i));
                    _session.RequestQueued(kind, request.key, request.additionalKey, "value", value);
                    break;

                default:
                    _session.RequestQueued(kind, request.key, request.additionalKey);
                    break;
            }
        }
    }

    // A request that carries more than its two keys has an entry in a second list, which names the
    // request by its position in the queue. The game's code is seen to build that entry, but
    // reading one from here is less certain than reading the queue. So a failure costs only the
    // values, and is not repeated every frame.
    private Dictionary<int, TypedValue?>? ValuesByPosition(GameInteropSubsystem interop)
    {
        if (!_valuesReadable)
        {
            return null;
        }

        try
        {
            var values = new Dictionary<int, TypedValue?>();
            var carried = interop.m_batchedRequestInterrupts;
            for (var i = 0; carried is not null && i < carried.Count; i++)
            {
                var entry = carried[i];
                values[entry.Item1] = entry.Item2;
            }

            return values;
        }
        catch (Exception exception)
        {
            _valuesReadable = false;
            InteropFeed.ReportFirstFailure($"{nameof(QueuedRequests)}.{nameof(ValuesByPosition)}", exception);
            return null;
        }
    }
}
