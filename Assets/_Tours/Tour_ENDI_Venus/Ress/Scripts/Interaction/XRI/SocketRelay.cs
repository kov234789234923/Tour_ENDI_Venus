// SocketRelay.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(FilteredSocket))]
public class SocketRelay : MonoBehaviour
{
    [SerializeField] private string _socketId;
    [SerializeField] private float _rejectCooldown = 1f;

    private FilteredSocket _socket;
    private float _lastReject = -10f;

    private void Awake() => _socket = GetComponent<FilteredSocket>();

    private void OnEnable()
    {
        _socket.selectEntered.AddListener(OnPlaced);
        _socket.selectExited.AddListener(OnRemoved);
    }

    private void OnDisable()
    {
        _socket.selectEntered.RemoveListener(OnPlaced);
        _socket.selectExited.RemoveListener(OnRemoved);
    }

    private void OnPlaced(SelectEnterEventArgs args)
    {
        var relay = RelayOf(args.interactableObject);
        Haptics.Success(relay != null ? relay.LastHolder : null);
        Raise(InteractionKind.SocketPlaced, args.interactableObject);
    }

    private void OnRemoved(SelectExitEventArgs args)
        => Raise(InteractionKind.SocketRemoved, args.interactableObject);

    // Предмет в руке коснулся зоны гнезда, но не подходит: отказ + «ошибочная» вибрация.
    private void OnTriggerEnter(Collider other)
    {
        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null || !grab.isSelected || _socket.IsItemAllowed(grab)) return;
        if (Time.time - _lastReject < _rejectCooldown) return;
        _lastReject = Time.time;
        var relay = RelayOf(grab);
        Haptics.Error(relay != null ? relay.LastHolder : null);
        Raise(InteractionKind.SocketRejected, grab);
    }

    private void Raise(InteractionKind kind, IXRInteractable item)
    {
        var relay = RelayOf(item);
        string id = relay != null ? relay.ObjectId : (item as Component)?.name ?? "?";
        InteractionBus.Raise(new InteractionEvent(kind, id, _socketId));
    }

    private static InteractionRelay RelayOf(IXRInteractable item)
        => (item as Component)?.GetComponentInParent<InteractionRelay>();
}
