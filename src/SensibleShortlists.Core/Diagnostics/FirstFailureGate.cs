using System.Collections.Concurrent;

namespace SensibleShortlists.Core.Diagnostics;

/// <summary>
/// Lets each hook report its first failure only. A hook on a hot path that fails once usually
/// fails on every call, and that would bury the log.
/// </summary>
public sealed class FirstFailureGate
{
    private readonly ConcurrentDictionary<string, bool> _reported = new();

    public bool ShouldReport(string hook) => _reported.TryAdd(hook, true);
}
