using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnvManager))]
public class EvacueeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EnvManager controller = (EnvManager)target;

        controller.EnableRandmizeSpeedEvacuee = EditorGUILayout.Toggle("Enable Randomize Speed", controller.EnableRandmizeSpeedEvacuee);

        // EanbleEandmizeSpeedEvacuee が true のときのみ EvacueeSpeedMin と EvacueeSpeedMax を表示
        if (controller.EnableRandmizeSpeedEvacuee)
        {
            controller.EvacueeSpeedMin = EditorGUILayout.FloatField("Evacuee Speed Min", controller.EvacueeSpeedMin);
            controller.EvacueeSpeedMax = EditorGUILayout.FloatField("Evacuee Speed Max", controller.EvacueeSpeedMax);
        }

        // 変更があった場合にインスペクタを更新
        if (GUI.changed)
        {
            EditorUtility.SetDirty(controller);
        }
    }
}
