// HighlightTarget.cs
using UnityEngine;

public class HighlightTarget : MonoBehaviour
{
    [SerializeField] private string _id;                   // "hl_venus_tablet"
    [SerializeField] private Material _outlineMaterial;    // материал подсветки из готовых ассетов
    [SerializeField] private Renderer[] _renderers;        // пусто = все дочерние

    private Material[][] _original;

    public string Id => _id;
    public bool IsOn { get; private set; }

    private void Awake()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>();
        _original = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _original[i] = _renderers[i].sharedMaterials;
    }

    private void OnEnable() => HighlightRegistry.Register(this);

    private void OnDisable()
    {
        SetHighlight(false);                  // выключенный объект не должен «залипнуть»
        HighlightRegistry.Unregister(this);
    }

    public void SetHighlight(bool on)
    {
        if (on == IsOn || _outlineMaterial == null || _original == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (on)
            {
                var src = _original[i];
                var withOutline = new Material[src.Length + 1];
                src.CopyTo(withOutline, 0);
                withOutline[src.Length] = _outlineMaterial;
                _renderers[i].sharedMaterials = withOutline;
            }
            else
            {
                _renderers[i].sharedMaterials = _original[i];
            }
        }
        IsOn = on;
    }
}
