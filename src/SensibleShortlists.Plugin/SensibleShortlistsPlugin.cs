using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Plugin.Game;

namespace SensibleShortlists.Plugin;

/// <summary>Composition root: reads the config and wires the features it enables.</summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class SensibleShortlistsPlugin : BasePlugin
{
    private const string TraceFileName = "SensibleShortlists.trace.log";

    private FileTraceSink? _traceSink;
    private Harmony? _harmony;

    public override void Load()
    {
        var traceInterop = Config.Bind(
            "Diagnostics",
            "TraceInterop",
            false,
            $"Record the traffic between the game's UI and its simulation core to BepInEx/{TraceFileName}. "
            + "For developing and repairing the plugin; leave off for normal play.");
        var traceFilter = Config.Bind(
            "Diagnostics",
            "TraceFilter",
            "shortlist,message,news,portal",
            "Comma-separated terms. A property channel is traced when its label contains any of them, "
            + "ignoring case. Empty traces every channel.");

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");

        if (traceInterop.Value)
        {
            StartTrace(traceFilter.Value);
        }
    }

    public override bool Unload()
    {
        // Hooks first: a hook that outlives the sink would fail on every game call.
        _harmony?.UnpatchSelf();
        _traceSink?.Dispose();
        return true;
    }

    private void StartTrace(string filter)
    {
        var path = Path.Combine(Paths.BepInExRootPath, TraceFileName);
        try
        {
            _traceSink = new FileTraceSink(path, () => DateTime.Now);
        }
        catch (Exception exception)
        {
            Log.LogError($"Interop trace is off: cannot open {path}: {exception.Message}");
            return;
        }

        var session = new InteropTraceSession(TraceFilter.Parse(filter), _traceSink);
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        var (installed, total) = InteropTraceHooks.Install(new HookInstaller(_harmony, Log), session, Log);
        Log.LogInfo($"Interop trace started with {installed} of {total} hooks; writing {path}");
    }
}
