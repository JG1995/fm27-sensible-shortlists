using FM.UI;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Hooks;

/// <summary>
/// Something that wants to know what passes between the game's UI and its simulation core. An
/// observer implements only what it needs.
/// </summary>
internal interface IInteropObserver
{
    /// <summary>The UI sent an event or action to the simulation core.</summary>
    void EventSent(uint number, InteropReference? target, TypedValue? data)
    {
    }

    /// <summary>Someone asked the simulation core for a property of a game object.</summary>
    void ChannelOpened(InteropReference? reference, PropertyID property, ulong key)
    {
    }

    /// <summary>
    /// The simulation core answered a batch of channels: one value per key, in order. The game's
    /// handler reads the batch after the observers have seen it.
    /// </summary>
    /// <param name="handler">The game's handler, which is about to give the values to the screen.</param>
    void ValuesDelivered(InteropDataHandler handler, ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values)
    {
    }

    /// <summary>The game's interop layer is starting a frame. The safe moment to call into the game.</summary>
    void Tick()
    {
    }

    /// <summary>
    /// Every observer has had its tick. What is queued for the simulation now is what the game is
    /// about to send it this frame.
    /// </summary>
    void BeforeSend()
    {
    }
}
