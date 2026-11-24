// InteractionEvent.cs
using UnityEngine;

public enum InteractionKind
{
    Grabbed, Released, Activated, Pressed,
    SocketPlaced, SocketRemoved, SocketRejected
}

public readonly struct InteractionEvent
{
    public readonly InteractionKind kind;
    public readonly string objectId;   // с чем действие: "item_test_tube_1"
    public readonly string targetId;   // куда: ID гнезда, иначе пусто
    public readonly float time;

    public InteractionEvent(InteractionKind kind, string objectId, string targetId = "")
    {
        this.kind = kind;
        this.objectId = objectId;
        this.targetId = targetId;
        time = Time.time;
    }

    public override string ToString() => kind switch
    {
        InteractionKind.Grabbed => $"взят предмет {objectId}",
        InteractionKind.Released => $"предмет {objectId} отпущен",
        InteractionKind.Activated => $"предмет {objectId} активирован (курок)",
        InteractionKind.Pressed => $"нажато {objectId}",
        InteractionKind.SocketPlaced => $"предмет {objectId} в гнезде {targetId}",
        InteractionKind.SocketRemoved => $"предмет {objectId} вынут из гнезда {targetId}",
        InteractionKind.SocketRejected => $"предмет {objectId} не подходит к гнезду {targetId}",
        _ => kind.ToString()
    };
}
