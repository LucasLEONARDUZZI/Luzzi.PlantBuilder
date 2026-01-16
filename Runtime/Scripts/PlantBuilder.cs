using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Luzzi.PlantSystem
{

[ExecuteInEditMode, RequireComponent(typeof(MeshCombiner))]
public class PlantBuilder : PlantHierarchy
{
    private static PlantBuilderSettings _settings;
    public static PlantBuilderSettings Settings
    {
        get
        {
            if (_settings == null)
            {
                _settings = Resources.Load<PlantBuilderSettings>("PlantBuilderSettings");
#if UNITY_EDITOR
                if (_settings == null)
                {
                    _settings = ScriptableObject.CreateInstance<PlantBuilderSettings>();
                    System.IO.Directory.CreateDirectory("Assets/Resources");
                    UnityEditor.AssetDatabase.CreateAsset(_settings, "Assets/Resources/PlantBuilderSettings.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
                }
#endif
            }
            return _settings;
        }
    }
    private bool _editMode = false;
    public bool EditMode => _editMode;

    private bool _applyModifiers = false;
    public bool ApplyModifiers => _applyModifiers;

    [SerializeField, Range(0f, 1f), HideInInspector]
    private float _lifeCycleFactor = 1f;

    [SerializeField]
    private float _autoRefreshRate = 1f;
    private float _timer;

    [SerializeField]
    private ComputeShader _helixModifier;

    private MeshCombiner _combiner => GetComponent<MeshCombiner>();

    // Track the existing mesh asset name before entering edit mode
    private string _existingMeshAssetName = null;

    /// <summary>
    /// Controls the edit mode state of the PlantBuilder.
    /// When enabling edit mode, uncombines meshes for individual node editing.
    /// When disabling edit mode, performs mesh combination with critical synchronous updates.
    /// IMPORTANT: Includes fix for UV3 channel generation timing issue - forces node updates 
    /// and UV3 generation before mesh combining to prevent visual deformation on first merge.
    /// </summary>
    /// <param name="enable">True to enable edit mode (uncombine), false to disable (combine and save)</param>
    public void SetEditMode(bool enable)
    {
        _editMode = enable;
        if (enable)
        {
            // DETECTION: Sauvegarder le nom du mesh asset AVANT de l'enlever
            var meshFilter = _combiner.GetComponent<MeshFilter>();
            if (meshFilter?.sharedMesh != null)
            {
                _existingMeshAssetName = meshFilter.sharedMesh.name;
            }
            else
            {
                _existingMeshAssetName = null;
            }
            
            _combiner.UnCombine();
            _applyModifiers = true; // Enable modifiers by default in edit mode
        }
        else
        {            
            // CRITICAL FIX: Force synchronous node update to ensure UV3 generation before combining
            // This fixes the missing UV3 channel bug on first merge after prefab opening
            UpdateHierarchy(this, _helixModifier);
            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                if (_nodes[nodeIndex] == null) continue;
                
                // Force mesh update
                _nodes[nodeIndex].UpdateMeshes();
                
                // CRITICAL: Force UV3 generation (required by shader)
                _nodes[nodeIndex].GenerateWorldPositionUVs();
                
                // Apply modifiers if needed
                if (_applyModifiers)
                {
                    _nodes[nodeIndex].SetEnableModifier(true);
                }
                
            }
            
            // Normalize prefab transform to avoid merge issues
            NormalizePrefabTransform();

            _combiner.Combine(GetObjetsMeshesToCombine());
            // Normalize UV2.w (remap all values between 0 and 1) as it is used as growth progression in shader
            _combiner.NormalizeUVs(2, 3);
            // Set UV1.z as a constant to be used in shader as the lifeCycleSpeedFactor
            _combiner.SetUVsConstant(1, 3, _lifeCycleFactor);
            _combiner.SaveMesh(!string.IsNullOrEmpty(_existingMeshAssetName));
            _applyModifiers = false; // Disable modifiers in merged mode
        }
        Refresh(true);
    }

    public void SetApplyModifiers(bool enable)
    {
        if (_applyModifiers != enable)
        {
            _applyModifiers = enable;
            Refresh(true);
        }
    }

    private void Update()
    {
        if (_editMode)
        {
            _timer += Time.deltaTime;
            float duration = 1f / Mathf.Max(_autoRefreshRate, float.Epsilon);
            if (_timer >= duration)
            {
                _timer = 0f;
                Refresh();
            }
        }
    }

    private void OnValidate()
    {
        Refresh();
    }

    public void Refresh(bool refreshModifiers = false)
    {
        // Only in edit mode
        if (!_editMode) return;
        if (!Application.isEditor) return;

#if UNITY_EDITOR
        var self = this;
        EditorApplication.delayCall += () =>
        {
            if (self == null) return;

            self.UpdateHierarchy(self, self._helixModifier);
            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                if (self._nodes[nodeIndex] == null) continue;
                self._nodes[nodeIndex].UpdateMeshes();
            }

            if (refreshModifiers)
            {
                for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
                {
                    if (self._nodes[nodeIndex] == null) continue;
                    self._nodes[nodeIndex].SetEnableModifier(self._applyModifiers);
                }
            }
        };
#else
        // Runtime fallback - execute immediately without editor delay
        UpdateHierarchy(this, _helixModifier);
        for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
        {
            if (_nodes[nodeIndex] == null) continue;
            _nodes[nodeIndex].UpdateMeshes();
        }

        if (refreshModifiers)
        {
            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                if (_nodes[nodeIndex] == null) continue;
                _nodes[nodeIndex].SetEnableModifier(_applyModifiers);
            }
        }
#endif
    }

    /// <summary>
    /// Collects all modified meshes from child PlantNode components for mesh combination.
    /// Validates modifier states and mesh integrity before combining.
    /// Returns dictionary mapping GameObjects to their modified meshes with applied modifiers/UV data.
    /// Critical for ensuring proper UV3 channel presence required by shader rendering.
    /// </summary>
    private Dictionary<GameObject, Mesh> GetObjetsMeshesToCombine()
    {        
        PlantNode[] nodes = GetComponentsInChildren<PlantNode>();
        Dictionary<GameObject, Mesh> objectsMeshes = new Dictionary<GameObject, Mesh>();
        
        for (int index = 0; index < nodes.Length; index++)
        {
            PlantNode node = nodes[index];
            
            // Validation: Check if node modifier state matches expected state
            bool nodeHasModifier = node.CanBeModified;
            bool nodeModifiersApplied = node.HasModifiersApplied;
            
            if (_applyModifiers && !nodeModifiersApplied)
            {
                // Validation handled by diagnostic methods for editor display
            }
            else if (!_applyModifiers && nodeModifiersApplied)
            {
                // Validation handled by diagnostic methods for editor display
            }
            
            // Validation: Check if ModifiedMesh is valid
            if (node.ModifiedMesh == null)
            {
                // Error handled by diagnostic methods for editor display
                continue;
            }
                        
            objectsMeshes.Add(node.gameObject, node.ModifiedMesh);
        }

        return objectsMeshes;
    }

#region PrefabTransformNormalization
    /// <summary>
    /// Retourne true si le prefab a une position locale nulle et un scale unitaire.
    /// </summary>
    public bool IsPrefabTransformNormalized()
    {
        Transform parent = transform;
        return parent.localPosition == Vector3.zero && parent.localScale == Vector3.one;
    }
        
    /// <summary>
    /// Applique la normalisation aux PlantNodes.
    /// </summary>
    public void NormalizePrefabTransform()
    {
        Transform parent = transform;
        Vector3 parentScale = parent.localScale;
        Vector3 parentPosition = parent.localPosition;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            ApplyScaleToPlantNode(child, parentScale);// Adjuste la position locale en fonction du scale parent
            ApplyPositionToPlantNode(child, parentPosition);//doit intervenir après le scale pour ajuster l'offset correctement
        }
        parent.localScale = Vector3.one;
        parent.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Applique la position du parent au PlantNode enfant.
    /// </summary>
    private void ApplyPositionToPlantNode(Transform child, Vector3 parentPosition)
    {
        child.localPosition += parentPosition;
    }

    /// <summary>
    /// Applique le scale du parent au PlantNode enfant.
    /// </summary>
    private void ApplyScaleToPlantNode(Transform child, Vector3 parentScale)
    {
        child.localPosition = Vector3.Scale(child.localPosition, parentScale);
        child.localScale = Vector3.Scale(child.localScale, parentScale);
    }
    #endregion

#region ChildrenLocalYZero
    /// <summary>
    /// Vérifie que tous les enfants directs du prefab ont une position locale Y à zéro.
    /// Retourne true si tous les enfants sont OK, false sinon.
    /// </summary>
    public bool AreChildrenLocalYZero()
    {
        Transform parent = transform;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (Mathf.Abs(child.localPosition.y) > 1e-4f)
            {
                return false;
            }
        }
        return true;
    }

        /// <summary>
    /// Remet la position locale Y de tous les enfants directs à zéro.
    /// </summary>
    public void SetChildrenLocalYToZero()
    {
        Transform parent = transform;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Vector3 pos = child.localPosition;
            if (Mathf.Abs(pos.y) > 1e-4f)
            {
                pos.y = 0f;
                child.localPosition = pos;
            }
        }
    }
#endregion

#region Recenter
    /// <summary>
    /// Dessine la preview de recentrage avec Handles (éditeur uniquement, fonctionne en mode prefab).
    /// </summary>
    /// <param name="plantBuilder">L'instance à prévisualiser</param>
    /// <param name="color">Couleur des lignes</param>
    public static void DrawRecenterPreviewHandles(PlantBuilder plantBuilder, Color? color = null)
    {
#if UNITY_EDITOR
        if (plantBuilder == null) return;
        Vector3 average = plantBuilder.GetChildrenAverageWorldPosition();
        Transform parent = plantBuilder.transform;
        Color lineColor = color ?? Color.cyan;
        lineColor.a = 0.3f;
        Color placeholderColor = color ?? Color.cyan;
        float placeholderSize = 0.1f;
        
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Vector3 from = child.position;
            Vector3 to = child.position - average;
            Vector3 direction = (to - from).normalized;
            Handles.color = lineColor;
            Handles.DrawLine(from, (to-(direction * placeholderSize)));
            Handles.color = placeholderColor;
            Handles.DrawWireDisc(to, Vector3.up, placeholderSize);
        }
        
#endif
    }
 /// <summary>
    /// Affiche un Debug.DrawLine entre la position actuelle de chaque enfant et sa future position après recentrage.
    /// (Utilisable pour prévisualiser le recentrage dans la scène.)
    /// </summary>
    public void DrawRecenterPreview(Color? color = null, float duration = 5f)
    {
        Vector3 average = GetChildrenAverageWorldPosition();
        Transform parent = transform;
        Color lineColor = color ?? Color.cyan;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Vector3 from = child.position;
            Vector3 to = child.position - average;
            Debug.DrawLine(from, to, lineColor, duration);
        }
        Debug.DrawLine(parent.position, parent.position - average, Color.yellow);
    }

 /// <summary>
    /// Recentre tous les enfants directs autour de l'origine en soustrayant la moyenne de leur position globale.
    /// </summary>
    public void RecenterChildrenToOrigin()
    {
        Vector3 average = GetChildrenAverageWorldPosition();
        Transform parent = transform;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            // On veut que la nouvelle position globale soit child.position - average
            // Donc, en local :
            child.position -= average;
        }
    }

/// <summary>
    /// Calcule la moyenne des positions globales de tous les GameObjects enfants directs du prefab.
    /// </summary>
    /// <returns>Le centre moyen (Vector3) de tous les enfants directs.</returns>
    public Vector3 GetChildrenAverageWorldPosition()
    {
        Transform parent = transform;
        if (parent.childCount == 0) return parent.position;
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            sum += child.position;
            count++;
        }
        return sum / Mathf.Max(1, count);
    }
#endregion
       


#if UNITY_EDITOR
    private void OnDrawGizmos()
        {
            if (Settings == null || !Settings.showGizmo) return;
            if (!Application.isEditor) return;
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null) return;//si on est pas en mode prefab on sort 
            DrawGizmoScale();

            static void DrawGizmoScale()
            {
                Gizmos.color = Color.green;
                Vector3 start = Vector3.zero;
                Vector3 end = new Vector3(0, Settings.gizmoSize, 0);
                Gizmos.DrawLine(start, end);
                float LineHalfSize = Settings.gizmoSize * 0.1f;
                Gizmos.DrawLine(end + Vector3.left * LineHalfSize, end + Vector3.right * LineHalfSize);
            }
        }
#endif

    }

}
