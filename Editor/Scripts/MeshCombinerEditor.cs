using UnityEditor;
using UnityEngine;

namespace Luzzi.PlantSystem.Editor
{
    [CustomEditor(typeof(MeshCombiner))]
    public class MeshCombinerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            MeshCombiner combiner = (MeshCombiner)target;
            var saveDirField = combiner.GetType().GetField("_saveDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string currentPath = saveDirField.GetValue(combiner) as string;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save Directory", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            string newPath = EditorGUILayout.TextField(currentPath);
            if (newPath != currentPath)
            {
                saveDirField.SetValue(combiner, newPath);
                EditorUtility.SetDirty(combiner);
            }
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string folder = EditorUtility.OpenFolderPanel("Select Save Directory", Application.dataPath, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    if (folder.StartsWith(Application.dataPath))
                    {
                        folder = "Assets" + folder.Substring(Application.dataPath.Length);
                    }
                    saveDirField.SetValue(combiner, folder + "/");
                    EditorUtility.SetDirty(combiner);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
