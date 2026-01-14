using UnityEditor;
using UnityEngine;

namespace Luzzi.PlantSystem
{
[ExecuteInEditMode, RequireComponent(typeof(MeshFilter))]
public class PlantNode : PlantHierarchy
{
    /// <summary>
    /// UV channel used for world position coordinates (required by shader)
    /// </summary>
    private const int UV_WORLD_POSITION_CHANNEL = 3;
    
    private bool _canBeModified;
    public bool CanBeModified => _canBeModified;
    
    /// <summary>
    /// Returns true if modifiers are actually applied and working (not just enabled)
    /// </summary>
    public bool HasModifiersApplied => _canBeModified && _modifier != null && _modifiedMesh != null && _baseMesh != null;

    [SerializeField]
    private bool _useRadialGrowth = true;

    [SerializeField]
    private HelixSettings _modifierSettings = new HelixSettings(new Vector2(0.1f, 0.1f), 1f, 0f, 0f);

    private MeshFilter _meshFilter => GetComponent<MeshFilter>();
    private ComputeShader _computeShader;
    private HelixModifier _modifier;
    public Mesh _baseMesh;
    public Mesh _modifiedMesh;
    public Mesh ModifiedMesh => _modifiedMesh;

    public void Setup(ComputeShader computeShader)
    {
        if (_computeShader == null)
        {
            _computeShader = computeShader;
        }
    }
    private void OnValidate()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Refresh()
    {
        // avoid send message warning by making a delay call
        var self = this;
        EditorApplication.delayCall += () =>
        {
            if (self == null) return;
            if (Application.isEditor)
            {
                self.UpdateModifier();
                self.GenerateWorldPositionUVs();
            }
        };
    }

    public void UpdateModifier()
    {
        if (_canBeModified)
        {
            ApplyHelixModifier();
        }
    }

    public void SetEnableModifier(bool enable)
    {
        if (_canBeModified != enable)
        {
            _canBeModified = enable;
            if (enable)
            {
                ApplyHelixModifier();
            }
            else
            {
                RevertHelixModifier();
            }

            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                if (_nodes[nodeIndex] == null) continue;
                _nodes[nodeIndex].SetEnableModifier(enable);
            }
        }

    }

    private void ApplyHelixModifier()
    {
        // Mesh modifier
        if (_modifier == null)
        {
            _modifier = new HelixModifier(_computeShader);
        }

        if (_baseMesh == null || _modifiedMesh == null)
        {
            return;
        }

        _modifier.Compute(_baseMesh, _modifierSettings, ref _modifiedMesh, out Vector3[] curveCenters);
        GenerateUV_LocalYMask(2, in curveCenters);
        _meshFilter.mesh = _modifiedMesh;

        for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
        {
            if (_nodes[nodeIndex] == null) continue;
            Vector3 baseLocalPosition = new Vector3(0f, _nodes[nodeIndex].gameObject.transform.localPosition.y, 0f);
            float height = _baseMesh.bounds.extents.y * 2;
            Vector3 newLocalPosition = _modifier.HelixCurve(baseLocalPosition, height, _modifierSettings);
            _nodes[nodeIndex].gameObject.transform.localPosition = newLocalPosition;
        }
    }

    public void UpdateMeshes()
    {
        bool udpated = false;
        //string baseMeshName = gameObject.name + "_basemesh";
        //if (_baseMesh == null) || _baseMesh.name != baseMeshName)
        //{
        if (_baseMesh == null && !_canBeModified)
        {
            _baseMesh = _meshFilter.sharedMesh;
            udpated = true;
        }
        //_baseMesh.name = baseMeshName;
        //}

        string modifiedMeshName = gameObject.name + "_modifiedmesh";
        if (_modifiedMesh == null || _modifiedMesh.name != modifiedMeshName)
        {
            _modifiedMesh = Instantiate(_baseMesh);
            _modifiedMesh.name = modifiedMeshName;
            if (_canBeModified)
            {
                ApplyHelixModifier();
            }
            udpated = true;
        }

        if (udpated)
        {
            _meshFilter.mesh = _canBeModified ? _modifiedMesh : _baseMesh;
        }

        if (_nodes == null || _nodes.Count == 0) return;
        for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
        {
            if (_nodes[nodeIndex] == null) continue;
            _nodes[nodeIndex].UpdateMeshes();
        }
    }

    private void RevertHelixModifier()
    {
        if (_baseMesh != null)
        {
            _meshFilter.mesh = _baseMesh;
        }

        if (_nodes == null || _nodes.Count == 0) return;
        for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
        {
            if (_nodes[nodeIndex] == null) continue;
            Vector3 modifiedPosition = _nodes[nodeIndex].gameObject.transform.localPosition;
            Vector3 resetPosition = new Vector3(0f, modifiedPosition.y, 0f);
            _nodes[nodeIndex].gameObject.transform.localPosition = resetPosition;
        }
    }

    public void GenerateUV_LocalYMask(int channel, in Vector3[] curveCenters)
    {
        if (_modifiedMesh == null || _modifier == null || curveCenters.Length == 0) return;

        Vector3[] vertices = _modifiedMesh.vertices;
        if (vertices == null || vertices.Length == 0) return;

        Vector3 scale = Vector3.one;
        float yPosition = 0f;
        Transform transform = gameObject.transform;
        while (transform != transform.root)
        {
            PlantNode currentNode = transform.GetComponent<PlantNode>();
            bool currentUsesRadialGrowth = currentNode && currentNode._useRadialGrowth;

            PlantNode parentNode = transform.parent.GetComponent<PlantNode>();
            bool parentUsesRadialGrowth = parentNode && parentNode._useRadialGrowth;

            if (parentUsesRadialGrowth || currentUsesRadialGrowth)
            {
                yPosition += transform.localPosition.y * transform.parent.localScale.y;
            }

            scale.x *= transform.localScale.x;
            scale.y *= transform.localScale.y;
            scale.z *= transform.localScale.z;

            transform = transform.parent;
        }

        Vector3 toOriginWS = Vector3.zero;
        if (!_useRadialGrowth)
        {
            transform = gameObject.transform;
            if (transform.parent != transform.root)
            {
                //toOriginWS = gameObject.transform.position;
                while (transform != transform.root)
                {
                    PlantNode parentNode = transform.parent.GetComponent<PlantNode>();
                    bool parentUsesRadialGrowth = parentNode && parentNode._useRadialGrowth;

                    if (parentUsesRadialGrowth)
                    {
                        break;
                    }
                    toOriginWS = gameObject.transform.position - transform.parent.position;

                    transform = transform.parent;
                }
            }
        }

        Vector4[] uv = new Vector4[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 offset = vertices[i];
            if (_useRadialGrowth)
            {
                offset -= curveCenters[i];
            }

            offset.x *= scale.x;
            offset.y *= scale.y;
            offset.z *= scale.z;
            offset = gameObject.transform.rotation * offset;
            offset += toOriginWS;

            float shiftedPosition = yPosition; // + vertices[i].y * gameObject.transform.localScale.y;
            if (_useRadialGrowth)
            {
                shiftedPosition += (vertices[i].y * gameObject.transform.localScale.y);
            }

            //shiftedPosition += _useRadialGrowth ? vertices[i].y * scale.y : 0f;
            uv[i] = new Vector4(offset.x, offset.y, offset.z, shiftedPosition);
        }

        _modifiedMesh.SetUVs(channel, uv);
        EditorUtility.SetDirty(_modifiedMesh);
    }

    /// <summary>
    /// Generates UV3 coordinates based on world position for shader rendering.
    /// Sets UV3.xy to the node's world position (X,Z) for all vertices.
    /// Essential for shader functionality - called synchronously during merge.
    /// </summary>
    public void GenerateWorldPositionUVs()
    {
        GenerateUV_LocalXY(UV_WORLD_POSITION_CHANNEL);
    }
    
    /// <summary>
    /// Internal method that generates UV coordinates for a specific channel based on world position.
    /// Sets UV.xy to the node's world position (X,Z) for all vertices.
    /// </summary>
    private void GenerateUV_LocalXY(int channel)
    {
        if (_modifiedMesh == null || _modifier == null) return;

        Vector3[] vertices = _modifiedMesh.vertices;
        if (vertices == null || vertices.Length == 0) return;

        Transform transform = gameObject.transform;
        Vector3 nodalOriginWS = Vector3.zero;
        while (transform != transform.root)
        {
            nodalOriginWS = transform.position;
            transform = transform.parent;
        }

        Vector4[] uv = new Vector4[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uv[i] = new Vector4(nodalOriginWS.x, nodalOriginWS.z, 0f, 0f);
        }

        _modifiedMesh.SetUVs(channel, uv);
        EditorUtility.SetDirty(_modifiedMesh);
    }
    private void OnDestroy()
    {
        if (_modifiedMesh == null) return;

        if (Application.isEditor)
        {
            DestroyImmediate(_modifiedMesh);
        }
        else
        {
            Destroy(_modifiedMesh);
        }
    }
}
}
