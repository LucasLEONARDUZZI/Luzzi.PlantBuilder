using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Luzzi.PlantBuilder{
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

    public void SetEditMode(bool enable)
    {
        _editMode = enable;
        if (enable)
        {
            _combiner.UnCombine();
        }
        else
        {
            _combiner.Combine(GetObjetsMeshesToCombine());
            // Normalize UV2.w (remap all values between 0 and 1) as it is used as growth progression in shader
            _combiner.NormalizeUVs(2, 3);
            // Set UV1.z as a constant to be used in shader as the lifeCycleSpeedFactor
            Debug.Log("_lifeCycleSpeedFactor " + _lifeCycleFactor);
            _combiner.SetUVsConstant(1, 3, _lifeCycleFactor);
            _combiner.SaveMesh();
        }

        _applyModifiers = false;
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
    }

    private Dictionary<GameObject, Mesh> GetObjetsMeshesToCombine()
    {
        PlantNode[] nodes = GetComponentsInChildren<PlantNode>();
        Dictionary<GameObject, Mesh> objectsMeshes = new Dictionary<GameObject, Mesh>();
        for (int index = 0; index < nodes.Length; index++)
        {
            PlantNode node = nodes[index];
            objectsMeshes.Add(node.gameObject, node.ModifiedMesh);
        }

        return objectsMeshes;
    }
}
}
