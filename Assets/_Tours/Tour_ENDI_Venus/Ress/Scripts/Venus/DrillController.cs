// DrillController.cs
// Бур стоит неподвижно, пока не вызван Play(). Тогда проигрывается его анимация (Skeleton_Drill_Work).
using UnityEngine;

public class DrillController : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null) _animator.speed = 0f;   // стоит в начальной позе
    }

    public void Play()
    {
        if (_animator != null) _animator.speed = 1f;
    }
}
