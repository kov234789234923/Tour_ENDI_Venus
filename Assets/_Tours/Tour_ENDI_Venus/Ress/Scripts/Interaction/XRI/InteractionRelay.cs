// InteractionRelay.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRBaseInteractable))]
public class InteractionRelay : MonoBehaviour
{
    [SerializeField] private string _objectId;

    private XRBaseInteractable _interactable;
    private bool _isGrab;

    public string ObjectId => _objectId;
    public Component LastHolder { get; private set; }   // рука, которая держала последней

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();
        _isGrab = _interactable is XRGrabInteractable;
    }

    private void OnEnable()
    {
        _interactable.selectEntered.AddListener(OnSelectEntered);
        _interactable.selectExited.AddListener(OnSelectExited);
        _interactable.activated.AddListener(OnActivated);
    }

    private void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        _interactable.selectExited.RemoveListener(OnSelectExited);
        _interactable.activated.RemoveListener(OnActivated);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        LastHolder = args.interactorObject as Component;
        Haptics.Select(LastHolder);
        Raise(_isGrab ? InteractionKind.Grabbed : InteractionKind.Pressed);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        if (_isGrab) Raise(InteractionKind.Released);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        LastHolder = args.interactorObject as Component;
        Raise(InteractionKind.Activated);
    }

    private void Raise(InteractionKind kind)
        => InteractionBus.Raise(new InteractionEvent(kind, _objectId));
}
