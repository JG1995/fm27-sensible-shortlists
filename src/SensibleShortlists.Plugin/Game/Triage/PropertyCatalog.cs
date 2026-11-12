using SensibleShortlists.Core.Diagnostics;
using SensibleShortlists.Core.Triage;
using SI.Bindable.Reference.Core;
using SI.Interop;

namespace SensibleShortlists.Plugin.Game.Triage;

/// <summary>
/// Finds a property of a game object by name. The game identifies properties by numbers that
/// cannot be derived from their names, but every object can list its own properties and each
/// prints as <c>Name:number</c>, so the names are looked up in the running game.
/// </summary>
internal sealed class PropertyCatalog
{
    private readonly Dictionary<string, Dictionary<string, PropertyID>> _byKind = new();
    private readonly ITraceSink _log;

    public PropertyCatalog(ITraceSink log) => _log = log;

    public bool TryFind(InteropReference target, string name, out PropertyID property)
    {
        var kind = target.GetIl2CppType().Name;
        if (!_byKind.TryGetValue(kind, out var properties))
        {
            properties = List(target);
            _byKind[kind] = properties;

            // Written once per kind of object: the vocabulary the next rule or repair will need.
            _log.Write($"NOTE properties of {kind}: {string.Join(" ", properties.Keys.OrderBy(key => key))}");
        }

        return properties.TryGetValue(name, out property);
    }

    private static Dictionary<string, PropertyID> List(InteropReference target)
    {
        var listed = new Il2CppSystem.Collections.Generic.List<PropertyID>();
        target.GetProperties(listed);
        var properties = new Dictionary<string, PropertyID>(StringComparer.Ordinal);
        for (var i = 0; i < listed.Count; i++)
        {
            var property = listed[i];
            properties[InboxWatch.NameOf(property.ToString())] = property;
        }

        return properties;
    }
}
