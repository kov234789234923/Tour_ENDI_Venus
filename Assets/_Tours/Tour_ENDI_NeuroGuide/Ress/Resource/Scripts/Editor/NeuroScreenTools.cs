using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NeuroScreenTools
{
    const string Root = "Assets/_Tours/Tour_ENDI_NeuroGuide/Ress/Resource/Characters/Neuro_Guide";
    public const string Output = "output/ScreenEmotions/Skins";

    [Serializable] public sealed class SkinEntry { public string bot; public string atlas; public string material; public string bodyMaterial; public string reason; }
    [Serializable] public sealed class SkinMapping { public SkinEntry[] entries; }
    static Dictionary<string, SkinEntry> Mapping()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Textures/ScreenEmotions/ScreenSkinMapping.json");
        if (json == null) throw new InvalidOperationException("Screen skin mapping is missing.");
        var entries = JsonUtility.FromJson<SkinMapping>(json.text).entries;
        if (entries.Length != 16 || entries.Select(x => x.atlas).Distinct().Count() != 16)
            throw new InvalidOperationException("Exactly 16 distinct skin atlases are required.");
        return entries.ToDictionary(x => x.bot);
    }

    static Material SkinMaterial(SkinEntry entry)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(entry.material);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.atlas);
        if (material == null || texture == null || material.GetTexture("_BaseMap") != texture)
            throw new InvalidOperationException("Missing or mismatched screen assets: " + entry.bot);
        if (material.shader.name != "Neuro/Animated Screen" || !material.shader.isSupported)
            throw new InvalidOperationException("Unsupported screen shader: " + entry.bot);
        return material;
    }

    static Renderer Screen(GameObject root) => root.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Monitor_low");

    static Mesh ScreenMesh(Renderer screen)
    {
        var skin = screen as SkinnedMeshRenderer;
        var filter = screen.GetComponent<MeshFilter>();
        var mesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
        if (mesh == null || mesh.uv.Length == 0) throw new InvalidOperationException("Screen mesh/UV missing: " + screen.name);
        return mesh;
    }

    static void Configure(GameObject root, Material material, Material bodyMaterial)
    {
        var screen = Screen(root);
        var mesh = ScreenMesh(screen);
        var lo = new Vector2(float.MaxValue, float.MaxValue);
        var hi = new Vector2(float.MinValue, float.MinValue);
        foreach (var uv in mesh.uv) { lo = Vector2.Min(lo, uv); hi = Vector2.Max(hi, uv); }
        screen.sharedMaterials = new[] { material };
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == screen) continue;
            renderer.sharedMaterials = Enumerable.Repeat(bodyMaterial, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
            EditorUtility.SetDirty(renderer);
        }
        var controller = root.GetComponent<NeuroScreenEmotion>() ?? root.AddComponent<NeuroScreenEmotion>();
        controller.Configure(screen, (lo + hi) * 0.5f, Mathf.Max(hi.x - lo.x, hi.y - lo.y));
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(screen);
    }

    [MenuItem("Tools/Neuro Screens/Assign matching skin atlases to all bots")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before installation.");
        Directory.CreateDirectory(Output);
        var mapping = Mapping();
        foreach (var entry in mapping.Values)
        {
            SkinMaterial(entry);
            if (AssetDatabase.LoadAssetAtPath<Material>(entry.bodyMaterial) == null) throw new InvalidOperationException("Body material missing: " + entry.bot);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + entry.bot + ".prefab");
            ScreenMesh(Screen(asset));
        }
        string backupDir = "Library/NeuroScreenSkinsBackup/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        Directory.CreateDirectory(backupDir);
        var scene = SceneManager.GetActiveScene();
        if (!EditorSceneManager.SaveScene(scene, backupDir + "/BeforeRebind.unity", true))
            throw new IOException("Could not preserve the current scene.");
        int prefabCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var material = SkinMaterial(mapping[Path.GetFileNameWithoutExtension(path)]);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(mapping[Path.GetFileNameWithoutExtension(path)].bodyMaterial);
            File.Copy(path, backupDir + "/" + Path.GetFileName(path));
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Configure(prefab, material, bodyMaterial);
                // Material fallback also shows the neutral face without a controller.
                material.SetVector("_BaseMap_ST", prefab.GetComponent<NeuroScreenEmotion>().AtlasTransform);
                EditorUtility.SetDirty(material);
                PrefabUtility.SaveAsPrefabAsset(prefab, path, out bool success);
                if (!success) throw new IOException("Could not save " + path);
                prefabCount++;
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        int instanceCount = 0;
        var sceneBots = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
            .Select(x => x.gameObject).Where(x => mapping.ContainsKey(x.name)).ToArray();
        foreach (var root in sceneBots)
        {
            var material = SkinMaterial(mapping[root.name]);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(mapping[root.name].bodyMaterial);
            Configure(root, material, bodyMaterial);
            var screen = Screen(root);
            var controller = root.GetComponent<NeuroScreenEmotion>();
            // Existing scene material overrides must be corrected explicitly.
            screen.sharedMaterials = new[] { material };
            controller.Apply();
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            instanceCount++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scene.");
        File.WriteAllText(Output + "/installation.txt", "Distinct skin atlases=" + mapping.Count + "\nPrefabs=" + prefabCount + "\nScene instances=" + instanceCount + "\nScene=" + scene.path + "\nBackup=" + backupDir);
        SceneView.RepaintAll();
        Debug.Log("Neuro screens installed: " + prefabCount + " prefabs, " + instanceCount + " scene bots.");
    }

    [MenuItem("Tools/Neuro Screens/Verify UV and render all emotions")]
    public static void Verify()
    {
        Directory.CreateDirectory(Output);
        var bots = SceneManager.GetActiveScene().GetRootGameObjects().Where(x => x.GetComponent<NeuroScreenEmotion>() != null).OrderBy(x => x.name).ToArray();
        if (bots.Length == 0) throw new InvalidOperationException("No bot screens in scene.");
        var report = new StringBuilder();
        report.AppendLine("Unity=" + Application.unityVersion + "; Play=" + EditorApplication.isPlaying);
        var mapping = Mapping();
        if (bots.Length != 16) throw new Exception("Expected 16 scene bots.");
        foreach (var entry in mapping.Values)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + entry.bot + ".prefab");
            if (prefab == null || prefab.GetComponent<NeuroScreenEmotion>() == null || Screen(prefab).sharedMaterial != SkinMaterial(entry)
                || prefab.GetComponent<NeuroScreenEmotion>().ScreenRenderer != Screen(prefab))
                throw new Exception("Saved prefab screen mismatch: " + entry.bot);
            var body = AssetDatabase.LoadAssetAtPath<Material>(entry.bodyMaterial);
            if (body == null || prefab.GetComponentsInChildren<Renderer>(true).Where(x => x != Screen(prefab)).Any(x => x.sharedMaterials.Any(m => m != body)))
                throw new Exception("Saved prefab body material mismatch: " + entry.bot);
        }
        report.AppendLine("Saved prefabs: 16/16 matching atlas materials and emotion controllers.");
        int samples = 0;
        foreach (var bot in bots)
        {
            var controller = bot.GetComponent<NeuroScreenEmotion>();
            var before = controller.CurrentEmotion;
            var screen = Screen(bot);
            var shared = SkinMaterial(mapping[bot.name]);
            Vector4 initialMaterialST = shared.GetVector("_BaseMap_ST");
            if (screen.sharedMaterial != shared) throw new Exception("Wrong skin material: " + bot.name);
            if (controller.ScreenRenderer != screen) throw new Exception("Broken screen controller binding: " + bot.name);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(mapping[bot.name].bodyMaterial);
            if (bot.GetComponentsInChildren<Renderer>(true).Where(x => x != screen).Any(x => x.sharedMaterials.Any(m => m != bodyMaterial)))
                throw new Exception("Wrong scene body material: " + bot.name);
            var mesh = ScreenMesh(screen);
            for (int index = 0; index < 16; index++)
            {
                controller.SetEmotionImmediate(index);
                var block = new MaterialPropertyBlock();
                screen.GetPropertyBlock(block, 0);
                var st = block.GetVector("_BaseMap_ST");
                if (st != controller.AtlasTransform) throw new Exception("Expression was not applied.");
                int col = index % 4, row = 3 - index / 4;
                foreach (var uv in mesh.uv)
                {
                    var mapped = new Vector2(uv.x * st.x + st.z, uv.y * st.y + st.w);
                    if (mapped.x <= col * .25f || mapped.x >= (col + 1) * .25f || mapped.y <= row * .25f || mapped.y >= (row + 1) * .25f)
                        throw new Exception("UV leaked into adjacent emotion: " + bot.name + " / " + index);
                    samples++;
                }
            }
            controller.SetEmotionImmediate(before);
            if (shared.GetVector("_BaseMap_ST") != initialMaterialST) throw new Exception("Material changed during expression selection: " + bot.name);
            report.AppendLine(bot.name + ": body material PASS; screen controller PASS; " + screen.GetType().Name + "; 16/16 expressions, " + mesh.vertexCount + " UV vertices per expression, " + mapping[bot.name].atlas + "; " + mapping[bot.name].reason);
        }
        var first = bots[0].GetComponent<NeuroScreenEmotion>();
        var second = bots.Length > 1 ? bots[1].GetComponent<NeuroScreenEmotion>() : null;
        if (second != null)
        {
            var previous = first.CurrentEmotion;
            var block = new MaterialPropertyBlock(); second.ScreenRenderer.GetPropertyBlock(block, 0);
            var before = block.GetVector("_BaseMap_ST");
            first.SetEmotionImmediate(13);
            second.ScreenRenderer.GetPropertyBlock(block, 0);
            if (block.GetVector("_BaseMap_ST") != before) throw new Exception("Expression affected another bot.");
            first.SetEmotionImmediate(previous);
        }
        report.AppendLine("16 distinct skin atlases; UV samples within selected cells=" + samples + "; per-bot independence PASS; materials unchanged PASS.");
        File.WriteAllText(Output + "/verification.txt", report.ToString());
        Render(bots);
        Debug.Log("Neuro screen verification PASS: " + samples + " UV samples.");
    }

    static void Render(GameObject[] bots)
    {
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var hidden = renderers.Select(x => x.forceRenderingOff).ToArray();
        var go = new GameObject("NeuroScreenVerificationCamera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 0.92f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.10f, .12f, .15f);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 12f;
        var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        var oldActive = RenderTexture.active;
        var sheet = new Texture2D(2048, 2048, TextureFormat.RGB24, false);
        var neutralSheet = new Texture2D(2048, 2048, TextureFormat.RGB24, false);
        var happySheet = new Texture2D(2048, 2048, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            for (int botIndex = 0; botIndex < bots.Length; botIndex++)
            {
                var bot = bots[botIndex];
                var controller = bot.GetComponent<NeuroScreenEmotion>();
                var previous = controller.CurrentEmotion;
                try
                {
                    for (int r = 0; r < renderers.Length; r++) renderers[r].forceRenderingOff = hidden[r] || !renderers[r].transform.IsChildOf(bot.transform);
                    var target = bot.transform.TransformPoint(new Vector3(0, .23f, 0));
                    camera.transform.position = target + bot.transform.TransformDirection(new Vector3(0, 0, 4));
                    camera.transform.LookAt(target, bot.transform.up);
                    for (int i = 0; i < 16; i++)
                    {
                        controller.SetEmotionImmediate(i);
                        camera.Render();
                        RenderTexture.active = rt;
                        var frame = new Texture2D(512, 512, TextureFormat.RGB24, false);
                        frame.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); frame.Apply();
                        var pixels = frame.GetPixels();
                        sheet.SetPixels(i % 4 * 512, (3 - i / 4) * 512, 512, 512, pixels);
                        if (i == 0) neutralSheet.SetPixels(botIndex % 4 * 512, (3 - botIndex / 4) * 512, 512, 512, pixels);
                        if (i == 1) happySheet.SetPixels(botIndex % 4 * 512, (3 - botIndex / 4) * 512, 512, 512, pixels);
                        UnityEngine.Object.DestroyImmediate(frame);
                    }
                    sheet.Apply();
                    File.WriteAllBytes(Output + "/" + bot.name + "_16_Emotions.png", sheet.EncodeToPNG());
                }
                finally { controller.SetEmotionImmediate(previous); }
            }
            neutralSheet.Apply(); happySheet.Apply();
            File.WriteAllBytes(Output + "/Unity_16_Skins_Neutral.png", neutralSheet.EncodeToPNG());
            File.WriteAllBytes(Output + "/Unity_16_Skins_Happy.png", happySheet.EncodeToPNG());
            File.WriteAllText(Output + "/GalleryOrder.txt", string.Join("\n", bots.Select((bot, index) => (index + 1) + ". " + bot.name)));
            var selected = bots.FirstOrDefault(x => x.name.Contains("Aqua")) ?? bots[0];
            for (int r = 0; r < renderers.Length; r++) renderers[r].forceRenderingOff = hidden[r] || !renderers[r].transform.IsChildOf(selected.transform);
            camera.orthographicSize = .78f;
            var center = Screen(selected).bounds.center;
            camera.transform.position = center + selected.transform.TransformDirection(new Vector3(1.5f, .7f, 3.8f));
            camera.transform.LookAt(center, selected.transform.up);
            camera.Render();
            RenderTexture.active = rt;
            var angled = new Texture2D(512, 512, TextureFormat.RGB24, false);
            angled.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); angled.Apply();
            File.WriteAllBytes(Output + "/Unity_Aqua_Angled.png", angled.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(angled);
            Selection.activeGameObject = selected;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(center, Quaternion.LookRotation(new Vector3(-1.5f, -.7f, -3.8f)), 1.25f);
        }
        finally
        {
            for (int r = 0; r < renderers.Length; r++) if (renderers[r] != null) renderers[r].forceRenderingOff = hidden[r];
            camera.targetTexture = null;
            RenderTexture.active = oldActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEngine.Object.DestroyImmediate(neutralSheet);
            UnityEngine.Object.DestroyImmediate(happySheet);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}

