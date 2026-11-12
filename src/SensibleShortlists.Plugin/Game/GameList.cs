using System.Collections;
using SI.Core;

namespace SensibleShortlists.Plugin.Game;

/// <summary>Lets the core read one of the game's lists in place instead of copying it.</summary>
internal sealed class GameList : IReadOnlyList<TypedValue>
{
    private readonly Il2CppSystem.Collections.Generic.List<TypedValue> _list;

    public GameList(Il2CppSystem.Collections.Generic.List<TypedValue> list) => _list = list;

    public int Count => _list.Count;

    public TypedValue this[int index] => _list[index];

    public IEnumerator<TypedValue> GetEnumerator()
    {
        for (var i = 0; i < _list.Count; i++)
        {
            yield return _list[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
