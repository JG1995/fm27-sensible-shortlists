using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Hiding;

/// <summary>
/// Knows which inbox messages the screen may show. A message judged quiet is hidden. A message
/// still awaiting its verdict is held back, so that a quiet one never flashes up, but only for so
/// long: a message the triage fails to judge must not stay out of sight.
/// </summary>
/// <remarks>A message nobody has mentioned is shown. Hiding takes a reason; showing does not.</remarks>
public sealed class MessageVisibility : IVerdictListener
{
    private readonly TimeSpan _patience;
    private readonly Func<TimeSpan> _now;
    private readonly HashSet<long> _hidden = new();
    private readonly HashSet<long> _heldBack = new();

    // In the order they were first listed, so the oldest is always at the front.
    private readonly Queue<(long Message, TimeSpan Since)> _waits = new();

    public MessageVisibility(TimeSpan patience, Func<TimeSpan> now)
    {
        _patience = patience;
        _now = now;
    }

    /// <summary>Moves whenever what is shown may have changed.</summary>
    public int Version { get; private set; }

    public bool IsShown(long message) => !_hidden.Contains(message) && !_heldBack.Contains(message);

    public void Awaiting(long message)
    {
        if (_heldBack.Add(message))
        {
            _waits.Enqueue((message, _now()));
            Version++;
        }
    }

    public void Judged(long message, Verdict verdict)
    {
        _heldBack.Remove(message);
        if (verdict.IsQuiet)
        {
            _hidden.Add(message);
        }

        Version++;
    }

    public void Forget()
    {
        _hidden.Clear();
        _heldBack.Clear();
        _waits.Clear();
        Version++;
    }

    /// <summary>Stops holding back the messages that have waited too long, and returns them.</summary>
    public IReadOnlyList<long> ReleaseOverdue()
    {
        List<long>? released = null;
        var now = _now();
        while (_waits.TryPeek(out var oldest) && now - oldest.Since >= _patience)
        {
            _waits.Dequeue();
            if (_heldBack.Remove(oldest.Message))
            {
                (released ??= new List<long>()).Add(oldest.Message);
            }
        }

        if (released is null)
        {
            return Array.Empty<long>();
        }

        Version++;
        return released;
    }
}
