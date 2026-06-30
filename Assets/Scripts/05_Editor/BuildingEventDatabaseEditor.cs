#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoardOfDead.Editor
{
    [CustomEditor(typeof(BuildingEventDatabaseSOBJ))]
    public class BuildingEventDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(8f);

            if (GUILayout.Button("Validate Event Database"))
            {
                BuildingEventDatabaseSOBJ database =
                    (BuildingEventDatabaseSOBJ)target;
                List<string> messages = database.ValidateDatabase();

                if (messages.Count == 0)
                {
                    Debug.Log("[BuildingEventDatabase] 검증 완료: 오류 없음", database);
                }
                else
                {
                    for (int index = 0; index < messages.Count; index++)
                    {
                        Debug.LogWarning(
                            "[BuildingEventDatabase] " + messages[index],
                            database);
                    }
                }
            }
        }
    }
}
#endif
