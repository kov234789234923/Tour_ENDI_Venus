// LineLibrary.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LineEntry
{
    public string id;                       // "S02_L01"
    [TextArea(2, 6)] public string text;    // текст из сценария дословно
    public AudioClip audio;                 // пока пусто
    [Min(0f)] public float pauseAfter = 0.5f;
}

[CreateAssetMenu(menuName = "VR Scenario/Line Library", fileName = "Lines_")]
public class LineLibrary : ScriptableObject
{
    [SerializeField] private List<LineEntry> _lines = new List<LineEntry>();
    private Dictionary<string, LineEntry> _map;

    public int Count => _lines.Count;

    public bool Contains(string id) => TryGet(id, out _);

    public bool TryGet(string id, out LineEntry entry)
    {
        if (_map == null) Rebuild();
        return _map.TryGetValue(id ?? string.Empty, out entry);
    }

    private void Rebuild()
    {
        _map = new Dictionary<string, LineEntry>();
        foreach (var line in _lines)
        {
            if (string.IsNullOrWhiteSpace(line.id)) continue;
            if (!_map.TryAdd(line.id, line))
                Debug.LogError($"Повторяющийся ID реплики '{line.id}' в {name}", this);
        }
    }

    private void OnEnable() => _map = null;
    private void OnValidate() => _map = null;   // правка в инспекторе сбрасывает кэш

#if UNITY_EDITOR
    public void SetLines(List<LineEntry> entries)
    {
        _lines = entries;
        _map = null;
    }
#endif
}
