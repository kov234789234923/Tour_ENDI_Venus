// HighlightRegistry.cs
using System.Collections.Generic;
using UnityEngine;

public static class HighlightRegistry
{
    private static readonly Dictionary<string, List<HighlightTarget>> _map = new Dictionary<string, List<HighlightTarget>>();

    // Нужно при выключенной перезагрузке домена: без сброса данные переживут остановку Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _map.Clear();

    public static void Register(HighlightTarget target)
    {
        if (string.IsNullOrEmpty(target.Id)) return;
        if (!_map.TryGetValue(target.Id, out var list)) _map[target.Id] = list = new List<HighlightTarget>();
        if (!list.Contains(target)) list.Add(target);
    }

    public static void Unregister(HighlightTarget target)
    {
        if (!string.IsNullOrEmpty(target.Id) && _map.TryGetValue(target.Id, out var list)) list.Remove(target);
    }

    public static void Set(string id, bool on)
    {
        if (_map.TryGetValue(id, out var list))
        {
            foreach (var t in list) t.SetHighlight(on);
        }
        else if (on)
        {
            Debug.LogWarning($"[Highlight] Нет активного объекта с ID '{id}'");
        }
    }

    public static void ClearAll()
    {
        foreach (var list in _map.Values)
        {
            foreach (var t in list) t.SetHighlight(false);
        }
    }
}
