using FM.UI;
using SI.Bindable;
using SI.Core;

namespace SensibleShortlists.Plugin.Game.Screen;

/// <summary>
/// Gives the screen a value outside a delivery, by setting it on the game's binding tree the way
/// the game's own handler does for a value from the simulation. Nothing is sent to the simulation.
/// </summary>
internal sealed class ScreenWriter
{
    // What the game's handler passes for a value that came from the simulation. The other flag,
    // UpdateHandler, would send the value back to the simulation as if the manager had entered it.
    private const Bindings.SetFlags FromSimulation = 0;

    private IntPtr _handler;
    private Bindings? _tree;

    /// <summary>Takes the binding tree from the handler that feeds it, so it is the tree the screen is bound to.</summary>
    public void Seen(InteropDataHandler handler)
    {
        if (handler.Pointer != _handler)
        {
            _tree = handler.m_bindingTree.m_bindings;
            _handler = handler.Pointer;
        }
    }

    /// <summary>The game's handler sets a batch of values between these two calls, and so does the plugin.</summary>
    public void Begin() => Tree.BeginBatchSet();

    public void End() => Tree.EndBatchSet();

    /// <summary>Sets a value on a binding. False, and nothing set, when the binding no longer exists.</summary>
    public bool TrySet(ulong key, TypedValue value)
    {
        var binding = GameReferences.Key(key);
        if (!Tree.Exists(ref binding))
        {
            return false;
        }

        Tree.SetInternal(ref binding, value, FromSimulation);
        return true;
    }

    private Bindings Tree => _tree ?? throw new InvalidOperationException("No delivery has shown the binding tree yet.");
}
