using BepInEx.Logging;
using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Plugin.Game.Trace;

/// <summary>
/// The hooks that feed the interop trace. They only read: each turns its arguments into plain
/// values, hands them to the session, and lets the game call proceed unchanged.
/// </summary>
internal static class InteropTraceHooks
{
    public static (int Installed, int Total) Install(
        HookInstaller hooks,
        InteropTraceSession session,
        ManualLogSource log)
    {
        TraceHookContext.Start(session, log);

        var installed = new[]
        {
            hooks.TryInstallPrefix(SendEventHook.Name, SendEventHook.Resolve),
            hooks.TryInstallPrefix(OpenChannelHook.Name, OpenChannelHook.Resolve),
            hooks.TryInstallPrefix(SetValueHooks.SetInternalName, SetValueHooks.ResolveSetInternal),
            hooks.TryInstallPrefix(SetValueHooks.SetPrefetchedName, SetValueHooks.ResolveSetPrefetched),
        };

        return (installed.Count(wasInstalled => wasInstalled), installed.Length);
    }
}
