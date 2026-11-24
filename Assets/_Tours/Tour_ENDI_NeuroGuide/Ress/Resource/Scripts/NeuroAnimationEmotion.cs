using UnityEngine;

/// <summary>Screen reaction on Animator state entry. Add to states, not to the bot GameObject.</summary>
public sealed class NeuroAnimationEmotion : StateMachineBehaviour
{
    public NeuroScreenEmotion.Emotion emotion;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var screen = animator.GetComponentInParent<NeuroScreenEmotion>();
        if (screen == null) screen = animator.GetComponentInChildren<NeuroScreenEmotion>();
        if (screen != null) { screen.AutoCycle = false; screen.SetEmotion(emotion); }
    }

    public static NeuroScreenEmotion.Emotion ForClip(string clip)
    {
        int suffix = clip.LastIndexOf('_');
        if (suffix >= 0 && int.TryParse(clip.Substring(suffix + 1), out _)) clip = clip.Substring(0, suffix);
        switch (clip)
        {
            case "Anim_Curious": return NeuroScreenEmotion.Emotion.Curious;
            case "Anim_Delight": return NeuroScreenEmotion.Emotion.Excited;
            case "Anim_Flight": case "Anim_FlightStart": case "Anim_FlightEnd": return NeuroScreenEmotion.Emotion.Happy;
            case "Anim_Fright": case "Anim_Fear": return NeuroScreenEmotion.Emotion.Worried;
            case "Anim_Laugh": return NeuroScreenEmotion.Emotion.Laugh;
            case "Anim_Loving": return NeuroScreenEmotion.Emotion.Love;
            case "Anim_No": return NeuroScreenEmotion.Emotion.Angry;
            case "Anim_Off": case "Anim_TurningOff": return NeuroScreenEmotion.Emotion.Offline;
            case "Anim_Sad": return NeuroScreenEmotion.Emotion.Sad;
            case "Anim_Surprised": return NeuroScreenEmotion.Emotion.Surprised;
            case "Anim_Thinking": return NeuroScreenEmotion.Emotion.Thinking;
            case "Anim_Yes": return NeuroScreenEmotion.Emotion.Happy;
            default: return NeuroScreenEmotion.Emotion.Neutral;
        }
    }
}
