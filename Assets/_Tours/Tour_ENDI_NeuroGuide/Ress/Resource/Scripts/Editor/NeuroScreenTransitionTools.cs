using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NeuroScreenTransitionTools
{
    const string Root = "Assets/_Tours/Tour_ENDI_NeuroGuide/Ress/Resource/Characters/Neuro_Guide";
    public const string Output = "output/ScreenEmotions/Transitions";

    static NeuroScreenEmotion[] Controllers() => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(x => x.GetComponentsInChildren<NeuroScreenEmotion>(true)).OrderBy(x => x.name).ToArray();

    [MenuItem("Tools/Neuro Screens/Enable animated screen materials")]
    public static void UpgradeMaterials()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before updating materials.");
        var shader = Shader.Find("Neuro/Animated Screen");
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("Animated screen shader is unavailable or has errors.");
        var mapping = JsonUtility.FromJson<NeuroScreenTools.SkinMapping>(File.ReadAllText(Root + "/Textures/ScreenEmotions/ScreenSkinMapping.json"));
        Directory.CreateDirectory("Library/NeuroTransitionBackup");
        foreach (var entry in mapping.entries)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(entry.material);
            if (material == null) throw new Exception("Missing material: " + entry.material);
            string backup = "Library/NeuroTransitionBackup/" + Path.GetFileName(entry.material);
            if (!File.Exists(backup)) File.Copy(entry.material, backup);
            var atlas = material.GetTexture("_BaseMap");
            var tint = material.GetColor("_BaseColor");
            var st = material.GetVector("_BaseMap_ST");
            material.shader = shader;
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", tint);
            material.SetVector("_BaseMap_ST", st);
            material.SetVector("_ScreenState", new Vector4(1, 1, 0, 0));
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        foreach (var controller in Controllers()) controller.Apply();
        SceneView.RepaintAll();
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/upgrade.txt", mapping.entries.Length + " materials upgraded; atlas textures, material GUIDs and scene transforms preserved.");
    }

    [MenuItem("Tools/Neuro Screens/Verify emotion transitions")]
    public static void VerifyTransitions()
    {
        var controllers = Controllers();
        if (controllers.Length != 16) throw new Exception("Expected 16 bots in the active scene.");
        int pairs = 0;
        var report = new StringBuilder("Unity=" + Application.unityVersion + "; Play=" + EditorApplication.isPlaying + "\n");
        foreach (var controller in controllers)
        {
            var original = controller.CurrentEmotion;
            var material = controller.ScreenRenderer.sharedMaterial;
            if (material.shader.name != "Neuro/Animated Screen" || ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Wrong/invalid shader: " + controller.name);
            var materialST = material.GetVector("_BaseMap_ST");
            var atlas = material.GetTexture("_BaseMap");
            var resting = new float[16];
            try
            {
                for (int i = 0; i < 16; i++) { controller.SetEmotionImmediate(i); resting[i] = controller.Brightness; }
                for (int source = 0; source < 16; source++)
                    for (int target = 0; target < 16; target++)
                    {
                        controller.SetEmotionImmediate(source);
                        controller.SetEmotion(target);
                        if (source != target && (!controller.IsTransitioning || (int)controller.DisplayedEmotion != source))
                            throw new Exception("Transition skipped source: " + controller.name + " " + source + "->" + target);
                        for (int tick = 0; tick < 200 && controller.IsTransitioning; tick++)
                        {
                            controller.AdvanceTransition(0.02f);
                            if (float.IsNaN(controller.Brightness) || controller.Brightness < 0 || controller.Brightness > 1 || controller.EyeOpenness < 0 || controller.EyeOpenness > 1)
                                throw new Exception("Invalid transition state.");
                            var block = new MaterialPropertyBlock();
                            controller.ScreenRenderer.GetPropertyBlock(block, 0);
                            if (block.GetVector("_BaseMap_ST") != controller.AtlasTransform) throw new Exception("UV state not applied.");
                            var state = block.GetVector("_ScreenState");
                            if (state.x != controller.EyeOpenness || state.y != controller.Brightness) throw new Exception("Animation state not applied.");
                        }
                        if (controller.IsTransitioning || (int)controller.DisplayedEmotion != target || Mathf.Abs(controller.Brightness - resting[target]) > 0.001f || controller.EyeOpenness != 1)
                            throw new Exception("Transition did not settle: " + controller.name + " " + source + "->" + target);
                        pairs++;
                    }
                controller.SetEmotionImmediate(0);
                controller.SetEmotion(1); controller.AdvanceTransition(.035f);
                controller.SetEmotion(10); controller.AdvanceTransition(.02f);
                controller.SetEmotion(12); controller.AdvanceTransition(.1f);
                controller.SetEmotion(15); controller.AdvanceTransition(.1f);
                controller.SetEmotion(13); controller.AdvanceTransition(3f);
                if (controller.IsTransitioning || controller.DisplayedEmotion != NeuroScreenEmotion.Emotion.Love) throw new Exception("Latest request did not win.");
                controller.SetEmotion(8); controller.Apply();
                if (controller.IsTransitioning || controller.DisplayedEmotion != NeuroScreenEmotion.Emotion.Sad) throw new Exception("Immediate cancellation failed.");
                if (controller.enabled)
                {
                    controller.SetEmotion(1);
                    controller.enabled = false; controller.enabled = true;
                    if (controller.IsTransitioning || controller.DisplayedEmotion != controller.CurrentEmotion) throw new Exception("Enable/disable did not settle.");
                }
                if (material.GetVector("_BaseMap_ST") != materialST || material.GetTexture("_BaseMap") != atlas) throw new Exception("Shared material or atlas changed.");
                report.AppendLine(controller.name + ": 256 emotion pairs + rapid retarget + immediate cancel + disable/enable PASS");
            }
            finally { controller.SetEmotionImmediate(original); }
        }
        report.AppendLine("Pairs=" + pairs + "; shader errors=0; material mutation=0; PASS");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/verification.txt", report.ToString());
        RenderPreview(controllers.FirstOrDefault(x => x.name.Contains("Aqua")) ?? controllers[0]);
        Debug.Log("Neuro transitions PASS: " + pairs + " emotion pairs.");
    }

    static void RenderPreview(NeuroScreenEmotion controller)
    {
        var original = controller.CurrentEmotion;
        var others = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(x => !x.transform.IsChildOf(controller.transform)).ToArray();
        var wasHidden = others.Select(x => x.forceRenderingOff).ToArray();
        var go = new GameObject("TransitionVerificationCamera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = .65f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .12f, .15f);
        camera.nearClipPlane = .01f; camera.farClipPlane = 10;
        var center = controller.ScreenRenderer.bounds.center;
        camera.transform.position = center + controller.transform.TransformDirection(new Vector3(.15f, .1f, 3f));
        camera.transform.LookAt(center, controller.transform.up);
        var rt = new RenderTexture(384, 384, 24);
        var previous = RenderTexture.active;
        var sheet = new Texture2D(2304, 1536, TextureFormat.RGB24, false);
        int[] sources = { 0, 0, 1, 15 };
        int[] targets = { 1, 12, 15, 1 };
        float[][] times = { new[] { 0f, .045f, .09f, .13f, .18f, .24f }, new[] { 0f, .2f, .4f, .6f, .8f, 1.01f }, new[] { 0f, .1f, .2f, .3f, .4f, .65f }, new[] { 0f, .12f, .24f, .36f, .48f, .65f } };
        try
        {
            foreach (var other in others) other.forceRenderingOff = true;
            camera.targetTexture = rt;
            for (int row = 0; row < 4; row++)
            {
                controller.SetEmotionImmediate(sources[row]); controller.SetEmotion(targets[row]);
                float elapsed = 0;
                for (int col = 0; col < 6; col++)
                {
                    controller.AdvanceTransition(times[row][col] - elapsed); elapsed = times[row][col];
                    camera.Render(); RenderTexture.active = rt;
                    var frame = new Texture2D(384, 384, TextureFormat.RGB24, false);
                    frame.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); frame.Apply();
                    sheet.SetPixels(col * 384, (3 - row) * 384, 384, 384, frame.GetPixels());
                    UnityEngine.Object.DestroyImmediate(frame);
                }
            }
            sheet.Apply(); File.WriteAllBytes(Output + "/TransitionStages.png", sheet.EncodeToPNG());
            Directory.CreateDirectory(Output + "/Frames");
            controller.SetEmotionImmediate(0);
            for (int frameIndex = 0; frameIndex < 100; frameIndex++)
            {
                if (frameIndex == 8) controller.SetEmotion(1);
                if (frameIndex == 24) controller.SetEmotion(12);
                if (frameIndex == 55) controller.SetEmotion(15);
                if (frameIndex == 73) controller.SetEmotion(1);
                controller.AdvanceTransition(.04f);
                camera.Render(); RenderTexture.active = rt;
                var frame = new Texture2D(384, 384, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); frame.Apply();
                File.WriteAllBytes(Output + "/Frames/" + frameIndex.ToString("D3") + ".png", frame.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frame);
            }
        }
        finally
        {
            for (int i = 0; i < others.Length; i++) if (others[i] != null) others[i].forceRenderingOff = wasHidden[i];
            controller.SetEmotionImmediate(original);
            camera.targetTexture = null; RenderTexture.active = previous; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(sheet); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
