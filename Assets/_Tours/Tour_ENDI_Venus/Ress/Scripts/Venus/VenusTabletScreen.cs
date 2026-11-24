// VenusTabletScreen.cs
// Делает планшет «настоящим»:
//   1. приводит размер к реальному (по умолчанию самая длинная сторона 0,35 м);
//   2. ставит точку хвата на нижний край: планшет держится рукой снизу, экраном к игроку, чуть откинут назад;
//   3. экран сначала тёмный, при первом взятии включается и показывает интерфейс с кнопками:
//      «ЗАБОР ГРУНТА» (этап S06), «ОТПРАВИТЬ» (этап S08) и круглая «ТЕЛЕПОРТ» (этап S09, появляется вместо первых двух).
//      Кнопки шлют событие Pressed в InteractionBus (btn_get_data, btn_send, btn_teleport), по ним сценарий идёт дальше.
// Ориентацию считает сам по меше (самая тонкая ось = нормаль экрана, сторона определяется по материалу M_Screen).
// Добавляется на планшет автоматически (VenusDirector). Подключение кнопок к рукам XR делает VenusTabletXRBinder.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VenusTabletScreen : MonoBehaviour
{
    public const string GetDataId = "btn_get_data";
    public const string SendId = "btn_send";
    public const string TeleportId = "btn_teleport";

    [Header("Идентификатор (как у InteractionRelay)")]
    [SerializeField] private string _objectId = "item_venus_tablet";

    [Header("Размер и хват")]
    [SerializeField] private float _targetLongestSide = 0.35f;   // длинная сторона планшета, м
    [SerializeField] private float _tiltDegrees = 20f;           // насколько верх планшета откинут от игрока
    [SerializeField] private bool _flipFacing = true;            // экран смотрел от игрока: перевёрнуто на лицевую сторону

    [Header("Экран")]
    [SerializeField] private string _screenMaterialName = "M_Screen";
    [SerializeField] private Color _screenOnColor = new Color(0.1f, 0.85f, 0.95f);
    [SerializeField] private Color _screenOffColor = new Color(0.02f, 0.03f, 0.04f);
    [SerializeField] private float _emission = 1.6f;

    private sealed class Button
    {
        public string id;
        public GameObject go;
        public Image image;
        public TMP_Text label;
        public bool enabled;
        public bool used;       // уже нажата на этом этапе
        public bool round;      // круглая кнопка с картинкой (телепорт)
    }

    private static readonly Color _buttonOn = new Color(0.1f, 0.7f, 0.85f);
    private static readonly Color _buttonOff = new Color(0.1f, 0.17f, 0.2f);
    private static readonly Color _labelOn = new Color(0.01f, 0.1f, 0.13f);
    private static readonly Color _labelOff = new Color(0.4f, 0.5f, 0.55f);
    private static readonly Color _roundOff = new Color(0.35f, 0.4f, 0.45f);

    private readonly List<Button> _buttons = new List<Button>();
    private Material _screenMaterial;
    private Canvas _canvas;
    private TMP_Text _status;
    private RectTransform _statusRect;
    private StageManager _manager;
    private bool _hasManager;
    private string _stageId = string.Empty;
    private bool _held;
    private bool _built;

    public bool IsReady => _built;
    public bool XrBound { get; set; }

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        InteractionBus.Raised += OnInteraction;
        _manager = FindFirstObjectByType<StageManager>();
        _hasManager = _manager != null;
        if (_manager != null)
        {
            _manager.StageEntered += OnStageEntered;
            _manager.ScenarioCompleted += OnScenarioCompleted;
            _stageId = _manager.Current != null ? _manager.Current.stageId : string.Empty;
        }
        _held = false;
        if (_built) Refresh();
    }

    private void OnDisable()
    {
        InteractionBus.Raised -= OnInteraction;
        if (_manager != null)
        {
            _manager.StageEntered -= OnStageEntered;
            _manager.ScenarioCompleted -= OnScenarioCompleted;
        }
        _manager = null;
    }

    private void OnInteraction(InteractionEvent e)
    {
        if (e.objectId != _objectId || e.kind != InteractionKind.Grabbed) return;
        _held = true;
        Refresh();
    }

    private void OnStageEntered(StageData data)
    {
        _stageId = data != null ? data.stageId : string.Empty;
        Refresh();
    }

    private void OnScenarioCompleted()
    {
        _stageId = string.Empty;
        Refresh();
    }

    public bool IsHeld => _held;

    // Для XR-подключения: id и объект каждой кнопки.
    public void GetButtons(List<KeyValuePair<string, GameObject>> into)
    {
        foreach (var b in _buttons) into.Add(new KeyValuePair<string, GameObject>(b.id, b.go));
    }

    // Нажатие кнопки игроком (или отладочной клавишей). Возвращает false и причину, если нажатие не засчитано.
    public bool PressButton(string id, out string reason)
    {
        reason = null;
        var b = _buttons.Find(x => x.id == id);
        if (!_built || b == null)
        {
            reason = "На планшете нет кнопки " + id + ".";
            return false;
        }
        if (!_held)
        {
            reason = "Планшет не взят в руки (экран выключен): сначала возьмите его (клавиша F6).";
            return false;
        }
        if (!b.enabled)
        {
            reason = b.used
                ? "Кнопка «" + Caption(id) + "» уже нажата, ждём смены этапа."
                : "Кнопка «" + Caption(id) + "» сейчас неактивна: она работает только на этапе " + RequiredStage(id)
                  + ", " + (_hasManager && string.IsNullOrEmpty(_stageId) ? "а сценарий не запущен или завершён." : "а сейчас этап " + _stageId + ".");
            return false;
        }

        b.enabled = false;           // защита от двойного нажатия до смены этапа
        b.used = true;
        if (_status != null) _status.text = "Команда принята...";
        StartCoroutine(Flash(b));
        InteractionBus.Raise(new InteractionEvent(InteractionKind.Pressed, id));
        return true;
    }

    private static string Caption(string id) =>
        id == GetDataId ? "Забор грунта" : id == TeleportId ? "Телепорт" : "Отправить на Землю";

    private static string RequiredStage(string id) => id == GetDataId ? "S06" : id == TeleportId ? "S09" : "S08";

    // ---------- Сборка ----------

    private void Build()
    {
        if (!Measure(out Bounds whole, out Bounds screen, out bool hasScreen))
        {
            Debug.LogWarning("[Tablet] На планшете не найден меш, хват и экран не настроены.", this);
            return;
        }

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        int thin = 0, wide = 0;
        for (int i = 1; i < 3; i++)
        {
            if (whole.size[i] < whole.size[thin]) thin = i;
            if (whole.size[i] > whole.size[wide]) wide = i;
        }
        if (thin == wide) wide = (thin + 1) % 3;
        int mid = 3 - thin - wide;

        // Реальный размер: переводим размеры меша (в локальных единицах) в метры и подгоняем масштаб.
        Vector3 lossy = transform.lossyScale;
        float longest = 0f;
        for (int i = 0; i < 3; i++) longest = Mathf.Max(longest, whole.size[i] * Mathf.Abs(lossy[i]));
        if (longest > 0.0001f)
        {
            float ratio = _targetLongestSide / longest;
            if (Mathf.Abs(ratio - 1f) > 0.05f) transform.localScale *= ratio;
        }
        FixGrabZone();

        // Нормаль экрана: какая сторона тонкой оси ближе к материалу экрана.
        float sign = 1f;
        if (hasScreen)
        {
            float offset = screen.center[thin] - whole.center[thin];
            if (Mathf.Abs(offset) > whole.size[thin] * 0.02f) sign = Mathf.Sign(offset);
        }
        if (_flipFacing) sign = -sign;

        Vector3 n = axes[thin] * sign;     // из экрана в сторону игрока
        Vector3 up = axes[mid];            // «верх» экрана (короткая сторона: планшет альбомный)

        // Рука смотрит вперёд (от игрока), экран обращён к игроку: forward руки = -n, верх руки = верх экрана.
        Quaternion hold = Quaternion.LookRotation(-n, up);
        Quaternion tilted = hold * Quaternion.Euler(-_tiltDegrees, 0f, 0f);

        // Хват за нижний край по центру.
        Vector3 gripLocal = whole.center - up * (whole.size[mid] * 0.5f);
        var grab = transform.Find("GrabPoint");
        if (grab == null)
        {
            grab = new GameObject("GrabPoint").transform;
            grab.SetParent(transform, false);
            Debug.LogWarning("[Tablet] Не было GrabPoint, создан новый. Назначьте его в Attach Transform у XR Grab Interactable.", this);
        }
        grab.localPosition = gripLocal;
        grab.localRotation = tilted;

        BuildScreen(hasScreen ? screen : whole, whole, n, thin, mid, wide, hold);
        _built = true;
        Refresh();
    }

    // Границы всего меша и (если есть) части с материалом экрана, в локальных координатах планшета.
    private bool Measure(out Bounds whole, out Bounds screen, out bool hasScreen)
    {
        whole = new Bounds();
        screen = new Bounds();
        hasScreen = false;
        bool any = false;

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Encapsulate(ref whole, ref any, toRoot, mesh.bounds);

            var renderer = mf.GetComponent<MeshRenderer>();
            if (renderer == null) continue;
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
            {
                if (mats[i] == null || !mats[i].name.Contains(_screenMaterialName)) continue;
                try
                {
                    Encapsulate(ref screen, ref hasScreen, toRoot, mesh.GetSubMesh(i).bounds);
                }
                catch (System.Exception)
                {
                    // границы подмеша недоступны: используем весь меш
                }
            }
        }
        return any;
    }

    private static void Encapsulate(ref Bounds target, ref bool initialized, Matrix4x4 toRoot, Bounds source)
    {
        Vector3 c = source.center;
        Vector3 e = source.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            Vector3 p = toRoot.MultiplyPoint3x4(corner);
            if (!initialized)
            {
                target = new Bounds(p, Vector3.zero);
                initialized = true;
            }
            else
            {
                target.Encapsulate(p);
            }
        }
    }

    // Зона захвата вокруг планшета остаётся радиусом 0,3 м после смены размера.
    private void FixGrabZone()
    {
        var zone = transform.Find("GrabZone");
        if (zone == null) return;
        var sphere = zone.GetComponent<SphereCollider>();
        if (sphere == null) return;
        Vector3 s = transform.lossyScale;
        sphere.radius = 0.3f / Mathf.Max(Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)), Mathf.Max(Mathf.Abs(s.z), 0.01f));
    }

    private void BuildScreen(Bounds area, Bounds whole, Vector3 n, int thin, int mid, int wide, Quaternion rotation)
    {
        // Материал экрана (создаём свой экземпляр, общий ассет не трогаем).
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains(_screenMaterialName)) _screenMaterial = mats[i];
            }
        }

        float areaWide = Mathf.Max(area.size[wide], 0.0001f);
        float areaMid = Mathf.Max(area.size[mid], 0.0001f);
        const float pixelsWide = 800f;
        float pixelsHigh = pixelsWide * areaMid / areaWide;

        var go = new GameObject("ScreenCanvas");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        go.transform.localPosition = area.center + n * (area.size[thin] * 0.5f + whole.size[thin] * 0.02f);
        go.transform.localRotation = rotation;
        go.transform.localScale = Vector3.one * (areaWide * 0.94f / pixelsWide);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(pixelsWide, pixelsHigh);

        var panel = new GameObject("Panel").AddComponent<Image>();
        panel.transform.SetParent(go.transform, false);
        panel.gameObject.layer = gameObject.layer;
        panel.color = new Color(0.01f, 0.09f, 0.12f, 1f);
        Stretch(panel.rectTransform);

        MakeText(go.transform, "Title", "ВЕНЕРА-7  |  БОРТОВОЙ ПУЛЬТ", 0.80f, 0.97f, 34f, new Color(0.55f, 0.9f, 1f), FontStyles.Bold);
        _status = MakeText(go.transform, "Status", string.Empty, 0.38f, 0.78f, 52f, Color.white, FontStyles.Bold);
        _statusRect = _status.rectTransform;
        MakeTeleportButton(go.transform, pixelsHigh);

        MakeButton(go.transform, GetDataId, "ЗАБОР\nГРУНТА", 0.04f, 0.48f, 0.06f, 0.34f, pixelsWide, pixelsHigh);
        MakeButton(go.transform, SendId, "ОТПРАВИТЬ\nНА ЗЕМЛЮ", 0.52f, 0.96f, 0.06f, 0.34f, pixelsWide, pixelsHigh);
    }

    // Круглая кнопка телепорта с картинкой «портала». Показывается только на этапе S09 вместо двух прямоугольных.
    private void MakeTeleportButton(Transform parent, float pixelsHigh)
    {
        float d = pixelsHigh * 0.52f;
        var go = new GameObject("Btn_" + TeleportId);
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        var image = go.AddComponent<Image>();
        image.sprite = CreatePortalSprite();
        image.preserveAspect = true;
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.30f);
        rect.anchorMax = new Vector2(0.5f, 0.30f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(d, d);

        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(d, d, 24f);

        var label = MakeText(go.transform, "Label", "ТЕЛЕПОРТ", 0.36f, 0.64f, 44f, Color.white, FontStyles.Bold);
        _buttons.Add(new Button { id = TeleportId, go = go, image = image, label = label, round = true });
        go.SetActive(false);
    }

    // Картинка портала рисуется кодом: тёмный круг, спираль и яркое кольцо по краю. Файлов в проекте не нужно.
    private static Sprite CreatePortalSprite()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Clamp01((1f - r) * half);   // сглаженный край круга
                if (edge <= 0f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float swirl = 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + r * 12f);
                float ring = Smooth(0.80f, 0.90f, r) * (1f - Smooth(0.96f, 1f, r));
                Color inner = Color.Lerp(new Color(0.02f, 0.10f, 0.16f), new Color(0.05f, 0.45f, 0.60f), swirl * (1f - r) * 0.8f);
                Color c = Color.Lerp(inner, new Color(0.55f, 0.95f, 1f), ring);
                c.a = edge;
                pixels[y * size + x] = c;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private void MakeButton(Transform parent, string id, string text, float xMin, float xMax, float yMin, float yMax,
        float pixelsWide, float pixelsHigh)
    {
        var go = new GameObject("Btn_" + id);
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        var image = go.AddComponent<Image>();
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Коллайдер по размеру кнопки: по нему нажимают пальцем или лучом.
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(pixelsWide * (xMax - xMin), pixelsHigh * (yMax - yMin), 24f);

        var label = MakeText(go.transform, "Label", text, 0f, 1f, 40f, _labelOn, FontStyles.Bold);
        _buttons.Add(new Button { id = id, go = go, image = image, label = label });
    }

    private static TMP_Text MakeText(Transform parent, string name, string text, float yMin, float yMax,
        float size, Color color, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.4f;
        t.raycastTarget = false;
        var rect = t.rectTransform;
        rect.anchorMin = new Vector2(0.04f, yMin);
        rect.anchorMax = new Vector2(0.96f, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return t;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------- Состояние экрана ----------

    // Что показывать, зависит от этапа: S06 забор грунта, S07 бурение, S08 отправка, дальше «отправлено».
    private void Refresh()
    {
        if (!_built) return;

        SetScreenMaterial(_held);
        if (_canvas != null) _canvas.gameObject.SetActive(_held);
        if (!_held) return;

        bool canGet = false;
        bool canSend = false;
        bool canTeleport = false;
        string status;
        switch (_stageId)
        {
            case "S06":
                canGet = true;
                status = "Нажми «Забор грунта»";
                break;
            case "S07":
                status = "Бур работает.\nИдёт забор грунта...";
                break;
            case "S08":
                canSend = true;
                status = "Данные получены.\nОтправь их на Землю";
                break;
            case "S09":
                canTeleport = true;
                status = "Нажми на телепорт";
                break;
            case "S10":
                status = "Данные отправлены\nна Землю";
                break;
            case "":
                if (_hasManager)
                {
                    status = "Ожидание";           // сценарий не запущен или завершён: кнопки серые
                }
                else
                {
                    canGet = true;                 // планшет без сценария (тестовая сцена)
                    canSend = true;
                    status = "Планшет включён";
                }
                break;
            default:
                status = "Ожидание";
                break;
        }

        if (_status != null) _status.text = status;
        if (_statusRect != null)
        {
            // С круглой кнопкой текст статуса короче и стоит выше.
            _statusRect.anchorMin = new Vector2(0.04f, canTeleport ? 0.58f : 0.38f);
            _statusRect.anchorMax = new Vector2(0.96f, 0.78f);
        }
        foreach (var b in _buttons)
        {
            b.used = false;
            if (b.id == TeleportId)
            {
                b.enabled = canTeleport;
                b.go.SetActive(canTeleport);
            }
            else
            {
                b.enabled = b.id == GetDataId ? canGet : canSend;
                b.go.SetActive(!canTeleport);
            }
            Paint(b, b.enabled ? _buttonOn : _buttonOff, b.enabled ? _labelOn : _labelOff);
        }
    }

    private static void Paint(Button b, Color background, Color text)
    {
        if (b.round)
        {
            // Круглая кнопка: картинка и так цветная, поэтому только светлая или приглушённая.
            bool on = background != _buttonOff;
            if (b.image != null) b.image.color = on ? Color.white : _roundOff;
            if (b.label != null) b.label.color = on ? Color.white : _labelOff;
            return;
        }
        if (b.image != null) b.image.color = background;
        if (b.label != null) b.label.color = text;
    }

    private IEnumerator Flash(Button b)
    {
        Paint(b, Color.white, _labelOn);
        yield return new WaitForSeconds(0.2f);
        if (!b.enabled) Paint(b, _buttonOff, _labelOff);
    }

    private void SetScreenMaterial(bool on)
    {
        if (_screenMaterial == null) return;
        _screenMaterial.SetColor("_BaseColor", on ? _screenOnColor * 0.25f : _screenOffColor);
        if (on)
        {
            _screenMaterial.EnableKeyword("_EMISSION");
            _screenMaterial.SetColor("_EmissionColor", _screenOnColor * _emission);
        }
        else
        {
            _screenMaterial.SetColor("_EmissionColor", Color.black);
        }
    }
}
