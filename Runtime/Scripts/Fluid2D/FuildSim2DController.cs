using UnityEngine;
using UnityEngine.UIElements;

namespace Luzzi.PlantSystem
{

[RequireComponent(typeof(FluidSim2D)), RequireComponent(typeof(MeshFilter))]
public class FluidSimController : MonoBehaviour
{
    // The object transform we are tracking as an input
    [SerializeField]
    private Transform _source;

    // References to other components
    private FluidSim2D _simulation;
    private MeshFilter _meshFilter;

    // Last source position projected on UV
    private Vector2? _lastUV = null;
    private void OnEnable()
    {
        _lastUV = null;
        _simulation = GetComponent<FluidSim2D>();
        _meshFilter = GetComponent<MeshFilter>();
    }

    void Update()
    {
        if (!_simulation || !_meshFilter || !_source) return;

        Vector2 deltaUV = Vector2.zero;
        // Try to project world position on plane UV
        if (TryWorldToPlaneUV(_source.position, out Vector2? uv))
        {
            // Find delta between current and last UV
            deltaUV = _lastUV.HasValue ? uv.Value - _lastUV.Value : Vector2.zero;
        }

        // Update simulation in FluidSim2D
        _simulation.UpdateSimulation(uv, deltaUV);

        // Store the current UV as last for the next Update()
        _lastUV = uv;
    }

    private bool TryWorldToPlaneUV(Vector3 worldPosition, out Vector2? uv)
    {
        uv = null;
        Mesh mesh = _meshFilter.sharedMesh;
        if (!mesh)
        {
            return false;
        }

        // World to local plane
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        // Apply local scale
        Vector3 size = mesh.bounds.size;
        Vector3 center = mesh.bounds.center;
        // Find position
        Vector3 position = local - center;

        // UV mapping (world axis is inverted from uv)
        float u = (-position.x / size.x) + 0.5f;
        float v = (-position.z / size.z) + 0.5f;

        // Make sure we are in uv bounds
        if (u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return false;
        }

        uv = new Vector2(u, v);
        return true;
    }
}
}