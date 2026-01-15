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

    private Color errorColor = new Color(1f, 0.7f, 0.7f); // Light red
    private Color warningColor = new Color(1f, 0.85f, 0.5f); // Light orange

    private void OnEnable()
    {
        speedFactorProperty = serializedObject.FindProperty("_lifeCycleFactor");
        growthDecayBalanceProperty = serializedObject.FindProperty("_growthDecayBalance");
    }

    public override void OnInspectorGUI()
    {
        

        // Centralisation des états et validations
        #region State Checks
        PlantBuilder builder = (PlantBuilder)target;
        bool isPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;
        bool isEditMode = builder.EditMode;
        bool isNormalized = builder.IsPrefabTransformNormalized();
        bool allAtGround = builder.AreChildrenLocalYZero();
        bool modifiersOk = builder.ApplyModifiers;
        // Auto Save
        bool autoSaveEnabled = true;

        // --- Vérification de l'état Auto Save en mode Prefab ---
        // Ce bloc tente de déterminer si l'option "Auto Save" est activée lors de l'édition d'un prefab :
        // 1. Il tente d'abord d'accéder à la propriété statique EditorSettings.prefabModeAutoSave (API Unity officielle).
        // 2. Si cette propriété n'existe pas (version Unity différente), il tente d'accéder à la propriété d'instance "autoSave" du PrefabStage courant via la réflexion.
        // 3. Si aucune des deux méthodes ne fonctionne (erreur d'accès/reflexion), il affiche un toggle manuel pour laisser l'utilisateur indiquer l'état d'Auto Save.
        if (isPrefabStage)
        {
            try
            {
                var editorSettingsType = typeof(EditorSettings);
                var autoSaveProperty = editorSettingsType.GetProperty("prefabModeAutoSave", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (autoSaveProperty != null)
                {
                    // Cas Unity >= 2019.3 : accès direct à EditorSettings.prefabModeAutoSave
                    autoSaveEnabled = (bool)autoSaveProperty.GetValue(null);
                }
                else
                {
                    // Cas fallback : accès à la propriété d'instance "autoSave" du PrefabStage courant
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    var prefabStageType = prefabStage.GetType();
                    var autoSaveField = prefabStageType.GetProperty("autoSave", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (autoSaveField != null)
                    {
                        autoSaveEnabled = (bool)autoSaveField.GetValue(prefabStage);
                    }
                }
            }
            catch
            {
                // Si la réflexion échoue, on propose un toggle manuel à l'utilisateur
                autoSaveEnabled = EditorGUILayout.Toggle("Auto Save Status", autoSaveEnabled);
                EditorGUILayout.HelpBox("Manual toggle - set to match your Auto Save setting", MessageType.Info);
            }
        }
        
        // Bouton merge warning
        bool mergeWarning = isEditMode && (!isNormalized || !allAtGround || !modifiersOk);
        
        bool buttonEnabled = isPrefabStage && (!isEditMode || autoSaveEnabled);
        #endregion

        // Early exit if not in prefab mode
        if (!isPrefabStage)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Open the prefab to edit the plant.", MessageType.Info);
            EditorGUILayout.Space(10);
            return;
        }

        // Force repaint to update Auto Save status in real-time
        if (isPrefabStage) Repaint();

        EditorGUILayout.Space(5);

        string generalTitle = isEditMode ? "PLANT EDITOR - EDIT MODE" : "PLANT EDITOR - MERGED MODE";
        EditorGUILayout.LabelField(generalTitle, EditorStyles.boldLabel);

        #region Bouton MERGE ALL / EDIT
        // Détermine le tooltip à afficher selon le mode
        string mergeEditTooltip = isEditMode
            ? "You can modify and add nodes. Click MERGE ALL to validate your editing."
            : "Do not modify or add nodes while merged. Click EDIT to modify nodes.";

        // Bouton EDIT / MERGE ALL avec tooltip
        EditorGUI.BeginDisabledGroup(!buttonEnabled);

        // Change button color if Auto Save is OFF and we're about to merge
        Color originalColor = GUI.backgroundColor;

        //la couleur ne peut etre activé qu'en prefab mode 
        if(isPrefabStage){
            // If Auto Save is OFF and we're in Edit Mode (about to merge)
            if(!autoSaveEnabled){
                GUI.backgroundColor = errorColor; // Light red
            }
            //If we are in edit mode
            if (builder.EditMode )
            {
                // and prefab is not normalized OR PlantNodes not at ground OR Modifiers disabled
                if ( !modifiersOk)
                {
                     GUI.backgroundColor = warningColor; // Light orange
                }
                if (!isNormalized || !allAtGround){
                    GUI.backgroundColor = errorColor;// Light red
                }
            }
        }

        string stateButtonText = isEditMode ? "MERGE ALL" : "EDIT";
        if (GUILayout.Button(new GUIContent(stateButtonText, mergeEditTooltip)))
        {
            bool nextEditMode = !builder.EditMode;
            if (nextEditMode == false) // On va merger
            {
                // Si le prefab n'est pas normalisé, propose de le normaliser automatiquement
                if (!isNormalized)
                {
                    bool normalize = EditorUtility.DisplayDialog(
                        "Prefab not normalized",
                        "The prefab will be normalized (position=0, scale=1) before merging.\nDo you want to continue?",
                        "Normalize and Merge",
                        "Cancel");
                    if (!normalize) return;
                    Undo.RecordObject(builder.transform, "Normalize Prefab Transform");
                    builder.NormalizePrefabTransform();
                    EditorUtility.SetDirty(builder);
                }
                // Si les PlantNodes ne sont pas au sol
                if (!allAtGround)
                {
                    bool align = EditorUtility.DisplayDialog(
                        "PlantNodes not at ground level",
                        "Some PlantNodes are not at Y=0.\nDo you want to continue ?",
                        "Merge",
                        "Cancel");
                    if (!align) return;
                    Undo.RecordObject(builder.transform, "Set PlantNodes Y to 0");
                    builder.SetChildrenLocalYToZero();
                    EditorUtility.SetDirty(builder);
                }
                // Si les modifiers sont désactivés
                if (!modifiersOk)
                {
                    bool enableMods = EditorUtility.DisplayDialog(
                        "Modifiers are disabled",
                        "Modifiers are currently disabled.\nThey will be enabled before merging.\nDo you want to continue?",
                        "Enable and Merge",
                        "Cancel");
                    if (!enableMods) return;
                    builder.SetApplyModifiers(true);
                    EditorUtility.SetDirty(builder);
                }
            }
            builder.SetEditMode(nextEditMode);
            EditorUtility.SetDirty(builder);
        }

        // Restore original color
        GUI.backgroundColor = originalColor;

        EditorGUI.EndDisabledGroup();
        #endregion
        
        // Warning message for Auto Save OFF (outside disabled group to remain visible)
        if (isPrefabStage && !autoSaveEnabled)
        {
            if (builder.EditMode)
            {
                EditorGUILayout.HelpBox("Auto Save is OFF - Enable Auto Save to merge.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Auto Save is OFF - you can EDIT but MERGING is disabled.", MessageType.Warning);
            }
        }

        EditorGUILayout.Space(10);

        // EDITION
        #region Edition
        EditorGUILayout.LabelField("EDITION", EditorStyles.boldLabel);

        string modifierButton = builder.ApplyModifiers ? "DISABLE MODIFIER" : "ENABLE MODIFIER";
        EditorGUI.BeginDisabledGroup(!isEditMode);

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
            GUI.backgroundColor = warningColor; // Light orange
        }

        if (GUILayout.Button(modifierButton))
        {
            builder.SetApplyModifiers(!builder.ApplyModifiers);
            EditorUtility.SetDirty(builder);
        }

        // Restore original color
        GUI.backgroundColor = originalModifierColor;

        EditorGUI.EndDisabledGroup();

        // Warning when modifiers sont désactivés en mode édition uniquement
        if (isEditMode && !builder.ApplyModifiers)
        {
            EditorGUILayout.HelpBox("⚠️ Modifiers are currently disabled. Click ENABLE MODIFIER to apply modifications.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
        // Affiche les propriétés standards du PlantBuilder
        base.OnInspectorGUI();
        #endregion


        // --- Section Normalisation ---
        #region Normalization
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Normalization", EditorStyles.boldLabel);
        Color prevColor = GUI.backgroundColor;
        if (isNormalized)
        {
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("Prefab is Normalized");
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            GUI.backgroundColor = errorColor; // Light red
            if (GUILayout.Button("Normalize"))
            {
                Undo.RecordObject(builder.transform, "Normalize Prefab Transform");
                builder.NormalizePrefabTransform();
                EditorUtility.SetDirty(builder);
            }
            GUI.backgroundColor = prevColor;
        }
        GUI.backgroundColor = prevColor;

        // --- Section Alignement au sol ---
        EditorGUILayout.Space(5);
        if (allAtGround)
        {
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("All PlantNodes are at ground level (Y=0)");
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red
            var groundTooltip = new GUIContent(
                "Set all PlantNodes Y to 0 (ground)",
                "The first-level PlantNodes will be repositioned so their Y=0 (ground level). Their children will follow this offset."
            );
            if (GUILayout.Button(groundTooltip))
            {
                Undo.RecordObject(builder.transform, "Set PlantNodes Y to 0");
                builder.SetChildrenLocalYToZero();
                EditorUtility.SetDirty(builder);
            }
            GUI.backgroundColor = prevColor;
        }
        // Affiche un warning si le prefab n'est pas normalisé (position != 0 ou scale != 1) ET si on est en mode édition, juste sous le bouton MERGE ALL
        var builderTarget = (PlantBuilder)target;
        if (builderTarget.EditMode && !builderTarget.IsPrefabTransformNormalized())
        {
            EditorGUILayout.HelpBox("Prefab isn't normalized.\nIt will be automatically normalized upon saving.", MessageType.Warning);
        }
        #endregion

        // Section Gizmos à la fin
        EditorGUILayout.LabelField("Gizmos", EditorStyles.boldLabel);
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
    }
}
}
