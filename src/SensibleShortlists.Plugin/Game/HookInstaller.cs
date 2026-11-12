using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace SensibleShortlists.Plugin.Game;

/// <summary>
/// Installs hooks one at a time. Game updates rename and reshape methods, and a hook that no longer
/// fits must cost that one capability, not the whole plugin.
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

    public bool TryInstallPrefix(Type targetType, string methodName, Type[] parameterTypes, MethodInfo prefix)
    {
        var name = $"{targetType.Name}.{methodName}";
        try
        {
            var target = AccessTools.Method(targetType, methodName, parameterTypes);
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
