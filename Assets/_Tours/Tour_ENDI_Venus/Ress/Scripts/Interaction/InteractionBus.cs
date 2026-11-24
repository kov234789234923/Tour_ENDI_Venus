// InteractionBus.cs
using System;
using UnityEngine;

public static class InteractionBus
{
    public static event Action<InteractionEvent> Raised;

    // При выключенной перезагрузке домена подписчики пережили бы остановку Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Raised = null;

    public static void Raise(InteractionEvent e) => Raised?.Invoke(e);
}
