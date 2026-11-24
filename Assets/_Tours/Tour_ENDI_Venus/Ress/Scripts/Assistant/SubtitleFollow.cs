// SubtitleFollow.cs
// Субтитры держатся НАД роботом-помощником и всегда повёрнуты к игроку.
// Вешается на SubtitleCanvas (дочерний объект помощника). Заменяет Billboard (убирается при сборке).
// Размер панели в мире не зависит от масштаба робота: если робота уменьшили, текст остаётся читаемым.
using UnityEngine;

public class SubtitleFollow : MonoBehaviour
{
    [SerializeField] private float _worldScale = 0.0018f;   // масштаб канваса в мире (1000 x 420 -> 1,8 x 0,76 м)
    [SerializeField] private float _gap = 0.12f;            // зазор между верхом робота и низом панели, м
    [SerializeField] private float _smooth = 10f;           // плавность движения

    private Transform _camera;
    private Transform _robot;
    private Renderer[] _renderers;
    private RectTransform _rect;

    private float _currentScale;
    private float _currentGap;
    private float _defaultScale;
    private float _defaultGap;
    private bool _snap = true;

    private void Awake()
    {
        _robot = transform.parent;
        _rect = transform as RectTransform;
        _defaultScale = _worldScale;
        _defaultGap = _gap;
        _currentScale = _worldScale;
        _currentGap = _gap;
        if (_robot != null) _renderers = _robot.GetComponentsInChildren<Renderer>(true);
    }

    // Профиль задаёт локация: в каюте с иллюминатором панель меньше и ближе к роботу.
    public void SetProfile(float worldScale, float gap)
    {
        _currentScale = worldScale;
        _currentGap = gap;
        _snap = true;
    }

    public void ResetProfile() => SetProfile(_defaultScale, _defaultGap);

    private void LateUpdate()
    {
        if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        if (_camera == null) return;

        // Размер в мировых единицах: компенсируем масштаб родителя (робот может быть уменьшен).
        float parentScale = _robot != null ? Mathf.Max(0.0001f, _robot.lossyScale.y) : 1f;
        transform.localScale = Vector3.one * (_currentScale / parentScale);

        float halfHeight = _rect != null ? _rect.sizeDelta.y * _currentScale * 0.5f : 0.2f;
        Vector3 anchor = _robot != null ? _robot.position : transform.position;
        Vector3 target = new Vector3(anchor.x, RobotTop() + _currentGap + halfHeight, anchor.z);

        float k = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        if (_snap || (transform.position - target).sqrMagnitude > 4f)
        {
            transform.position = target;   // робота телепортировали: не «ездим» за ним через всю сцену
            _snap = false;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, target, k);
        }

        // Канвас смотрит в +Z, поэтому разворот от камеры. Только по горизонтали.
        Vector3 away = transform.position - _camera.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(away);
    }

    private float RobotTop()
    {
        float top = _robot != null ? _robot.position.y : transform.position.y;
        if (_renderers == null) return top;
        bool found = false;
        float max = float.MinValue;
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            max = Mathf.Max(max, r.bounds.max.y);
            found = true;
        }
        return found ? max : top;
    }
}
