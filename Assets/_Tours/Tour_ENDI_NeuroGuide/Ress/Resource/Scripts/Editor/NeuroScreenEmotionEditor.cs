using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(NeuroScreenEmotion)), CanEditMultipleObjects]
public sealed class NeuroScreenEmotionEditor : Editor
{
    static readonly string[] Labels = {
        "Нейтральный", "Радость", "Смех", "Подмигивание",
        "Любопытство", "Удивление", "Размышление", "Замешательство",
        "Грусть", "Тревога", "Злость", "Сонливость",
        "Сон", "Любовь", "Восторг", "Выключен"
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Эмоция экрана", EditorStyles.boldLabel);
        for (int row = 0; row < 4; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int col = 0; col < 4; col++)
                {
                    int index = row * 4 + col;
                    if (GUILayout.Button(Labels[index], GUILayout.MinHeight(26)))
                    {
                        foreach (var obj in targets)
                        {
                            var controller = (NeuroScreenEmotion)obj;
                            Undo.RecordObject(controller, "Change screen emotion");
                            controller.SetEmotion(index);
                            NeuroScreenEmotionPreview.Track(controller);
                            EditorUtility.SetDirty(controller);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                        }
                        SceneView.RepaintAll();
                    }
                }
        }
        if (GUI.changed)
            foreach (var obj in targets)
            {
                var controller = (NeuroScreenEmotion)obj;
                controller.SetEmotion(controller.CurrentEmotion);
                NeuroScreenEmotionPreview.Track(controller);
            }
        EditorGUILayout.HelpBox("Смена эмоции — через моргание. Сон и выключение — через затухание. Предпросмотр работает вне Play. Auto Cycle перебирает эмоции в Play.", MessageType.Info);
    }

    void OnEnable() { Undo.undoRedoPerformed += OnUndo; }
    void OnDisable() { Undo.undoRedoPerformed -= OnUndo; }
    void OnUndo()
    {
        foreach (var obj in targets) if (obj != null) ((NeuroScreenEmotion)obj).Apply();
        SceneView.RepaintAll();
    }
}

[InitializeOnLoad]
public static class NeuroScreenEmotionPreview
{
    static readonly HashSet<NeuroScreenEmotion> Active = new HashSet<NeuroScreenEmotion>();
    static readonly List<NeuroScreenEmotion> Finished = new List<NeuroScreenEmotion>();
    static double lastTime;
    static NeuroScreenEmotionPreview() { lastTime = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    public static void Track(NeuroScreenEmotion controller)
    {
        if (!EditorApplication.isPlaying && controller != null && controller.IsTransitioning) Active.Add(controller);
    }
    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastTime); lastTime = now;
        if (EditorApplication.isPlaying) { Active.Clear(); return; }
        if (Active.Count == 0) return;
        Finished.Clear();
        foreach (var controller in Active)
        {
            if (controller == null || !controller.isActiveAndEnabled || !controller.IsTransitioning) { Finished.Add(controller); continue; }
            controller.AdvanceTransition(delta);
            if (!controller.IsTransitioning) Finished.Add(controller);
        }
        foreach (var controller in Finished) Active.Remove(controller);
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
