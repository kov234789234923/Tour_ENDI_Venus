// FilteredSocket.cs
using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FilteredSocket : XRSocketInteractor
{
    // пусто = любой предмет с InteractionRelay
    [SerializeField] private string[] _allowedObjectIds = Array.Empty<string>();

    public bool IsItemAllowed(IXRInteractable interactable)
    {
        var relay = (interactable as Component)?.GetComponentInParent<InteractionRelay>();
        if (relay == null) return false;
        return _allowedObjectIds.Length == 0 ||
            Array.IndexOf(_allowedObjectIds, relay.ObjectId) >= 0;
    }

    public override bool CanHover(IXRHoverInteractable interactable)
        => base.CanHover(interactable) && IsItemAllowed(interactable);

    public override bool CanSelect(IXRSelectInteractable interactable)
        => base.CanSelect(interactable) && IsItemAllowed(interactable);
}
