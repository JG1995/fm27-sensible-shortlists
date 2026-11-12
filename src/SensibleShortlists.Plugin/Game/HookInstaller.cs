using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace SensibleShortlists.Plugin.Game;

/// <summary>
/// Installs hooks one at a time. Game updates rename and reshape methods and types, and a hook that
/// no longer fits must cost that one capability, not the whole plugin.
/// </summary>
internal sealed class HookInstaller
{
    private readonly Harmony _harmony;
    private readonly ManualLogSource _log;

    public HookInstaller(Harmony harmony, ManualLogSource log)
    {
        _harmony = harmony;
        _log = log;
    }

    /// <param name="name">The game method, for the log.</param>
    /// <param name="resolve">
    /// Finds the game method and the prefix. It runs inside the guard because naming a game type that
    /// a new build no longer has throws as soon as the code that names it is reached.
    /// </param>
    public bool TryInstallPrefix(string name, Func<(MethodBase? Target, MethodInfo Prefix)> resolve)
    {
        try
        {
            var (target, prefix) = resolve();
            if (target is null)
            {
                _log.LogWarning($"Hook skipped: {name} does not exist in this game build.");
                return false;
            }

            _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            return true;
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Hook skipped: {name} could not be hooked: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }
}
