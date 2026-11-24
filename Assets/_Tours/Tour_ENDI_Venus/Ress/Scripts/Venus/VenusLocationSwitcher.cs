// VenusLocationSwitcher.cs
// Переключает две локации сценария: тёмная каюта с круглым иллюминатором и поверхность Венеры.
// Вызывается из StageManager через onEnter (UnityEvent), сам StageManager о каюте ничего не знает.
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class VenusLocationSwitcher : MonoBehaviour
{
    [Header("Игрок и помощник")]
    [SerializeField] private Transform _player;
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _assistant;
    [SerializeField] private ScreenFader _fader;

    [Header("Каюта")]
    [SerializeField] private GameObject _introRoot;
    [SerializeField] private GameObject _outsideSpace;
    [SerializeField] private GameObject _outsideDescent;
    [SerializeField] private Transform _introSpawn;
    [SerializeField] private Transform _introAssistantPoint;

    [Header("Помощник в каюте (чтобы не закрывать иллюминатор и был виден текст)")]
    [SerializeField] private float _introAssistantLowerBy = 0.9f;      // на сколько опустить робота относительно точки, м
    [SerializeField] private float _introAssistantScale = 0.6f;        // во сколько раз уменьшить робота
    [SerializeField] private float _introSubtitleScale = 0.0012f;      // масштаб панели субтитров в каюте
    [SerializeField] private float _introSubtitleGap = 0.08f;          // зазор над роботом в каюте, м

    [Header("Поверхность")]
    [SerializeField] private GameObject _surfaceRoot;
    [SerializeField] private GameObject _sun;
    [SerializeField] private Transform _surfaceSpawn;
    [SerializeField] private Transform _surfaceAssistantPoint;

    [Header("Атмосфера поверхности (в каюте всё чёрное)")]
    [SerializeField] private Material _surfaceSkybox;
    [SerializeField] private Color _fogColor = new Color(0.648f, 0.598f, 0.304f);
    [SerializeField] private float _fogDensity = 0.015f;
    [SerializeField] private Color _ambientLight = new Color(0.698f, 0.628f, 0.226f);

    [Header("Спуск через облака")]
    [SerializeField] private float _descentSeconds = 4f;
    [SerializeField] private float _descentSpeed = 25f;
    [SerializeField] private float _fadeSeconds = 0.8f;

    [Header("Восход Солнца")]
    [SerializeField] private float _sunriseSeconds = 8f;
    [SerializeField, Range(0f, 1f)] private float _sunStartFactor = 0f;
    [SerializeField, Range(0f, 1f)] private float _nightFactor = 0.04f;   // освещённость поверхности до восхода: почти темно

    // Игрок высадился на поверхность (на него подписан StageSequencer).
    public event System.Action SurfaceReached;

    private Vector3[] _descentStart;
    private Coroutine _descent;
    private Coroutine _sunrise;
    private Light _sunLight;
    private float _sunTargetIntensity = 0.6f;
    private Quaternion _sunTargetRotation;
    private Vector3 _assistantBaseScale = Vector3.one;
    private SubtitleFollow _subtitle;

#if UNITY_EDITOR
    public void EditorSetup(Transform player, Camera cam, Transform assistant, ScreenFader fader,
        GameObject introRoot, GameObject outsideSpace, GameObject outsideDescent,
        Transform introSpawn, Transform introAssistantPoint,
        GameObject surfaceRoot, GameObject sun, Transform surfaceSpawn, Transform surfaceAssistantPoint,
        Material surfaceSkybox)
    {
        _player = player;
        _camera = cam;
        _assistant = assistant;
        _fader = fader;
        _introRoot = introRoot;
        _outsideSpace = outsideSpace;
        _outsideDescent = outsideDescent;
        _introSpawn = introSpawn;
        _introAssistantPoint = introAssistantPoint;
        _surfaceRoot = surfaceRoot;
        _sun = sun;
        _surfaceSpawn = surfaceSpawn;
        _surfaceAssistantPoint = surfaceAssistantPoint;
        _surfaceSkybox = surfaceSkybox;
    }
#endif

    private void Awake()
    {
        if (_assistant != null)
        {
            _assistantBaseScale = _assistant.localScale;
            _subtitle = _assistant.GetComponentInChildren<SubtitleFollow>(true);
        }

        if (_sun != null)
        {
            _sunLight = _sun.GetComponent<Light>();
            if (_sunLight != null) _sunTargetIntensity = _sunLight.intensity;
            _sunTargetRotation = _sun.transform.rotation;
        }

        if (_outsideDescent != null)
        {
            var layers = _outsideDescent.transform;
            _descentStart = new Vector3[layers.childCount];
            for (int i = 0; i < layers.childCount; i++) _descentStart[i] = layers.GetChild(i).localPosition;
        }
    }

    // Стартовый экран: космос и планета в круглом иллюминаторе, в каюте нет света.
    public void EnterIntro()
    {
        StopDescent();
        StopSunrise();
        Show(_introRoot, true);
        Show(_surfaceRoot, false);
        Show(_sun, false);
        Show(_outsideSpace, true);
        Show(_outsideDescent, false);

        RenderSettings.fog = false;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        if (_camera != null)
        {
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
        }
        if (_fader != null) _fader.SetAlpha(0f);

        MovePlayer(_introSpawn, _introAssistantPoint, true);
    }

    // Высадка: мгновенный переход на поверхность без спуска.
    public void EnterSurface()
    {
        StopDescent();
        ApplySurface();
    }

    // Без StopDescent: вызывается изнутри самого падения, и там StopDescent остановил бы его самое (был чёрный экран).
    private void ApplySurface()
    {
        Show(_introRoot, false);
        Show(_outsideSpace, false);
        Show(_outsideDescent, false);
        Show(_surfaceRoot, true);
        Show(_sun, true);
        SetSunFactor(_sunStartFactor);

        // Сначала на поверхности темно: только робот со своим светом. Небо и свет приходят с восходом.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = _fogDensity;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.skybox = null;
        ApplyAtmosphere(_sunStartFactor);
        if (_camera != null) _camera.clearFlags = CameraClearFlags.SolidColor;

        MovePlayer(_surfaceSpawn, _surfaceAssistantPoint, false);
    }

    // Затемнение, падение сквозь облака в окне, затемнение, высадка на поверхность.
    public void EnterSurfaceWithDescent()
    {
        StopDescent();
        _descent = StartCoroutine(DescentRoutine());
    }

    // Над планетой медленно восходит Солнце (еле видно).
    public void RaiseSun()
    {
        StopSunrise();
        _sunrise = StartCoroutine(SunriseRoutine());
    }

    private IEnumerator DescentRoutine()
    {
        yield return Fade(1f);                       // чёрный экран
        Show(_outsideSpace, false);
        Show(_outsideDescent, true);
        yield return Fade(0f);                       // видим падение в окне

        float t = 0f;
        while (t < _descentSeconds)
        {
            t += Time.deltaTime;
            if (_outsideDescent != null)
            {
                var layers = _outsideDescent.transform;
                for (int i = 0; i < layers.childCount; i++)
                    layers.GetChild(i).localPosition += Vector3.back * (_descentSpeed * Time.deltaTime);
            }
            yield return null;
        }

        yield return Fade(1f);                       // снова чёрный экран
        ResetDescent();
        ApplySurface();
        yield return Fade(0f);                       // игрок уже на планете

        _descent = null;
        SurfaceReached?.Invoke();
    }

    private IEnumerator SunriseRoutine()
    {
        float t = 0f;
        while (t < _sunriseSeconds)
        {
            t += Time.deltaTime;
            SetSunFactor(Mathf.Lerp(_sunStartFactor, 1f, t / _sunriseSeconds));
            yield return null;
        }
        SetSunFactor(1f);
        RenderSettings.skybox = _surfaceSkybox;
        if (_camera != null) _camera.clearFlags = CameraClearFlags.Skybox;
        _sunrise = null;
    }

    // Туман, ambient и цвет фона растут от «ночи» к дневному жёлтому цвету Венеры.
    private void ApplyAtmosphere(float factor)
    {
        float k = Mathf.Lerp(_nightFactor, 1f, factor);
        Color fog = _fogColor * k;
        RenderSettings.fogColor = fog;
        RenderSettings.ambientLight = _ambientLight * k;
        if (_camera != null && _camera.clearFlags == CameraClearFlags.SolidColor) _camera.backgroundColor = fog;
    }

    private void SetSunFactor(float factor)
    {
        ApplyAtmosphere(factor);
        if (_sun == null) return;
        if (_sunLight != null) _sunLight.intensity = _sunTargetIntensity * factor;
        // Солнце поднимается из-за горизонта: наклон растёт от 5 градусов до заданного.
        Vector3 target = _sunTargetRotation.eulerAngles;
        float pitch = Mathf.Lerp(5f, target.x, factor);
        _sun.transform.rotation = Quaternion.Euler(pitch, target.y, target.z);
    }

    private IEnumerator Fade(float target)
    {
        if (_fader != null) yield return _fader.FadeTo(target, _fadeSeconds);
    }

    private void StopDescent()
    {
        if (_descent != null) StopCoroutine(_descent);
        _descent = null;
        ResetDescent();
    }

    private void StopSunrise()
    {
        if (_sunrise != null) StopCoroutine(_sunrise);
        _sunrise = null;
    }

    private void ResetDescent()
    {
        if (_outsideDescent == null || _descentStart == null) return;
        var layers = _outsideDescent.transform;
        for (int i = 0; i < layers.childCount && i < _descentStart.Length; i++)
            layers.GetChild(i).localPosition = _descentStart[i];
    }

    private void MovePlayer(Transform spawn, Transform assistantPoint, bool intro)
    {
        if (_player != null && spawn != null)
        {
            var body = _player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;   // иначе CharacterController вернёт старую позицию
            _player.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (body != null) body.enabled = true;
        }
        if (_assistant != null && assistantPoint != null)
        {
            // В каюте робот ниже и меньше: иллюминатор остаётся открытым, субтитры над роботом видны целиком.
            Vector3 pos = assistantPoint.position;
            if (intro) pos += Vector3.down * _introAssistantLowerBy;
            _assistant.SetPositionAndRotation(pos, assistantPoint.rotation);
            _assistant.localScale = intro ? _assistantBaseScale * _introAssistantScale : _assistantBaseScale;
        }

        if (_subtitle != null)
        {
            if (intro) _subtitle.SetProfile(_introSubtitleScale, _introSubtitleGap);
            else _subtitle.ResetProfile();
        }
    }

    private static void Show(GameObject go, bool value)
    {
        if (go != null) go.SetActive(value);
    }
}
