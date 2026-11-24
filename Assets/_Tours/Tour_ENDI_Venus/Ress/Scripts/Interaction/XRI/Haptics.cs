// Haptics.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public static class Haptics
{
    // interactor: рука (компонент интерактора), от которой ищем контроллер
    public static void Pulse(Component interactor, float amplitude, float duration)
    {
        if (interactor == null) return;
        var player = interactor.GetComponentInParent<HapticImpulsePlayer>();
        if (player != null) player.SendHapticImpulse(amplitude, duration);
    }

    public static void Select(Component interactor) => Pulse(interactor, 0.25f, 0.06f);    // взял, нажал
    public static void Success(Component interactor) => Pulse(interactor, 0.50f, 0.12f);   // встало в гнездо
    public static void Error(Component interactor) => Pulse(interactor, 0.90f, 0.35f);     // не подходит
}
