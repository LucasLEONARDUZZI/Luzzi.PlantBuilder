using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Luzzi.PlantBuilder{
[CustomEditor(typeof(PlantBuilder))]
public class PlantBuilderEditor : Editor
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

        PlantBuilder builder = (PlantBuilder)target;
        bool isPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;


        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("GENERAL", EditorStyles.boldLabel);
        string label1 = "Current state: " + (builder.EditMode ? "EDIT MODE" : "MERGED");
        string label2 = builder.EditMode ? "You can modify and add nodes" : "No not modify or add nodes while merged";
        string label3 = builder.EditMode ? "Click MERGE ALL to validate your editing" : "Click on EDIT to modify nodes.";
        EditorGUILayout.LabelField(label1);
        EditorGUILayout.LabelField(label2);
        EditorGUILayout.LabelField(label3);

        string stateButtonText = builder.EditMode ? "MERGE ALL" : "EDIT";
        EditorGUI.BeginDisabledGroup(!isPrefabStage);
        if (GUILayout.Button(stateButtonText))
        {
            builder.SetEditMode(!builder.EditMode);
            EditorUtility.SetDirty(builder);
        }


        EditorGUI.EndDisabledGroup();
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
        
        if (GUILayout.Button(modifierButton))
        {
            builder.SetApplyModifiers(!builder.ApplyModifiers);
            EditorUtility.SetDirty(builder);
        }
        
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);

        base.OnInspectorGUI();
    }
}
}
