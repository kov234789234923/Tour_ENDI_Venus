// Billboard.cs
using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform _cam;

    private void LateUpdate()
    {
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        if (_cam == null) return;

        Vector3 dir = transform.position - _cam.position;
        dir.y = 0f;                                   // не наклоняем панель
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
