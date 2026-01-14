using System;
using UnityEngine;

namespace Luzzi.PlantSystem
{

[Serializable]
public struct HelixSettings
{
    public HelixSettings(Vector2 offset, float turns, float phase, float orientation)
    {
        Offset = offset;
        Turns = turns;
        Phase = phase;
        Orientation = orientation;
    }

    public Vector2 Offset;
    public float Turns;
    [Range(0f, 1f)]
    public float Phase;
    [Range(0f, 1f)]
    public float Orientation;
}

[ExecuteInEditMode]
public class HelixModifier
{
    private ComputeShader _helixCompute;

    public HelixModifier(ComputeShader helixCompute)
    {
        _helixCompute = helixCompute;
    }

    public void Compute(Mesh mesh, in HelixSettings settings, ref Mesh modifiedMesh, out Vector3[] curveCenters)
    {
        if (mesh == null || _helixCompute == null)
        {
            curveCenters = new Vector3[0];
            return;
        }

        Vector3[] verts = mesh.vertices;
        int count = verts.Length;
        curveCenters = new Vector3[count];
        // Mesh must have at least one vertex to be modified
        if (count == 0)
        {
            return;
        }

        // Find mesh height (from y = 0)
        float height = float.MinValue;
        for (int index = 0; index < count; index++)
        {
            float y = verts[index].y;
            if (y > height)
            {
                height = y;
            }
        }

        ComputeBuffer inBuffer = new ComputeBuffer(count, sizeof(float) * 3);
        ComputeBuffer outBuffer = new ComputeBuffer(count, sizeof(float) * 3);
        ComputeBuffer curveBuffer = new ComputeBuffer(count, sizeof(float) * 3);

        inBuffer.SetData(verts);

        int kernel = _helixCompute.FindKernel("CSMain");

        _helixCompute.SetInt("_VertexCount", count);
        _helixCompute.SetFloat("_Height", height);
        _helixCompute.SetVector("_Offset", new Vector4(settings.Offset.x, settings.Phase, settings.Offset.y, settings.Orientation));
        _helixCompute.SetFloat("_Turns", settings.Turns);

        _helixCompute.SetBuffer(kernel, "_InVertices", inBuffer);
        _helixCompute.SetBuffer(kernel, "_OutVertices", outBuffer);
        _helixCompute.SetBuffer(kernel, "_OutCurve", curveBuffer);

        int threadGroupSize = Mathf.CeilToInt(count / 64f);
        _helixCompute.Dispatch(kernel, threadGroupSize, 1, 1);

        outBuffer.GetData(verts);
        curveBuffer.GetData(curveCenters);

        inBuffer.Release();
        outBuffer.Release();
        curveBuffer.Release();

        modifiedMesh.vertices = verts;
        modifiedMesh.RecalculateBounds();
        modifiedMesh.RecalculateNormals();
        modifiedMesh.RecalculateTangents();
    }

    // A simple copy of compute shader function (Curve)
    public Vector3 HelixCurve(Vector3 position, float height, HelixSettings settings)
    {
        float t = Mathf.Clamp01(position.y / height);
        float theta = 2f * Mathf.PI * Mathf.Max(settings.Turns, 1e-06f) * t;

        float ox = settings.Offset.x * t;
        float oz = settings.Offset.y * t;
        float oy = settings.Phase * Mathf.PI * 2;

        float cx = ox * Mathf.Cos(theta + oy);
        float cz = oz * Mathf.Sin(theta + oy);

        return new Vector3(cx, position.y, cz);
    }

}
}
