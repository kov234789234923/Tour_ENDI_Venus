using UnityEngine;

/// <summary>Independent screen expression; shares the atlas material across bots.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class NeuroScreenEmotion : MonoBehaviour
{
    public enum Emotion
    {
        Neutral, Happy, Laugh, Wink, Curious, Surprised, Thinking, Confused,
        Sad, Worried, Angry, Sleepy, Sleeping, Love, Excited, Offline
    }

    [SerializeField] Renderer screenRenderer;
    [SerializeField] Emotion emotion;
    [SerializeField] Vector2 uvCenter = new Vector2(0.500035f, 0.50681f);
    [SerializeField, Min(0.001f)] float uvSpan = 0.99191f;
    [SerializeField] bool mirrorX;
    [SerializeField] bool flipY;
    [Tooltip("Preview all expressions in Play mode.")]
    [SerializeField] bool autoCycle;
    [SerializeField, Min(0.1f)] float cycleSeconds = 1.5f;

    [Header("Transitions")]
    [SerializeField] bool animateTransitions = true;
    [SerializeField, Min(0.01f)] float blinkCloseSeconds = 0.09f;
    [SerializeField, Min(0.01f)] float blinkOpenSeconds = 0.14f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 0.4f;
    [SerializeField, Min(0.01f)] float fadeInSeconds = 0.6f;
    [SerializeField, Range(0f, 1f)] float sleepingBrightness = 0.45f;

    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int ScreenState = Shader.PropertyToID("_ScreenState");
    static readonly int EyeLayout = Shader.PropertyToID("_EyeLayout");
    // Eye/mouth gaps and eye centers measured from the common reference atlas (cell-local V).
    // Every skin preserves this layout; using per-expression gaps avoids cutting into a smile.
    static readonly Vector2[] EyeLayouts = {
        new Vector2(.3710f,.5016f), new Vector2(.4061f,.5191f), new Vector2(.4188f,.5271f), new Vector2(.3822f,.5159f),
        new Vector2(.4361f,.5671f), new Vector2(.4521f,.5751f), new Vector2(.4441f,.5767f), new Vector2(.4121f,.6006f),
        new Vector2(.4649f,.5703f), new Vector2(.4185f,.5415f), new Vector2(.4185f,.5863f), new Vector2(.4521f,.5367f),
        new Vector2(.4475f,.5334f), new Vector2(.4729f,.5987f), new Vector2(.4825f,.6274f), new Vector2(.4904f,.5748f)
    };
    enum Phase { Idle, BlinkClose, BlinkOpen, FadeOut, FadeIn }
    MaterialPropertyBlock properties;
    bool dirty = true;
    float nextCycle;
    Emotion displayedEmotion;
    Phase phase;
    float elapsed, duration, fromOpen, fromBrightness;
    float openness = 1f, brightness = 1f;

    public Emotion CurrentEmotion => emotion;
    public Emotion DisplayedEmotion => displayedEmotion;
    public bool IsTransitioning => phase != Phase.Idle;
    public float EyeOpenness => openness;
    public float Brightness => brightness;
    public Renderer ScreenRenderer => screenRenderer;
    public bool AutoCycle { get => autoCycle; set { autoCycle = value; nextCycle = Time.unscaledTime + cycleSeconds; } }

    public void Configure(Renderer target, Vector2 center, float span)
    {
        screenRenderer = target;
        uvCenter = center;
        uvSpan = Mathf.Max(span, 0.001f);
        Apply();
    }

    public void SetEmotion(Emotion value) => SetEmotion((int)value);
    public void SetEmotion(int index)
    {
        var target = (Emotion)Mathf.Clamp(index, 0, 15);
        dirty = false;
        if (!animateTransitions || !isActiveAndEnabled || screenRenderer == null ||
            screenRenderer.sharedMaterial == null || !screenRenderer.sharedMaterial.HasProperty(ScreenState))
        { emotion = target; Apply(); return; }
        if (target == emotion && IsTransitioning) { WriteProperties(); return; }
        emotion = target;
        if (!IsTransitioning && displayedEmotion == emotion) { brightness = RestingBrightness(emotion); WriteProperties(); return; }
        // A new request takes over from the current visual state, never from an old coroutine.
        bool fade = IsDimmed(displayedEmotion) || IsDimmed(emotion) || phase == Phase.FadeOut || phase == Phase.FadeIn;
        Begin(fade ? Phase.FadeOut : Phase.BlinkClose);
        WriteProperties();
    }

    public void SetEmotionImmediate(Emotion value) => SetEmotionImmediate((int)value);
    public void SetEmotionImmediate(int index) { emotion = (Emotion)Mathf.Clamp(index, 0, 15); Apply(); }

    static bool IsDimmed(Emotion value) => value == Emotion.Sleeping || value == Emotion.Offline;
    float RestingBrightness(Emotion value) => value == Emotion.Offline ? 0f : value == Emotion.Sleeping ? sleepingBrightness : 1f;

    void Begin(Phase value)
    {
        phase = value;
        elapsed = 0f;
        fromOpen = openness;
        fromBrightness = brightness;
        switch (value)
        {
            case Phase.BlinkClose: duration = blinkCloseSeconds * openness; break;
            case Phase.BlinkOpen: duration = blinkOpenSeconds * (1f - openness); break;
            case Phase.FadeOut: duration = brightness > 0.001f ? fadeOutSeconds : 0.001f; break;
            default: duration = fadeInSeconds; break;
        }
        duration = Mathf.Max(0.001f, duration);
    }

    /// <summary>Advance in unscaled seconds. Update drives this in Play; the editor preview drives it outside Play.</summary>
    public void AdvanceTransition(float deltaSeconds)
    {
        if (!IsTransitioning || deltaSeconds <= 0f) return;
        float remaining = deltaSeconds;
        for (int step = 0; step < 4 && IsTransitioning && remaining > 0f; step++)
        {
            float consumed = Mathf.Min(remaining, duration - elapsed);
            elapsed += consumed;
            remaining -= consumed;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            switch (phase)
            {
                case Phase.BlinkClose: openness = Mathf.Lerp(fromOpen, 0f, t); break;
                case Phase.BlinkOpen: openness = Mathf.Lerp(fromOpen, 1f, t); break;
                case Phase.FadeOut: brightness = Mathf.Lerp(fromBrightness, 0f, t); break;
                case Phase.FadeIn: brightness = Mathf.Lerp(fromBrightness, RestingBrightness(emotion), t); break;
            }
            if (elapsed < duration) break;
            if (phase == Phase.BlinkClose)
            {
                displayedEmotion = emotion;
                openness = 0f;
                Begin(Phase.BlinkOpen);
            }
            else if (phase == Phase.FadeOut)
            {
                displayedEmotion = emotion;
                openness = 1f;
                brightness = 0f;
                if (RestingBrightness(emotion) > 0f) Begin(Phase.FadeIn);
                else phase = Phase.Idle;
            }
            else
            {
                phase = Phase.Idle;
                openness = 1f;
                brightness = RestingBrightness(emotion);
            }
        }
        WriteProperties();
    }

    [ContextMenu("Next emotion")]
    public void NextEmotion() => SetEmotion(((int)emotion + 1) % 16);
    [ContextMenu("Previous emotion")]
    public void PreviousEmotion() => SetEmotion(((int)emotion + 15) % 16);

    public Vector4 AtlasTransform
    {
        get
        {
            int index = Mathf.Clamp((int)displayedEmotion, 0, 15);
            // Uniform UV scale preserves the original unwrap's aspect ratio.
            // The small inset keeps bilinear samples inside the selected cell.
            float scale = 0.25f * 0.99f / Mathf.Max(uvSpan, 0.001f);
            float sx = mirrorX ? -scale : scale;
            float sy = flipY ? -scale : scale;
            return new Vector4(sx, sy, (index % 4 + 0.5f) * 0.25f - uvCenter.x * sx,
                (3 - index / 4 + 0.5f) * 0.25f - uvCenter.y * sy);
        }
    }

    public void Apply()
    {
        dirty = false;
        phase = Phase.Idle;
        displayedEmotion = emotion;
        openness = 1f;
        brightness = RestingBrightness(emotion);
        WriteProperties();
    }

    void WriteProperties()
    {
        if (screenRenderer == null) return;
        properties ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(properties, 0);
        properties.SetVector(BaseMapST, AtlasTransform);
        properties.SetVector(ScreenState, new Vector4(openness, brightness, 0f, 0f));
        var layout = EyeLayouts[Mathf.Clamp((int)displayedEmotion, 0, 15)];
        properties.SetVector(EyeLayout, new Vector4(layout.x, .94f, layout.y, 0));
        screenRenderer.SetPropertyBlock(properties, 0);
    }

    void OnEnable() { nextCycle = Time.unscaledTime + cycleSeconds; Apply(); }
    void OnValidate() { dirty = true; cycleSeconds = Mathf.Max(0.1f, cycleSeconds); }
    void Update()
    {
        if (dirty) SetEmotion(emotion);
        if (!Application.isPlaying) return;
        AdvanceTransition(Time.unscaledDeltaTime);
        if (!autoCycle || IsTransitioning || Time.unscaledTime < nextCycle) return;
        nextCycle = Time.unscaledTime + cycleSeconds;
        NextEmotion();
    }

    void OnDisable()
    {
        phase = Phase.Idle;
        if (screenRenderer == null || screenRenderer.sharedMaterial == null) return;
        properties ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(properties, 0);
        properties.SetVector(BaseMapST, screenRenderer.sharedMaterial.GetVector(BaseMapST));
        properties.SetVector(ScreenState, screenRenderer.sharedMaterial.HasProperty(ScreenState)
            ? screenRenderer.sharedMaterial.GetVector(ScreenState) : Vector4.one);
        if (screenRenderer.sharedMaterial.HasProperty(EyeLayout)) properties.SetVector(EyeLayout, screenRenderer.sharedMaterial.GetVector(EyeLayout));
        screenRenderer.SetPropertyBlock(properties, 0);
    }
}
