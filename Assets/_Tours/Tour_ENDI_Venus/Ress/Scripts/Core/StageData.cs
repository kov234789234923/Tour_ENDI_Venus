// StageData.cs
using UnityEngine;

[CreateAssetMenu(menuName = "VR Scenario/Stage Data", fileName = "Stage_")]
public class StageData : ScriptableObject
{
    public string stageId;              // "S02"
    public string title;                // "Высадка на поверхность"
    public string[] entryLineIds;       // реплики при входе на этап, по порядку
    public string[] highlightIds;       // что подсветить при входе
}
