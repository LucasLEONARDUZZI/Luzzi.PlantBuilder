using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Luzzi.PlantSystem.Editor;

namespace Luzzi.PlantSystem.Editor
{
[CustomEditor(typeof(PlantBuilder))]
public class PlantBuilderEditor : UnityEditor.Editor
{
    private SerializedProperty speedFactorProperty;
    private SerializedProperty growthDecayBalanceProperty;
    private PlantBuilder _builder;

    private void OnEnable()
    {
        speedFactorProperty = serializedObject.FindProperty("_lifeCycleFactor");
        growthDecayBalanceProperty = serializedObject.FindProperty("_growthDecayBalance");
    }

    public override void OnInspectorGUI()
    {
    // Gizmo controls: constants, plus info
    EditorGUILayout.Space(10);
        var settings = PlantBuilder.Settings;
        if (settings != null)
        {
            SerializedObject settingsSO = new SerializedObject(settings);
            settingsSO.Update();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(settingsSO.FindProperty("gizmoSize"), new GUIContent("Etalon"));
            EditorGUILayout.PropertyField(settingsSO.FindProperty("showGizmo"), GUIContent.none, GUILayout.Width(16));
            EditorGUILayout.EndHorizontal();
            settingsSO.ApplyModifiedProperties();
        }
        PlantBuilder builder = (PlantBuilder)target;
        bool isPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;
        
        // If not in prefab mode, show simple message and exit
        if (!isPrefabStage)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Open the prefab to edit the plant.", MessageType.Info);
            EditorGUILayout.Space(10);
            return;
        }
        
        // Force repaint to update Auto Save status in real-time
        if (isPrefabStage)
        {
            Repaint();
        }
        
        // Check if Auto Save is enabled in prefab mode via reflection
        bool autoSaveEnabled = true; // Default to true
        
        if (isPrefabStage)
        {
            try 
            {
                // Try to access via reflection
                var editorSettingsType = typeof(EditorSettings);
                var autoSaveProperty = editorSettingsType.GetProperty("prefabModeAutoSave", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                if (autoSaveProperty != null)
                {
                    autoSaveEnabled = (bool)autoSaveProperty.GetValue(null);
                }
                else
                {
                    // Alternative: try PrefabStage itself
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    var prefabStageType = prefabStage.GetType();
                    var autoSaveField = prefabStageType.GetProperty("autoSave", 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    
                    if (autoSaveField != null)
                    {
                        autoSaveEnabled = (bool)autoSaveField.GetValue(prefabStage);
                    }
                }
            }
            catch 
            {
                // Fallback: manual toggle for now
                autoSaveEnabled = EditorGUILayout.Toggle("Auto Save Status", autoSaveEnabled);
                EditorGUILayout.HelpBox("Manual toggle - set to match your Auto Save setting", MessageType.Info);
            }
        }


        EditorGUILayout.Space(5);

        string generalTitle = builder.EditMode ? "PLANT EDITOR - EDIT MODE" : "PLANT EDITOR - MERGED MODE";
        EditorGUILayout.LabelField(generalTitle, EditorStyles.boldLabel);
        
        // Visual status display with HelpBox
        if (builder.EditMode)
        {
            EditorGUILayout.HelpBox("You can modify and add nodes\n" +
                                  "Click MERGE ALL to validate your editing", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Do not modify or add nodes while merged\n" +
                                  "Click EDIT to modify nodes", MessageType.Warning);
        }

        string stateButtonText = builder.EditMode ? "MERGE ALL" : "EDIT";
        
        // Disable MERGE ALL button if Auto Save is OFF (but keep EDIT always enabled)
        bool buttonEnabled = isPrefabStage && (!builder.EditMode || autoSaveEnabled);
        
        EditorGUI.BeginDisabledGroup(!buttonEnabled);
        
        // Change button color if Auto Save is OFF and we're about to merge
        Color originalColor = GUI.backgroundColor;
        if (!builder.EditMode && isPrefabStage && !autoSaveEnabled)
        {
            GUI.backgroundColor = Color.red;
        }
        
        if (GUILayout.Button(stateButtonText))
        {
            builder.SetEditMode(!builder.EditMode);
            EditorUtility.SetDirty(builder);
        }
        
        // Restore original color
        GUI.backgroundColor = originalColor;
        
        EditorGUI.EndDisabledGroup();
        
        // Warning message for Auto Save OFF (outside disabled group to remain visible)
        if (isPrefabStage && !autoSaveEnabled)
        {
            if (builder.EditMode)
            {
                EditorGUILayout.HelpBox("⚠️ Auto Save is OFF - MERGE ALL is disabled for safety. Enable Auto Save to merge.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Auto Save is OFF - MERGE ALL was safely disabled.", MessageType.Warning);
            }
        }

        EditorGUILayout.Space(10);

        // EDITION
        EditorGUILayout.LabelField("EDITION", EditorStyles.boldLabel);

        string modifierButton = builder.ApplyModifiers ? "DISABLE MODIFIER" : "ENABLE MODIFIER";
        EditorGUI.BeginDisabledGroup(!builder.EditMode);
        
        serializedObject.Update();

        EditorGUILayout.Slider(speedFactorProperty, 0f, 1f);
        serializedObject.ApplyModifiedProperties();
        
        if (GUILayout.Button("REFRESH"))
        {

            builder.Refresh();
            EditorUtility.SetDirty(builder);
        }
        
        // Change button color if modifiers are disabled
        Color originalModifierColor = GUI.backgroundColor;
        if (!builder.ApplyModifiers)
        {
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red
        }
        
        if (GUILayout.Button(modifierButton))
        {
            builder.SetApplyModifiers(!builder.ApplyModifiers);
            EditorUtility.SetDirty(builder);
        }
        
        // Restore original color
        GUI.backgroundColor = originalModifierColor;
        
        EditorGUI.EndDisabledGroup();

        // Warning when modifiers are disabled in edit mode
        if (builder.EditMode && !builder.ApplyModifiers)
        {
            EditorGUILayout.HelpBox("⚠️ Modifiers are currently disabled. Click ENABLE MODIFIER to apply modifications.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        base.OnInspectorGUI();
    }
}
}
