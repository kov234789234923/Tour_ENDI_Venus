// ReturnToHome.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable), typeof(Rigidbody))]
public class ReturnToHome : MonoBehaviour
{
    [SerializeField] private float _dropDepth = 0.5f;    // насколько ниже старта считается падением, м
    [SerializeField] private float _maxDistance = 8f;    // дальше этого расстояния от старта предмет потерян, м
    [SerializeField] private float _delay = 2f;          // сколько секунд ждать перед возвратом

    private XRBaseInteractable _interactable;
    private Rigidbody _rb;
    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _timer;

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();
        _rb = GetComponent<Rigidbody>();
        _homePos = transform.position;
        _homeRot = transform.rotation;
    }

    private void Update()
    {
        if (_interactable.isSelected)
        {
            _timer = 0f;
            return;
        }

        bool lost = transform.position.y < _homePos.y - _dropDepth
            || (transform.position - _homePos).sqrMagnitude > _maxDistance * _maxDistance;
        if (!lost)
        {
            _timer = 0f;
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _delay) Respawn();
    }

    public void Respawn()
    {
        _timer = 0f;
        _rb.linearVelocity = Vector3.zero;   // в Unity 6 вместо velocity
        _rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(_homePos, _homeRot);
    }
}
