#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BoardOfDead.Editor
{
    [CustomEditor(typeof(ProceduralCityBoardManager))]
    public sealed class ProceduralCityBoardManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "GameBoardSettingManager Integration",
                EditorStyles.boldLabel);

            ProceduralCityBoardManager manager =
                (ProceduralCityBoardManager)target;

            if (GUILayout.Button(
                "Generate Board + Secondary Roads + City",
                GUILayout.Height(34f)))
            {
                manager.GenerateFromGameBoardSettingManager();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button(
                "Rebuild Visuals From Existing Board",
                GUILayout.Height(28f)))
            {
                manager.RebuildVisualsFromExistingBoard();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button(
                "Clear Procedural Visuals",
                GUILayout.Height(24f)))
            {
                manager.ClearGeneratedVisuals();
                EditorUtility.SetDirty(manager);
            }

            EditorGUILayout.HelpBox(
                "v10은 기존 GameBoardSettingManager 도로와 이동 노드를 유지하면서 각 논리 BoardSpace 아래에 1x1 이동 칸 오버레이를 생성합니다. " +
                "인접 노드 사이에도 랜덤 제어점이 들어가 사선 진입과 완만한 꺾임이 생깁니다. " +
                "도로 칸은 중립색으로, 역할 건물 칸은 해당 건물 역할 색상으로 표시됩니다. 이 오버레이는 기존 바닥을 생성하거나 덮지 않으며 이동 거리 확인용으로만 사용됩니다.",
                MessageType.Info);
        }
    }
}
#endif
