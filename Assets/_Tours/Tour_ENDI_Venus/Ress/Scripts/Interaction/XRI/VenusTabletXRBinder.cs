// VenusTabletXRBinder.cs
// Подключает кнопки на экране планшета (VenusTabletScreen) к рукам XR: на каждую кнопку добавляется
// XRSimpleInteractable (нажатие лучом или выбором) и XRPokeFilter (нажатие пальцем).
// Создаётся сам при запуске сцены и раз в полсекунды ищет планшет, у которого кнопки ещё не подключены.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VenusTabletXRBinder : MonoBehaviour
{
    private const string PokeFilterType = "UnityEngine.XR.Interaction.Toolkit.Filters.XRPokeFilter";

    private float _next;
    private readonly List<KeyValuePair<string, GameObject>> _buttons = new List<KeyValuePair<string, GameObject>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<VenusTabletXRBinder>() != null) return;
        new GameObject("VenusTabletXRBinder").AddComponent<VenusTabletXRBinder>();
    }

    private void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;

        foreach (var screen in FindObjectsByType<VenusTabletScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (screen.XrBound || !screen.IsReady) continue;
            Bind(screen);
        }
    }

    private void Bind(VenusTabletScreen screen)
    {
        _buttons.Clear();
        screen.GetButtons(_buttons);
        var pokeType = FindType(PokeFilterType);

        foreach (var pair in _buttons)
        {
            string id = pair.Key;
            GameObject go = pair.Value;
            if (go == null || go.GetComponent<XRSimpleInteractable>() != null) continue;

            var simple = go.AddComponent<XRSimpleInteractable>();
            simple.selectEntered.AddListener(args =>
            {
                var hand = args.interactorObject as Component;
                if (screen.PressButton(id, out string reason))
                {
                    Haptics.Select(hand);
                }
                else
                {
                    Haptics.Error(hand);
                    Debug.LogWarning("[TabletXR] " + reason, screen);
                }
            });

            if (pokeType == null) continue;
            try
            {
                var filter = go.AddComponent(pokeType);
                var property = pokeType.GetProperty("pokeCollider");
                if (property != null && property.CanWrite) property.SetValue(filter, go.GetComponent<Collider>());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TabletXR] Не удалось добавить XRPokeFilter на " + go.name + ": " + e.Message, go);
            }
        }
        screen.XrBound = true;
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = assembly.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
