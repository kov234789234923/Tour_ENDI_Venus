using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class NeuroAnimationVideo
{
    const string Root = "Assets/_Tours/Tour_ENDI_NeuroGuide/Ress/Resource/Characters/Neuro_Guide";
    const string Output = "output/AnimationDemo/Animator60_20261023";
    const int Fps = 60;
    const float Step = 1f / Fps;
    static GameObject bot, cameraObject;
    static Camera camera;
    static RenderTexture target, oldActive;
    static Texture2D pixels;
    static Renderer[] others;
    static bool[] hidden;
    static Animator animator;
    static Dictionary<int, AnimatorState> states;
    static readonly HashSet<int> visited = new HashSet<int>();
    static NeuroScreenEmotion screen;
    static int totalFrame, defaultHash, transitionFrames;
    static int lastEntered;
    static int finishAt;
    static StringBuilder report;

    [MenuItem("Tools/Neuro Screens/Connect animation emotions")]
    public static void Install()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animations/Neuro_Animator.controller");
        Directory.CreateDirectory(Output);
        var backup = "output/AnimationDemo/Neuro_Animator.before-emotions.controller";
        if (!File.Exists(backup)) File.Copy(AssetDatabase.GetAssetPath(controller), backup);
        foreach (var layer in controller.layers) InstallStates(layer.stateMachine);
        AssetDatabase.SaveAssets();
    }
    static void InstallStates(AnimatorStateMachine machine)
    {
        foreach (var child in machine.states)
        {
            var state = child.state;
            var reaction = state.behaviours.OfType<NeuroAnimationEmotion>().FirstOrDefault() ?? state.AddStateMachineBehaviour<NeuroAnimationEmotion>();
            reaction.emotion = NeuroAnimationEmotion.ForClip(state.motion != null ? state.motion.name : state.name);
            EditorUtility.SetDirty(reaction); EditorUtility.SetDirty(state);
        }
        foreach (var child in machine.stateMachines) InstallStates(child.stateMachine);
    }

    [MenuItem("Tools/Neuro Screens/Render animation demo frames")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before capture");
        if (bot != null) throw new InvalidOperationException("Capture already running");
        Directory.CreateDirectory(Output + "/Frames");
        var source = GameObject.Find("PF_Neuro_AntiqueBronze");
        var sourceAnimator = source != null ? source.GetComponentInChildren<Animator>() : null;
        var controller = sourceAnimator != null ? sourceAnimator.runtimeAnimatorController as AnimatorController : null;
        if (controller == null) controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animations/Neuro_Animator.controller");
        states = controller.layers[0].stateMachine.states.ToDictionary(x => Animator.StringToHash(x.state.name), x => x.state);
        defaultHash = Animator.StringToHash(controller.layers[0].stateMachine.defaultState.name);
        if (states.Values.Any(x => x.behaviours.OfType<NeuroAnimationEmotion>().Count() != 1))
            throw new InvalidOperationException("Each state must have one NeuroAnimationEmotion behaviour");
        others = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        hidden = others.Select(x => x.forceRenderingOff).ToArray();
        try
        {
            foreach (var r in others) r.forceRenderingOff = true;
            bot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/PF_Neuro_AntiqueBronze.prefab"));
            bot.hideFlags = HideFlags.HideAndDontSave;
            bot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator = bot.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.enabled = true; animator.speed = sourceAnimator != null ? sourceAnimator.speed : 1f;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if(animator.speed <= 0) throw new InvalidOperationException("Animator speed must be positive");
            foreach (var r in bot.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
            screen = bot.GetComponent<NeuroScreenEmotion>();
            screen.AutoCycle = false;
            cameraObject = new GameObject("Neuro Animation Capture", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            var bounds = new Bounds(); bool first = true;
            animator.Rebind(); animator.Update(0f);
            // Measure the same state-machine playback, including blended poses.
            for(int sample=0;sample<60*40;sample++)
            {
                animator.Update(Step); screen.AdvanceTransition(Step);
                if(sample%6 != 0) continue;
                foreach(var renderer in bot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh(); renderer.BakeMesh(mesh);
                    foreach(var vertex in mesh.vertices)
                    { var world=renderer.transform.TransformPoint(vertex); if(first){bounds=new Bounds(world,Vector3.zero);first=false;}else bounds.Encapsulate(world); }
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y,bounds.extents.x)*1.18f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.085f,.12f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 20f;
            camera.transform.position = bounds.center + new Vector3(0,0,5f);
            camera.transform.LookAt(bounds.center);
            target = new RenderTexture(960,960,24); oldActive = RenderTexture.active;
            camera.targetTexture = target; pixels = new Texture2D(960,960,TextureFormat.RGB24,false);
            animator.Rebind(); screen.SetEmotionImmediate(NeuroScreenEmotion.Emotion.Offline); animator.Update(0f);
            totalFrame = transitionFrames = 0; lastEntered = 0; finishAt = -1; visited.Clear();
            report = new StringBuilder("60 fps; 960x960; AntiqueBronze; original AnimatorController; fixed Animator.Update(1/60); native state transitions and OnStateEnter emotions\n");
            report.AppendLine("Controller=" + AssetDatabase.GetAssetPath(controller) + "; speed=" + animator.speed);
            File.WriteAllText(Output + "/capture.txt", "RUNNING\n");
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
        }
        catch { Cleanup(); throw; }
    }
    static void Tick()
    {
        try
        {
            for (int batch = 0; batch < 3; batch++)
            {
                if (finishAt >= 0 && totalFrame >= finishAt)
                {
                    if(visited.Count != states.Count || transitionFrames == 0) throw new InvalidOperationException("Incomplete Animator coverage");
                    report.AppendLine("States="+visited.Count+"; transition frames="+transitionFrames+"; frames="+totalFrame+"; PASS");
                    File.WriteAllText(Output + "/capture.txt", report.ToString()); Cleanup(); return;
                }
                if(totalFrame >= Fps*60) throw new InvalidOperationException("Animator did not complete a cycle in 60 seconds");
                animator.Update(Step);
                bool blending = animator.IsInTransition(0);
                if(blending) transitionFrames++;
                var state = blending ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                int hash = state.shortNameHash;
                if(hash != lastEntered && states.TryGetValue(hash, out var definition))
                {
                    var expected = definition.behaviours.OfType<NeuroAnimationEmotion>().Single().emotion;
                    if(screen.CurrentEmotion != expected) throw new InvalidOperationException(definition.name+": OnStateEnter emotion mismatch");
                    if(hash==defaultHash && visited.Count==states.Count && finishAt<0) finishAt=totalFrame+Fps;
                    visited.Add(hash); lastEntered=hash;
                    report.AppendLine("frame="+totalFrame+"; "+definition.name+" -> "+expected+"; blend="+blending+"; PASS");
                }
                screen.AdvanceTransition(Step);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,960,960),0,0); pixels.Apply();
                File.WriteAllBytes(Output + "/Frames/" + totalFrame.ToString("D5") + ".png", pixels.EncodeToPNG());
                totalFrame++;
            }
        }
        catch (Exception e) { File.WriteAllText(Output + "/capture.txt", e.ToString()); Cleanup(); Debug.LogException(e); }
    }
    static void OnPlayModeChanged(PlayModeStateChange state) { Cleanup(); }
    static void Cleanup()
    {
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (others != null) for(int i=0;i<others.Length;i++) if(others[i]!=null) others[i].forceRenderingOff=hidden[i];
        if(camera!=null) camera.targetTexture=null;
        RenderTexture.active=oldActive;
        if(target!=null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        if(pixels!=null) UnityEngine.Object.DestroyImmediate(pixels);
        if(cameraObject!=null) UnityEngine.Object.DestroyImmediate(cameraObject);
        if(bot!=null) UnityEngine.Object.DestroyImmediate(bot);
    }
}
