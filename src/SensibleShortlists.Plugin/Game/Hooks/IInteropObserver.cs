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

    /// <summary>The simulation core answered a batch of channels: one value per key, in order.</summary>
    void ValuesDelivered(ulong[] keys, Il2CppSystem.Collections.Generic.List<TypedValue> values)
    {
    }

    /// <summary>The game's interop layer is starting a frame. The safe moment to call into the game.</summary>
    void Tick()
    {
    }
}
