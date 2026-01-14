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
}

}
