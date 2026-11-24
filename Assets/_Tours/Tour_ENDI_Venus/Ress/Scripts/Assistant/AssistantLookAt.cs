// AssistantLookAt.cs
// Помощник плавно поворачивается лицом к игроку (только по горизонтали).
using UnityEngine;

public class AssistantLookAt : MonoBehaviour
{
    [SerializeField] private float _turnSpeed = 6f;
    [Tooltip("Если у модели перёд в другую сторону, поверните на нужный угол (например 180)")]
    [SerializeField] private float _yawOffset = 0f;

    private Transform _camera;

    private void LateUpdate()
    {
        if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        if (_camera == null) return;

        Vector3 direction = _camera.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion target = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, _yawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
    }
}
