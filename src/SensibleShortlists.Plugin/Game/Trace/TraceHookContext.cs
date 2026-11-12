using BepInEx.Logging;
using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>
/// What every trace hook shares. Harmony calls hooks as static methods, so they cannot be handed
/// their session; they read it from here.
/// </summary>
internal static class TraceHookContext
{
    private static readonly FirstFailureGate Failures = new();

    private static ManualLogSource? s_log;

    public static InteropTraceSession? Session { get; private set; }

    public static void Start(InteropTraceSession session, ManualLogSource log)
    {
        Session = session;
        s_log = log;
    }

    /// <summary>
    /// Called from each hook's catch. Nothing may escape a hook into the game: an exception thrown
    /// into IL2CPP code can take the process down.
    /// </summary>
    public static void ReportFirstFailure(string hook, Exception exception)
    {
        if (Failures.ShouldReport(hook))
        {
            s_log?.LogWarning($"Trace hook on {hook} failed and will stay silent from now on: {exception}");
        }
    }
}
