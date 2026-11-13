namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Keeps track of the reads the plugin is waiting on. The game answers a read a moment later, or
/// never, and a read that is never answered must not leave its message waiting forever with
/// nothing to show for it: after a while it is given up and answered as unreadable.
/// </summary>
/// <remarks>
/// Patience is measured in time, not in the game's ticks, because the tick rate follows the frame
/// rate. Reads are asked in order and share one patience, so the oldest waiting read is always at
/// the front of the queue and checking for overdue reads costs nothing while none is overdue.
/// </remarks>
public sealed class ReadLedger
{
    private readonly TimeSpan _patience;
    private readonly Func<TimeSpan> _now;
    private readonly Dictionary<ulong, Action<GameValue?>> _waiting = new();
    private readonly Queue<(ulong Key, TimeSpan AskedAt)> _asked = new();

    public ReadLedger(TimeSpan patience, Func<TimeSpan> now)
    {
        _patience = patience;
        _now = now;
    }

    public void Expect(ulong key, Action<GameValue?> onValue)
    {
        // Add throws on a key that is already waiting: a second waiter would silently replace the first.
        _waiting.Add(key, onValue);
        _asked.Enqueue((key, _now()));
    }

    public bool IsExpected(ulong key) => _waiting.ContainsKey(key);

    /// <summary>Hands an answer to whoever asked. False when nobody is waiting on that key.</summary>
    public bool Answer(ulong key, GameValue? value)
    {
        if (!_waiting.Remove(key, out var onValue))
        {
            return false;
        }

        onValue(value);
        return true;
    }

    /// <summary>Returns the reads whose patience has run out, each already answered as unreadable.</summary>
    public IReadOnlyList<ulong> GiveUpOverdue()
    {
        List<ulong>? givenUp = null;
        var now = _now();
        while (_asked.TryPeek(out var oldest) && now - oldest.AskedAt >= _patience)
        {
            _asked.Dequeue();
            if (Answer(oldest.Key, null))
            {
                (givenUp ??= new List<ulong>()).Add(oldest.Key);
            }
        }

        return givenUp ?? (IReadOnlyList<ulong>)Array.Empty<ulong>();
    }
}
