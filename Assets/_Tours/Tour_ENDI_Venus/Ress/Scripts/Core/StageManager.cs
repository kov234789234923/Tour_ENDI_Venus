// StageManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class StageManager : MonoBehaviour
{
    [Serializable]
    public class StageBinding
    {
        public StageData data;
        [Tooltip("Объекты, которые активны только на этом этапе")]
        public GameObject[] activeObjects;
        public UnityEvent onEnter;
        public UnityEvent onExit;
    }

    [SerializeField] private AssistantController _assistant;
    [SerializeField] private List<StageBinding> _stages = new List<StageBinding>();
    [SerializeField] private bool _autoStart = true;

    private bool _busy;

    public int CurrentIndex { get; private set; } = -1;
    public int StageCount => _stages.Count;
    public StageData Current => CurrentIndex >= 0 ? _stages[CurrentIndex].data : null;

    public event Action<StageData> StageEntered;
    public event Action<StageData> StageExited;
    public event Action ScenarioCompleted;
    public event Action<InteractionEvent> InteractionReceived;   // этап 3: действия игрока

#if UNITY_EDITOR
    internal List<StageBinding> EditorStages => _stages;

    internal void EditorSetup(AssistantController assistant, bool autoStart)
    {
        _assistant = assistant;
        _autoStart = autoStart;
    }
#endif

    private void Awake()
    {
        foreach (var s in _stages) SetActive(s.activeObjects, false);
    }

    private void OnEnable() => InteractionBus.Raised += OnInteraction;

    private void OnDisable() => InteractionBus.Raised -= OnInteraction;

    private void Start()
    {
        if (_autoStart) GoTo(0);
    }

    public void Next()
    {
        if (CurrentIndex + 1 < _stages.Count) GoTo(CurrentIndex + 1);
        else Complete();
    }

    public void Previous()
    {
        if (CurrentIndex > 0) GoTo(CurrentIndex - 1);
    }

    public void GoToId(string stageId)
    {
        int i = _stages.FindIndex(s => s.data != null && s.data.stageId == stageId);
        if (i < 0) Debug.LogError($"[StageManager] Этап '{stageId}' не найден", this);
        else GoTo(i);
    }

    public void GoTo(int index)
    {
        if (_busy)
        {
            Debug.LogWarning("[StageManager] Переход уже идёт, вызов проигнорирован", this);
            return;
        }
        if (index < 0 || index >= _stages.Count)
        {
            Debug.LogError($"[StageManager] Нет этапа с индексом {index}", this);
            return;
        }

        _busy = true;
        try
        {
            if (CurrentIndex >= 0) ExitStage(_stages[CurrentIndex]);
            CurrentIndex = index;
            EnterStage(_stages[index]);
        }
        finally
        {
            _busy = false;
        }
    }

    public string GetLabel(int index)
    {
        var d = _stages[index].data;
        return d == null ? $"{index}: (пусто)" : $"{d.stageId}: {d.title}";
    }

    private void EnterStage(StageBinding s)
    {
        SetActive(s.activeObjects, true);          // сначала включаем объекты,
        HighlightRegistry.ClearAll();              // чтобы подсветка нашла их в реестре
        if (s.data != null && s.data.highlightIds != null)
        {
            foreach (var id in s.data.highlightIds) HighlightRegistry.Set(id, true);
        }

        if (_assistant != null)
        {
            _assistant.ClearQueue();
            if (s.data != null && s.data.entryLineIds != null)
            {
                foreach (var lineId in s.data.entryLineIds) _assistant.Say(lineId);
            }
        }

        Debug.Log($"[StageManager] → {GetLabel(CurrentIndex)}");
        s.onEnter.Invoke();
        StageEntered?.Invoke(s.data);
    }

    private void ExitStage(StageBinding s)
    {
        s.onExit.Invoke();
        StageExited?.Invoke(s.data);
        HighlightRegistry.ClearAll();
        if (_assistant != null) _assistant.ClearQueue();
        SetActive(s.activeObjects, false);
    }

    private void Complete()
    {
        if (CurrentIndex >= 0) ExitStage(_stages[CurrentIndex]);
        CurrentIndex = -1;
        ScenarioCompleted?.Invoke();
        Debug.Log("[StageManager] Сценарий завершён");
    }

    private void OnInteraction(InteractionEvent e)
    {
        if (CurrentIndex < 0) return;
        Debug.Log($"[StageManager] {Current.stageId}: {e}", this);
        InteractionReceived?.Invoke(e);
    }

    private static void SetActive(GameObject[] objects, bool value)
    {
        if (objects == null) return;
        foreach (var o in objects)
        {
            if (o != null) o.SetActive(value);
        }
    }

    [ContextMenu("Проверить данные")]
    private void ValidateData()
    {
        int errors = 0;
        foreach (var s in _stages)
        {
            if (s.data == null)
            {
                Debug.LogError("Пустой StageData в списке этапов", this);
                errors++;
                continue;
            }
            if (s.data.entryLineIds == null) continue;
            foreach (var id in s.data.entryLineIds)
            {
                if (_assistant == null || !_assistant.HasLine(id))
                {
                    Debug.LogError($"{s.data.stageId}: нет реплики '{id}'", this);
                    errors++;
                }
            }
        }
        Debug.Log(errors == 0 ? "Данные в порядке" : $"Найдено ошибок: {errors}", this);
    }
}
