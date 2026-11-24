#if UNITY_EDITOR
// StageManagerEditor.cs
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageManager))]
public class StageManagerEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var manager = (StageManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Отладка", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            string current = manager.CurrentIndex < 0 ? "—" : manager.GetLabel(manager.CurrentIndex);
            EditorGUILayout.LabelField("Текущий этап", current);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Назад")) manager.Previous();
                if (GUILayout.Button("Вперёд ▶")) manager.Next();
                if (GUILayout.Button("В начало")) manager.GoTo(0);
            }

            for (int i = 0; i < manager.StageCount; i++)
            {
                if (GUILayout.Button(manager.GetLabel(i))) manager.GoTo(i);
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Кнопки доступны в режиме Play.", MessageType.Info);
    }
}
#endif
