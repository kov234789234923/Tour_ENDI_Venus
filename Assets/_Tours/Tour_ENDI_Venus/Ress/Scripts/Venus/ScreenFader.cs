// ScreenFader.cs
// Затемнение экрана для VR: чёрный прозрачный квад перед камерой (Screen Space Overlay в VR не работает).
using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private string _colorProperty = "_BaseColor";

    private Material _material;
    private float _alpha;

    public float Alpha => _alpha;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        _material = _renderer.material;
        SetAlpha(0f);
    }

    public void SetAlpha(float value)
    {
        _alpha = Mathf.Clamp01(value);
        if (_material == null) return;
        Color c = _material.GetColor(_colorProperty);
        c.a = _alpha;
        _material.SetColor(_colorProperty, c);
        _renderer.enabled = _alpha > 0.001f;   // невидимый квад не рисуем
    }

    public IEnumerator FadeTo(float target, float seconds)
    {
        float start = _alpha;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(start, target, t / seconds));
            yield return null;
        }
        SetAlpha(target);
    }
}
