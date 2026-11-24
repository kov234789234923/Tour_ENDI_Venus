// VenusDirector.cs
// Режиссёр поверхности. Слушает StageManager и по ID этапа:
//   S01, S10 : аппарата, бура и планшета в мире нет;
//   S05      : аппарат «Венера-7» падает с неба;
//   S06      : затемнение, игрок оказывается у аппарата, появляются бур и планшет.
// Подсказок-надписей в мире нет. Кнопки «Забор грунта», «Отправить на Землю» и «Телепорт» живут на экране планшета
// (VenusTabletScreen); старые отдельные 3D-кнопки «Отправить» и «Телепорт» скрываются (у них те же id).
// Сам бур запускает StageSequencer (этап S07).
using System.Collections;
using UnityEngine;

public class VenusDirector : MonoBehaviour
{
    [Header("Этапы")]
    [SerializeField] private StageManager _stages;
    [SerializeField] private string _resetStage = "S01";
    [SerializeField] private string _endStage = "S10";
    [SerializeField] private string _dropStage = "S05";
    [SerializeField] private string _questStage = "S06";
    [SerializeField] private string _sendStage = "S08";
    [SerializeField] private string _teleportStage = "S09";

    [Header("ID объектов взаимодействия")]
    [SerializeField] private string _sendId = "btn_send";
    [SerializeField] private string _teleportId = "btn_teleport";

    [Header("Игрок и помощник")]
    [SerializeField] private Transform _player;
    [SerializeField] private Transform _assistant;
    [SerializeField] private ScreenFader _fader;
    [SerializeField] private Transform _questSpawn;
    [SerializeField] private Transform _assistantQuestPoint;

    [Header("Объекты поверхности")]
    [SerializeField] private GameObject _lander;
    [SerializeField] private GameObject _tablet;
    [SerializeField] private GameObject _drill;
    [SerializeField] private GameObject _tabletStand;
    [SerializeField] private GameObject _sendButton;        // старая 3D-кнопка, скрывается
    [SerializeField] private GameObject _teleportButton;    // старая 3D-кнопка, скрывается

    [Header("Падение аппарата")]
    [SerializeField] private float _dropHeight = 50f;
    [SerializeField] private float _gravity = 8.87f;       // на Венере
    [SerializeField] private float _maxFallSpeed = 16f;    // атмосфера и парашют тормозят
    [SerializeField] private float _fadeSeconds = 0.8f;

    private Vector3 _landerEnd;
    private Quaternion _landerRot;
    private Coroutine _drop;
    private Coroutine _move;

#if UNITY_EDITOR
    public void EditorSetup(StageManager stages, Transform player, Transform assistant, ScreenFader fader,
        Transform questSpawn, Transform assistantQuestPoint, GameObject lander, GameObject tablet,
        GameObject drill, GameObject tabletStand, GameObject sendButton, GameObject teleportButton)
    {
        _stages = stages;
        _player = player;
        _assistant = assistant;
        _fader = fader;
        _questSpawn = questSpawn;
        _assistantQuestPoint = assistantQuestPoint;
        _lander = lander;
        _tablet = tablet;
        _drill = drill;
        _tabletStand = tabletStand;
        _sendButton = sendButton;
        _teleportButton = teleportButton;
    }
#endif

    private void Awake()
    {
        int items = LayerMask.NameToLayer("Items");
        int playerLayer = LayerMask.NameToLayer("Player");
        if (items >= 0 && playerLayer >= 0) Physics.IgnoreLayerCollision(items, playerLayer, true);

        if (_lander != null)
        {
            _landerEnd = _lander.transform.position;
            _landerRot = _lander.transform.rotation;
        }
        FixSkinned(_drill);
        if (_tablet != null && _tablet.GetComponent<VenusTabletScreen>() == null)
            _tablet.AddComponent<VenusTabletScreen>();   // размер, хват за край и включающийся экран
        HideLegacyButtons();
        HideQuestObjects();
    }

    // Отдельные 3D-кнопки «Отправить» и «Телепорт» не нужны: обе функции на планшете. У них те же id (btn_send, btn_teleport),
    // поэтому оставленная кнопка могла бы завершить этап в обход планшета. StageManager включает их через Active Objects
    // этапов S08 и S09, поэтому прячем заново при входе на этап.
    private void HideLegacyButtons()
    {
        SetActive(_sendButton, false);
        SetActive(_teleportButton, false);
        foreach (var go in FindRelayObjects(_sendId)) go.SetActive(false);
        foreach (var go in FindRelayObjects(_teleportId)) go.SetActive(false);
    }

    // Ищем объекты с InteractionRelay по id через рефлексию: эта сборка не видит тип InteractionRelay.
    public static System.Collections.Generic.List<GameObject> FindRelayObjects(string objectId)
    {
        var result = new System.Collections.Generic.List<GameObject>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "InteractionRelay") continue;
            var prop = mb.GetType().GetProperty("ObjectId");
            if (prop != null && (prop.GetValue(mb) as string) == objectId) result.Add(mb.gameObject);
        }
        return result;
    }

    private void OnEnable()
    {
        if (_stages != null) _stages.StageEntered += OnStageEntered;
    }

    private void OnDisable()
    {
        if (_stages != null) _stages.StageEntered -= OnStageEntered;
    }

    private void OnStageEntered(StageData data)
    {
        if (data == null) return;
        string id = data.stageId;

        if (id == _resetStage || id == _endStage) ResetWorld();
        else if (id == _dropStage) DropLander();
        else if (id == _questStage) BeginQuest();

        if (id == _sendStage || id == _teleportStage) HideLegacyButtons();
    }

    // ---------- Мир ----------

    private void ResetWorld()
    {
        if (_drop != null) StopCoroutine(_drop);
        if (_move != null) StopCoroutine(_move);
        _drop = null;
        _move = null;
        HideQuestObjects();
        if (_lander != null) _lander.transform.SetPositionAndRotation(_landerEnd, _landerRot);
    }

    private void HideQuestObjects()
    {
        SetActive(_lander, false);
        SetActive(_tablet, false);
        SetActive(_tabletStand, false);
        SetActive(_drill, false);
    }

    // Этап S05: аппарат падает с неба и садится на землю.
    private void DropLander()
    {
        if (_lander == null) return;
        if (_drop != null) StopCoroutine(_drop);
        _drop = StartCoroutine(DropRoutine());
    }

    private IEnumerator DropRoutine()
    {
        Transform t = _lander.transform;
        _lander.SetActive(true);
        t.SetPositionAndRotation(_landerEnd + Vector3.up * _dropHeight, _landerRot);

        float speed = 0f;
        float time = 0f;
        while (t.position.y > _landerEnd.y)
        {
            time += Time.deltaTime;
            speed = Mathf.Min(speed + _gravity * Time.deltaTime, _maxFallSpeed);
            float remaining = t.position.y - _landerEnd.y;
            float brake = Mathf.Clamp01(remaining / 8f);              // у земли тормозные двигатели
            Vector3 p = t.position;
            p.y = Mathf.Max(_landerEnd.y, p.y - speed * Mathf.Lerp(0.35f, 1f, brake) * Time.deltaTime);
            t.position = p;
            t.rotation = _landerRot * Quaternion.Euler(Mathf.Sin(time * 1.3f) * 4f * brake, 0f, Mathf.Cos(time * 0.9f) * 4f * brake);
            yield return null;
        }

        foreach (float h in new[] { 0.3f, 0.1f })                    // два отскока после касания
        {
            float u = 0f;
            while (u < 1f)
            {
                u += Time.deltaTime / 0.35f;
                Vector3 p = _landerEnd;
                p.y += Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * h;
                t.position = p;
                yield return null;
            }
        }
        t.SetPositionAndRotation(_landerEnd, _landerRot);
        _drop = null;
    }

    // Этап S06: затемнение, игрок у аппарата, бур и планшет появляются.
    private void BeginQuest()
    {
        if (_move != null) StopCoroutine(_move);
        _move = StartCoroutine(BeginRoutine());
    }

    private IEnumerator BeginRoutine()
    {
        if (_fader != null) yield return _fader.FadeTo(1f, _fadeSeconds);

        if (_drop != null)
        {
            StopCoroutine(_drop);
            _drop = null;
        }
        if (_lander != null)
        {
            _lander.SetActive(true);
            _lander.transform.SetPositionAndRotation(_landerEnd, _landerRot);
        }
        Place(_player, _questSpawn);
        Place(_assistant, _assistantQuestPoint);
        SetActive(_drill, true);
        SetActive(_tabletStand, true);
        SetActive(_tablet, true);

        if (_fader != null) yield return _fader.FadeTo(0f, _fadeSeconds);
        _move = null;
    }

    // Бур был виден только под некоторыми углами: границы скиннинг-меша не обновлялись.
    public static void FixSkinned(GameObject root)
    {
        if (root == null) return;
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
        }
        foreach (var a in root.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    // ---------- Вспомогательное ----------

    private static void SetActive(GameObject go, bool value)
    {
        if (go != null) go.SetActive(value);
    }

    private static void Place(Transform target, Transform point)
    {
        if (target == null || point == null) return;
        var cc = target.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        target.SetPositionAndRotation(point.position, point.rotation);
        if (cc != null) cc.enabled = true;
    }
}
