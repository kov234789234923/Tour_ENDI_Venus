// AssistantEmotions.cs
// Эмоции помощника: меняются цвет свечения, пульсация и «лицо» над роботом. Эмоция берётся из ID реплики.
// Логику реплик не трогает: только слушает события AssistantController.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(AssistantController))]
public class AssistantEmotions : MonoBehaviour
{
    public enum Emotion { Neutral, Happy, Excited, Surprised, Worried, Thinking, Sad }

    private struct Look
    {
        public Color color;
        public float intensity;
        public float pulseAmount;
        public float pulseSpeed;
        public string face;
    }

    private static readonly Dictionary<Emotion, Look> Looks = new Dictionary<Emotion, Look>
    {
        { Emotion.Neutral,   new Look { color = new Color(1f, 0.70f, 0.25f), intensity = 1.6f, pulseAmount = 0.02f, pulseSpeed = 2f,  face = "o_o" } },
        { Emotion.Happy,     new Look { color = new Color(1f, 0.90f, 0.35f), intensity = 2.2f, pulseAmount = 0.05f, pulseSpeed = 3f,  face = "^_^" } },
        { Emotion.Excited,   new Look { color = new Color(1f, 0.50f, 0.15f), intensity = 2.8f, pulseAmount = 0.09f, pulseSpeed = 7f,  face = "^o^" } },
        { Emotion.Surprised, new Look { color = new Color(0.6f, 0.95f, 1f),  intensity = 2.6f, pulseAmount = 0.12f, pulseSpeed = 10f, face = "O_O" } },
        { Emotion.Worried,   new Look { color = new Color(1f, 0.25f, 0.15f), intensity = 2.0f, pulseAmount = 0.06f, pulseSpeed = 12f, face = "o_O" } },
        { Emotion.Thinking,  new Look { color = new Color(0.65f, 0.5f, 1f),  intensity = 1.5f, pulseAmount = 0.03f, pulseSpeed = 1.5f, face = "?_?" } },
        { Emotion.Sad,       new Look { color = new Color(0.25f, 0.4f, 1f),  intensity = 1.0f, pulseAmount = 0.02f, pulseSpeed = 1f,  face = ";_;" } }
    };

    private static readonly Dictionary<string, Emotion> ByLine = new Dictionary<string, Emotion>
    {
        { "S01_L01", Emotion.Happy },
        { "S03_L01", Emotion.Worried },
        { "S04_L01", Emotion.Thinking },
        { "S05_L01", Emotion.Excited },
        { "S05_L02", Emotion.Surprised },
        { "S06_L01", Emotion.Excited },
        { "S09_L01", Emotion.Happy },
        { "S09_L02", Emotion.Excited },
        { "S10_L01", Emotion.Happy },
        { "S11_L01", Emotion.Sad },
        { "S11_L02", Emotion.Happy },
        { "S11_L03", Emotion.Excited },
        { "S12_L01", Emotion.Excited }
    };

    [SerializeField] private AssistantController _assistant;
    [SerializeField] private Light _glow;
    [SerializeField] private float _faceHeight = 0.2f;

    private readonly List<Material> _materials = new List<Material>();
    private TMP_Text _face;
    private Emotion _current = Emotion.Neutral;
    private Color _color;
    private float _intensity;

    public Emotion Current => _current;

    private void Awake()
    {
        if (_assistant == null) _assistant = GetComponent<AssistantController>();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponentInParent<Canvas>() != null || r.GetComponent<TMP_Text>() != null) continue;
            foreach (var m in r.materials)
            {
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                m.EnableKeyword("_EMISSION");
                _materials.Add(m);
            }
        }

        if (_glow == null) _glow = GetComponentInChildren<Light>(true);
        if (_glow == null)
        {
            var go = new GameObject("EmotionGlow");
            go.transform.SetParent(transform, false);
            _glow = go.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = 7f;
            _glow.shadows = LightShadows.None;
        }

        FindScreen();
        if (_screen == null) BuildFace();   // текстовое «лицо» — только запасной вариант
        _color = Looks[Emotion.Neutral].color;
        _intensity = Looks[Emotion.Neutral].intensity;
    }

    private void OnEnable()
    {
        if (_assistant == null) return;
        _assistant.LineStarted += OnLineStarted;
        _assistant.QueueEmpty += OnQueueEmpty;
    }

    private void OnDisable()
    {
        if (_assistant == null) return;
        _assistant.LineStarted -= OnLineStarted;
        _assistant.QueueEmpty -= OnQueueEmpty;
    }

    public void SetEmotion(Emotion emotion)
    {
        _current = emotion;
        if (_face != null) _face.text = Looks[emotion].face;
        if (_screen != null && _setScreen != null) _setScreen.Invoke(_screen, new object[] { ScreenIndex[(int)emotion] });
    }

    // Номера эмоций в атласе экрана NeuroGuide (порядок как у enum Emotion выше):
    // Neutral 0, Happy 1, Excited 14, Surprised 5, Worried 9, Thinking 6, Sad 8.
    private static readonly int[] ScreenIndex = { 0, 1, 14, 5, 9, 6, 8 };

    private Component _screen;
    private System.Reflection.MethodInfo _setScreen;

    // Экран-лицо робота (NeuroScreenEmotion из Tour_ENDI_NeuroGuide). Тип ищем по имени, так как он в другой сборке.
    private void FindScreen()
    {
        System.Type type = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType("NeuroScreenEmotion");
            if (type != null) break;
        }
        if (type == null) return;

        _screen = GetComponentInChildren(type, true);
        if (_screen == null)
        {
            Renderer monitor = null;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Monitor")) { monitor = r; break; }
            }
            if (monitor == null)
            {
                Debug.LogWarning("[AssistantEmotions] У робота нет экрана Monitor_low, эмоции на лице не работают.", this);
                return;
            }
            _screen = gameObject.AddComponent(type);
            type.GetMethod("Configure")?.Invoke(_screen, new object[] { monitor, new Vector2(0.500035f, 0.50681f), 0.99191f });
        }
        _setScreen = type.GetMethod("SetEmotion", new[] { typeof(int) });
    }

    private void OnLineStarted(string id)
    {
        SetEmotion(ByLine.TryGetValue(id, out Emotion e) ? e : Emotion.Neutral);
    }

    private void OnQueueEmpty() => SetEmotion(Emotion.Neutral);

    private void Update()
    {
        Look look = Looks[_current];
        float dt = Time.deltaTime;
        float pulse = Mathf.Sin(Time.time * look.pulseSpeed);

        _color = Color.Lerp(_color, look.color, dt * 5f);
        _intensity = Mathf.Lerp(_intensity, look.intensity, dt * 5f);

        if (_glow != null)
        {
            _glow.color = _color;
            _glow.intensity = _intensity * (1f + pulse * (0.1f + look.pulseAmount * 3f));
        }
        Color emission = _color * (0.6f + 0.4f * (pulse * 0.5f + 0.5f));
        foreach (var m in _materials)
        {
            if (m != null) m.SetColor("_EmissionColor", emission);
        }
    }

    private void BuildFace()
    {
        float top = transform.position.y;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<Canvas>() != null || r.GetComponent<TMP_Text>() != null) continue;
            top = Mathf.Max(top, r.bounds.max.y);
        }

        var go = new GameObject("EmotionFace");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(transform.position.x, top + _faceHeight, transform.position.z);
        float s = Mathf.Max(0.0001f, transform.lossyScale.x);
        go.transform.localScale = Vector3.one / s;
        go.AddComponent<Billboard>();

        var text = go.AddComponent<TextMeshPro>();
        text.richText = false;
        text.fontSize = 3f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.text = Looks[Emotion.Neutral].face;
        _face = text;
    }
}
