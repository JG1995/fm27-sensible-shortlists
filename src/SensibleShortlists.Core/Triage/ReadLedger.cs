namespace SensibleShortlists.Core.Triage;

/// <summary>
/// Keeps track of the reads the plugin is waiting on. The game answers a read on some later tick,
/// or never, and a read that is never answered must not leave its message waiting forever with
/// nothing to show for it: after a number of ticks it is given up and answered as unreadable.
/// </summary>
public sealed class ReadLedger
{
    private readonly int _patienceInTicks;
    private readonly Dictionary<ulong, (Action<GameValue?> OnValue, long AskedAt)> _waiting = new();
    private long _tick;

    public ReadLedger(int patienceInTicks) => _patienceInTicks = patienceInTicks;

    public void Expect(ulong key, Action<GameValue?> onValue) => _waiting[key] = (onValue, _tick);

    public bool IsExpected(ulong key) => _waiting.ContainsKey(key);

    /// <summary>Hands an answer to whoever asked. False when nobody is waiting on that key.</summary>
    public bool Answer(ulong key, GameValue? value)
    {
        if (!_waiting.Remove(key, out var read))
        {
            return false;
        }

        read.OnValue(value);
        return true;
    }

    /// <summary>Counts one tick and returns the reads given up on it, each already answered as unreadable.</summary>
    public IReadOnlyList<ulong> Tick()
    {
        _tick++;
        var overdue = _waiting
            .Where(read => _tick - read.Value.AskedAt >= _patienceInTicks)
            .Select(read => read.Key)
            .ToArray();
        foreach (var key in overdue)
        {
            Answer(key, null);
        }

        return overdue;
    }
}
