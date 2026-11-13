using System.Runtime.InteropServices;
using FM.GamePlugin;
using Il2CppInterop.Runtime;
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
                    _session.RequestQueued(kind, request.key, request.additionalKey, "value", Describe(values, i));
                    break;

                default:
                    _session.RequestQueued(kind, request.key, request.additionalKey);
                    break;
            }
        }
    }

    // The game queues these two kinds only with a value. So a request with none recorded for its
    // position means the positions were read wrongly, and the line says so.
    private static string? Describe(Dictionary<int, TypedValue?>? values, int position)
    {
        if (values is null)
        {
            return "<unreadable>";
        }

        return values.TryGetValue(position, out var value) ? GameDescriber.Value(value) : "<no entry>";
    }

    // A request that carries more than its two keys has an entry in a second list, which names the
    // request by its position in the queue. An entry is a tuple of a number and three objects. Its
    // fields are read from memory at the offsets that layout gives, because the interop
    // assemblies' own accessor for a number in a generic tuple returned garbage in the first live
    // session (every value came back missing). A failure costs only the values, and is not
    // repeated every frame.
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
                var fields = IL2CPP.il2cpp_object_unbox(carried[i].Pointer);
                var value = Marshal.ReadIntPtr(fields, IntPtr.Size);
                values[Marshal.ReadInt32(fields)] = value == IntPtr.Zero ? null : new TypedValue(value);
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
