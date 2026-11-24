// VenusPlayerBody.cs
// Тело игрока: CharacterController подгоняется под рост (положение головы в VR), есть гравитация,
// игрок не проходит сквозь землю и предметы. Телепорт и перенос между локациями поддерживаются.
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VenusPlayerBody : MonoBehaviour
{
    [SerializeField] private Camera _head;
    [SerializeField] private float _radius = 0.25f;
    [SerializeField] private float _minHeight = 0.6f;
    [SerializeField] private float _maxHeight = 2.2f;
    [SerializeField] private float _gravity = 8.87f;
    [SerializeField] private float _killY = -30f;

    private CharacterController _controller;
    private Vector3 _lastPosition;
    private Vector3 _startPosition;
    private float _verticalSpeed;
    private bool _driverPresent;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _controller.radius = _radius;
        _controller.stepOffset = 0.3f;
        _controller.slopeLimit = 50f;
        _controller.skinWidth = 0.03f;
        foreach (var c in GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "CharacterControllerDriver") _driverPresent = true;
        }
        _startPosition = transform.position;
        _lastPosition = transform.position;
    }

    private void Update()
    {
        if (_head == null) _head = GetComponentInChildren<Camera>();

        // Внешний перенос (телепорт, смена локации): CharacterController надо перезапустить, иначе он вернёт позицию.
        if ((transform.position - _lastPosition).sqrMagnitude > 0.0004f)
        {
            _controller.enabled = false;
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }

        if (!_driverPresent) FitToHead();

        if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -1f;
        else _verticalSpeed -= _gravity * Time.deltaTime;
        _controller.Move(Vector3.up * (_verticalSpeed * Time.deltaTime));

        if (transform.position.y < _killY)
        {
            _controller.enabled = false;
            transform.position = _startPosition;
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }
        _lastPosition = transform.position;
    }

    private void FitToHead()
    {
        if (_head == null) return;
        Vector3 local = transform.InverseTransformPoint(_head.transform.position);
        float height = Mathf.Clamp(local.y + 0.1f, _minHeight, _maxHeight);
        _controller.height = height;
        _controller.center = new Vector3(local.x, height * 0.5f + _controller.skinWidth, local.z);
    }
}
