// StageSequencer.cs
// Для каждого этапа решает две вещи: что сделать при входе (перенести игрока, начать падение, поднять Солнце)
// и когда переходить дальше (после реплик, по сигналу или по действию игрока).
// StageManager об этом ничего не знает, его On Enter / On Exit остаются пустыми.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageSequencer : MonoBehaviour
{
    public enum AdvanceMode
    {
        Manual,        // сам не переходит (кнопки в инспекторе StageManager)
        AfterLines,    // после всех реплик этапа + пауза
        Signal,        // когда игрок высадился (событие переключателя локаций)
        Interaction    // когда игрок совершил действие с объектом objectId
    }

    public enum EnterAction
    {
        None,
        EnterIntro,                // каюта и космос в окне
        EnterSurfaceWithDescent,   // падение в окне и высадка
        RaiseSun,                  // Солнце восходит
        StartDrill                 // бур начинает работу
    }

    [Serializable]
    public class Rule
    {
        public string stageId;
        public EnterAction onEnter = EnterAction.None;
        public AdvanceMode mode = AdvanceMode.AfterLines;
        public float delay = 1.5f;
        public string objectId;
        public InteractionKind kind = InteractionKind.Pressed;
    }

    [SerializeField] private StageManager _stages;
    [SerializeField] private AssistantController _assistant;
    [SerializeField] private VenusLocationSwitcher _switcher;
    [SerializeField] private DrillController _drill;
    [SerializeField] private List<Rule> _rules = new List<Rule>();

    private Rule _active;
    private Coroutine _routine;
    private bool _linesDone;

#if UNITY_EDITOR
    public void EditorSetup(StageManager stages, AssistantController assistant, VenusLocationSwitcher switcher,
        DrillController drill, List<Rule> rules)
    {
        _stages = stages;
        _assistant = assistant;
        _switcher = switcher;
        _drill = drill;
        _rules = rules;
    }
#endif

    private void Awake() => NormalizeTabletRules();

    // По ТЗ планшета S06 ждёт кнопку «Забор грунта» (а не взятие планшета), S08 ждёт «Отправить на Землю»,
    // S09 ждёт круглую кнопку «Телепорт» на планшете.
    // Правила лежат в сцене, поэтому старые значения приводятся к нужным при запуске (в инспекторе их тоже можно поправить).
    private void NormalizeTabletRules()
    {
        foreach (var rule in _rules)
        {
            string expected = rule.stageId == "S06" ? VenusTabletScreen.GetDataId
                : rule.stageId == "S08" ? VenusTabletScreen.SendId
                : rule.stageId == "S09" ? VenusTabletScreen.TeleportId : null;
            if (expected == null) continue;
            if (rule.mode == AdvanceMode.Interaction && rule.objectId == expected && rule.kind == InteractionKind.Pressed) continue;

            Debug.LogWarning($"[StageSequencer] Правило {rule.stageId} исправлено: ждёт {InteractionKind.Pressed} от '{expected}' " +
                $"(было {rule.mode} / '{rule.objectId}' / {rule.kind}). Запустите Tools/Venus/Применить ТЗ планшета и сохраните сцену.", this);
            rule.mode = AdvanceMode.Interaction;
            rule.objectId = expected;
            rule.kind = InteractionKind.Pressed;
            if (rule.delay <= 0f) rule.delay = 0.5f;
        }
    }

    private void OnEnable()
    {
        if (_stages != null)
        {
            _stages.StageEntered += OnStageEntered;
            _stages.StageExited += OnStageExited;
            _stages.InteractionReceived += OnInteraction;
        }
        if (_assistant != null) _assistant.QueueEmpty += OnQueueEmpty;
        if (_switcher != null) _switcher.SurfaceReached += Signal;
    }

    private void OnDisable()
    {
        if (_stages != null)
        {
            _stages.StageEntered -= OnStageEntered;
            _stages.StageExited -= OnStageExited;
            _stages.InteractionReceived -= OnInteraction;
        }
        if (_assistant != null) _assistant.QueueEmpty -= OnQueueEmpty;
        if (_switcher != null) _switcher.SurfaceReached -= Signal;
    }

    private void OnStageEntered(StageData data)
    {
        StopRoutine();
        _linesDone = false;
        _active = data == null ? null : _rules.Find(r => r.stageId == data.stageId);
        if (_active == null) return;

        RunEnterAction(_active.onEnter);

        if (_active.mode == AdvanceMode.AfterLines)
            _routine = StartCoroutine(WaitLinesThenAdvance(_stages.CurrentIndex));
    }

    private void OnStageExited(StageData data)
    {
        StopRoutine();
        _active = null;
    }

    private void RunEnterAction(EnterAction action)
    {
        if (action == EnterAction.StartDrill)
        {
            if (_drill != null) _drill.Play();
            return;
        }
        if (_switcher == null) return;
        switch (action)
        {
            case EnterAction.EnterIntro: _switcher.EnterIntro(); break;
            case EnterAction.EnterSurfaceWithDescent: _switcher.EnterSurfaceWithDescent(); break;
            case EnterAction.RaiseSun: _switcher.RaiseSun(); break;
        }
    }

    private void OnQueueEmpty() => _linesDone = true;

    // Вызывается, когда игрок высадился (событие VenusLocationSwitcher.SurfaceReached).
    private void Signal()
    {
        if (_active == null || _active.mode != AdvanceMode.Signal) return;
        StopRoutine();
        _routine = StartCoroutine(AdvanceAfter(_active.delay, _stages.CurrentIndex));
    }

    private void OnInteraction(InteractionEvent e)
    {
        if (_active == null || _active.mode != AdvanceMode.Interaction) return;
        if (e.kind != _active.kind || e.objectId != _active.objectId) return;
        StopRoutine();
        _routine = StartCoroutine(AdvanceAfter(_active.delay, _stages.CurrentIndex));
    }

    private IEnumerator WaitLinesThenAdvance(int stageIndex)
    {
        yield return null;                                   // помощник успевает начать первую реплику
        if (_assistant == null || !_assistant.IsSpeaking) _linesDone = true;   // реплик нет
        while (!_linesDone) yield return null;
        yield return AdvanceAfter(_active.delay, stageIndex);
    }

    private IEnumerator AdvanceAfter(float seconds, int stageIndex)
    {
        yield return new WaitForSeconds(seconds);
        _routine = null;
        if (_stages.CurrentIndex == stageIndex) _stages.Next();
    }

    private void StopRoutine()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
    }
}
