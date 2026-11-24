// ToggleInteractable.cs
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class ToggleInteractable : MonoBehaviour
{
    [SerializeField] private Transform _visual;                       // что поворачивать (ручка рычага)
    [SerializeField] private Vector3 _offEuler;
    [SerializeField] private Vector3 _onEuler = new Vector3(-60f, 0f, 0f);
    [SerializeField] private float _speed = 8f;
    [SerializeField] private bool _startOn;

    public UnityEvent<bool> onChanged;

    public bool IsOn { get; private set; }

    private XRSimpleInteractable _interactable;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        IsOn = _startOn;
        if (_visual != null) _visual.localRotation = Quaternion.Euler(IsOn ? _onEuler : _offEuler);
    }

    private void OnEnable() => _interactable.selectEntered.AddListener(OnSelect);

    private void OnDisable() => _interactable.selectEntered.RemoveListener(OnSelect);

    private void OnSelect(SelectEnterEventArgs _) => Set(!IsOn);

    public void Set(bool on)
    {
        if (on == IsOn) return;
        IsOn = on;
        onChanged?.Invoke(IsOn);
    }

    private void Update()
    {
        if (_visual == null) return;
        var target = Quaternion.Euler(IsOn ? _onEuler : _offEuler);
        _visual.localRotation = Quaternion.Slerp(_visual.localRotation, target,
            1f - Mathf.Exp(-_speed * Time.deltaTime));
    }
}
