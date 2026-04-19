#nullable enable
using UnityEditor;
using UnityEngine;

namespace ExtractionWeight.Core.Editor
{
    [CustomEditor(typeof(PlayerController))]
    public sealed class PlayerControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (PlayerController)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Carry Debug", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Clear Carry"))
                {
                    controller.DebugApplyMobilityLoad(0f);
                }

                if (GUILayout.Button("Add Light Test Loot"))
                {
                    controller.DebugApplyMobilityLoad(0.25f);
                }

                if (GUILayout.Button("Add Loaded Test Loot"))
                {
                    controller.DebugApplyMobilityLoad(0.6f);
                }

                if (GUILayout.Button("Add Overburdened Test Loot"))
                {
                    controller.DebugApplyMobilityLoad(0.9f);
                }

                if (GUILayout.Button("Add Soft Ceiling Test Loot"))
                {
                    controller.DebugApplyMobilityLoad(1.05f);
                }
            }
        }
    }
}
