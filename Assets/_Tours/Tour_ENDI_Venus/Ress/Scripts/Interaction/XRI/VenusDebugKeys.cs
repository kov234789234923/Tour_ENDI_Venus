// VenusDebugKeys.cs
// Отладочные клавиши для проверки сценария без шлема и без возни с симулятором.
// Работают только в редакторе и в Development Build; создаются сами при запуске сцены, в сцену ничего добавлять не нужно.
//   F6  : взять планшет рукой / отпустить (настоящий захват через XR Interaction Manager)
//   F7  : нажать на планшете кнопку «Забор грунта»      (работает только на этапе S06)
//   F8  : нажать на планшете кнопку «Отправить на Землю» (работает только на этапе S08)
//   F9  : следующий этап
//   F10 : нажать на планшете кнопку «Телепорт» (работает только на этапе S09)
// F7, F8 и F10 нажимают кнопки планшета так же, как игрок: если планшет не взят в руки или кнопка сейчас серая,
// в Console появляется предупреждение с причиной.
// Клавиши F6-F10 выбраны так, чтобы не пересекаться с XR Device Simulator (WASD, Shift, Space, G, B, N, M, мышь).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VenusDebugKeys : MonoBehaviour
{
    private const string _tabletId = "item_venus_tablet";

    private string _status = string.Empty;
    private float _statusTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<VenusDebugKeys>() != null) return;
        new GameObject("VenusDebugKeys").AddComponent<VenusDebugKeys>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f6Key.wasPressedThisFrame) ToggleTablet();
        if (kb.f7Key.wasPressedThisFrame) PressTabletButton(VenusTabletScreen.GetDataId, "Забор грунта");
        if (kb.f8Key.wasPressedThisFrame) PressTabletButton(VenusTabletScreen.SendId, "Отправить на Землю");
        if (kb.f9Key.wasPressedThisFrame) NextStage();
        if (kb.f10Key.wasPressedThisFrame) PressTabletButton(VenusTabletScreen.TeleportId, "Телепорт");
    }

    private void ToggleTablet()
    {
        var relay = FindObjectsByType<InteractionRelay>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(r => r.ObjectId == _tabletId);
        if (relay == null)
        {
            Warn("Планшета нет: он появляется на этапе S06 (F9 пролистывает этапы).");
            return;
        }

        var grab = relay.GetComponent<XRGrabInteractable>();
        if (grab == null || grab.interactionManager == null)
        {
            Warn("У планшета нет XRGrabInteractable или менеджера. Запустите Tools/Venus/Проверить планшет и руки.");
            return;
        }

        if (grab.isSelected)
        {
            grab.interactionManager.SelectExit(grab.interactorsSelecting[0], grab);
            Say("Планшет отпущен.");
            return;
        }

        var hand = PickHand();
        if (hand == null)
        {
            Warn("Не найдено ни одной руки (интерактора). Нужен XR Origin из Starter Assets.");
            return;
        }

        grab.interactionManager.SelectEnter(hand, grab);
        Say("Планшет взят рукой " + ((Component)hand).name + ".");
    }

    // Предпочитаем правую руку; сокеты и палец-нажималка для хвата не подходят.
    private static IXRSelectInteractor PickHand()
    {
        var hands = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(i => i.isActiveAndEnabled && !(i is XRSocketInteractor) && !(i is XRPokeInteractor))
            .ToList();
        var right = hands.FirstOrDefault(i => i.name.ToLowerInvariant().Contains("right"));
        return right != null ? right : hands.FirstOrDefault();
    }

    // Нажатие кнопки на экране планшета: ровно то, что делает рука игрока.
    private void PressTabletButton(string id, string caption)
    {
        var tablet = FindFirstObjectByType<VenusTabletScreen>(FindObjectsInactive.Exclude);
        if (tablet == null)
        {
            Warn("Кнопка «" + caption + "» не нажата: планшета сейчас нет в мире (он появляется на этапе S06).");
            return;
        }

        if (tablet.PressButton(id, out string reason)) Say("Нажато на планшете: «" + caption + "».");
        else Warn("Кнопка «" + caption + "» не нажата. " + reason);
    }

    private void NextStage()
    {
        var manager = FindFirstObjectByType<StageManager>();
        if (manager == null)
        {
            Warn("StageManager не найден.");
            return;
        }
        manager.Next();
        Say("Следующий этап.");
    }

    private void Say(string text)
    {
        _status = text;
        _statusTime = Time.unscaledTime;
        Debug.Log("[VenusDebugKeys] " + text);
    }

    private void Warn(string text)
    {
        _status = text;
        _statusTime = Time.unscaledTime;
        Debug.LogWarning("[VenusDebugKeys] " + text);
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
        GUI.Label(new Rect(10, 10, 760, 24), "F6 планшет   F7 Забор грунта   F8 Отправить на Землю   F9 след. этап   F10 Телепорт", style);
        if (!string.IsNullOrEmpty(_status) && Time.unscaledTime - _statusTime < 4f)
            GUI.Label(new Rect(10, 34, 1000, 24), _status, style);
    }
}
#endif
