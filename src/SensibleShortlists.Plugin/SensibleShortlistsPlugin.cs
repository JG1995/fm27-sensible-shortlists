using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Hiding;
using SensibleShortlists.Core.Triage;
using SensibleShortlists.Plugin.Game.Hooks;
using SensibleShortlists.Plugin.Game.Screen;
using SensibleShortlists.Plugin.Game.Trace;
using SensibleShortlists.Plugin.Game.Triage;

namespace SensibleShortlists.Plugin;

/// <summary>Composition root: reads the config and wires the features it enables.</summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class SensibleShortlistsPlugin : BasePlugin
{
    private const string TraceFileName = "SensibleShortlists.trace.log";
    private const string TriageFileName = "SensibleShortlists.triage.log";

    // A message is held back from the screen until it is judged. Every read the verdict waits for
    // has given up by then, so this only ends a wait the triage itself has lost track of.
    private static readonly TimeSpan HeldBackFor = ChannelReader.Patience + TimeSpan.FromSeconds(5);

    private readonly List<FileTraceSink> _files = new();
    private Harmony? _harmony;

    public override void Load()
    {
        var mode = Config.Bind(
            "QuietShortlist",
            "Mode",
            nameof(TriageMode.Off),
            "Off: do nothing. Observe: judge each inbox message and record the verdict to "
            + $"BepInEx/{TriageFileName}, without changing anything in the game. "
            + "Quiet: as Observe, and mark as read each unread message judged to be shortlist noise. "
            + "Hide: as Quiet, and keep those messages out of Messages. They are not deleted: "
            + "with another mode they are listed again.");
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

        var traceNodeGraph = Config.Bind(
            "Diagnostics",
            "TraceNodeGraph",
            false,
            "With TraceInterop on, also record how the UI registers its bindings with the simulation. "
            + "Every binding in the game is written, so the trace grows by megabytes a minute.");

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");

        var observers = new List<IInteropObserver>();
        var tracing = false;
        if (traceInterop.Value && Open(TraceFileName, "Interop trace") is { } traceFile)
        {
            tracing = true;
            observers.Add(new TraceObserver(
                new InteropTraceSession(TraceFilter.Parse(traceFilter.Value), traceFile),
                withNodeGraph: traceNodeGraph.Value));
        }

        var triageMode = TriageModes.Parse(mode.Value);
        if (triageMode != TriageMode.Off && Open(TriageFileName, $"Inbox triage ({triageMode})") is { } triageFile)
        {
            var reader = new ChannelReader(new PropertyCatalog(triageFile), triageFile);
            var commands = new GameCommands();
            var visibility = triageMode == TriageMode.Hide ? new MessageVisibility(HeldBackFor, Clock()) : null;
            observers.Add(new TriageObserver(
                new InboxTriage(reader, commands, triageFile, triageMode, visibility), reader, commands));

            // After the triage's observer: the triage must see the game's list before the screen is given another.
            if (visibility is not null)
            {
                observers.Add(new ScreenObserver(new MessageScreen(visibility, triageFile)));
            }
        }

        if (traceNodeGraph.Value && !tracing)
        {
            Log.LogWarning("TraceNodeGraph is on but the interop trace is not running, so the node graph is not recorded.");
        }

        // With nothing switched on, the game is not hooked at all.
        if (observers.Count == 0)
        {
            return;
        }

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        var (installed, total) = InteropFeed.Start(new HookInstaller(_harmony, Log), Log, observers);
        Log.LogInfo($"Hooked the game's interop layer: {installed} of {total} hooks installed.");
    }

    public override bool Unload()
    {
        // Hooks first: a hook that outlives its file would fail on every game call.
        _harmony?.UnpatchSelf();
        _files.ForEach(file => file.Dispose());
        return true;
    }

    private static Func<TimeSpan> Clock()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        return () => clock.Elapsed;
    }

    private FileTraceSink? Open(string fileName, string feature)
    {
        var path = Path.Combine(Paths.BepInExRootPath, fileName);
        try
        {
            var file = new FileTraceSink(path, () => DateTime.Now);
            _files.Add(file);
            Log.LogInfo($"{feature} started; writing {path}");
            return file;
        }
        catch (Exception exception)
        {
            Log.LogError($"{feature} is off: cannot open {path}: {exception.Message}");
            return null;
        }
    }
}
