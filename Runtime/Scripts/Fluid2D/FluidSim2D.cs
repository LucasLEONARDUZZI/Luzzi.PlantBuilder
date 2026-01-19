using Unity.Mathematics;
using UnityEngine;

namespace Luzzi.PlantSystem
{
public class FluidSim2D : MonoBehaviour
{
    [Header("GENERAL")]

    [SerializeField, Tooltip("Fluid2D compute shader")]
    private ComputeShader _fluidCompute;

    [SerializeField, Range(64, 1024)]
    private int _width = 512;

    [SerializeField, Range(64, 1024)]
    private int _height = 512;

    [Space(10)]
    [Header("SIMULATION")]

    [SerializeField, Tooltip("in seconds")]
    private float _dissipationDuration = 60f;
    [SerializeField, Range(1, 100)]
    private int _pressureIterations = 40;
    [SerializeField, Tooltip("delta Time in seconds between simulation steps (0 = game delta time)")]
    private float _deltaTime = 1f / 60f;

    [Space(10)]
    [Header("SPLAT")]

    [SerializeField]
    private float _splatRadiusUV = 0.03f;
    [SerializeField]
    private float _splatStrength = 5.0f;
    [SerializeField]
    private float _forceScale = 50.0f;
    //[SerializeField]
    //private Color _splatColor = Color.cyan; // TODO advect water color ?

    // RenderTexture ping-pong
    // Step 1 : advected velocity + splat
    private RenderTexture _velocityA, _velocityB;
    //private RenderTexture _dyeA, _dyeB;  // TODO advect water color ?

    // Step 2 : divergence + pressure
    private RenderTexture _divergence;
    private RenderTexture _pressureA, _pressureB;

    // Compute shader kernels
    private int _kernelAdvectVelocity, _kernelSplat; // step 1
    private int _kernelDivergence, _kernelJacobiPressure, _kernelVelocityProjection; // step 2

    // TODO rework input based on character position
    private Vector2 _lastMouseUV;
    bool _hasLastMouse;

    private void OnEnable()
    {
        // Retrieve kernels from the compute shader
        _kernelAdvectVelocity = _fluidCompute.FindKernel("AdvectVelocity");
        //_kernelAdvectDye = _fluidCompute.FindKernel("AdvectDye"); // TODO advect water color ?
        _kernelSplat = _fluidCompute.FindKernel("Splat");
        _kernelDivergence = _fluidCompute.FindKernel("ComputeDivergence");
        _kernelJacobiPressure = _fluidCompute.FindKernel("JacobiPressure");
        _kernelVelocityProjection = _fluidCompute.FindKernel("SubtractPressureGradient");

        // Create render textures
        _velocityA = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RGFloat); // RGHalf : velocity is a xy component
        _velocityB = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RGFloat);
        //_dyeA = CreateRT(_width, _height, RenderTextureFormat.ARGBHalf); // ARGBHalf : color output is rgba
        //_dyeB = CreateRT(_width, _height, RenderTextureFormat.ARGBHalf); // TODO advect water color ?
        _divergence = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RHalf); // divergence : 1 component
        _pressureA = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RHalf);
        _pressureB = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RHalf);

        // Clear render targets to have clean initialization
        UtilsLibrary.ClearRT(_velocityA);
        UtilsLibrary.ClearRT(_velocityB);
        //ClearRT(_dyeA);  // TODO advect water color ?
        //ClearRT(_dyeB);
        UtilsLibrary.ClearRT(_divergence);
        UtilsLibrary.ClearRT(_pressureA);
        UtilsLibrary.ClearRT(_pressureB);

        // Expose dye texture as global to be used in any shader
        //Shader.SetGlobalTexture("_FluidDyeTex", _dyeA);  // TODO advect water color ?
        Shader.SetGlobalTexture("_FluidVelocityTex", _velocityA);
        Shader.SetGlobalTexture("_FluidPressureTex", _pressureA);
    }

    public void UpdateSimulation(Vector2? uv, Vector2 delta)
    {
        float stepDeltaTime = _deltaTime > 0 ? _deltaTime : Time.deltaTime;
        SetCommonParams(stepDeltaTime);

        Vector2Int size = new Vector2Int(_width, _height);

        // STEP 1 : ADVECT VELOCITY + SPLAT

        // 1. Advect velocity : velocityA -> velocityB
        _fluidCompute.SetTexture(_kernelAdvectVelocity, "_VelocityIn", _velocityA);
        _fluidCompute.SetTexture(_kernelAdvectVelocity, "_VelocityOut", _velocityB);
        UtilsLibrary.Dispatch(_fluidCompute, size, _kernelAdvectVelocity);

        // TODO advect water color ?
        // 2. Advect dye: dyeA -> dyeB, using the advected velocity (just stored in velocity B in 1.)
        //_fluidCompute.SetTexture(_kernelAdvectDye, "_VelocityIn", _velocityB);
        //_fluidCompute.SetTexture(_kernelAdvectDye, "_DyeIn", _dyeA);  
        //_fluidCompute.SetTexture(_kernelAdvectDye, "_DyeOut", _dyeB);
        //Dispatch(_kernelAdvectDye);

        // 3. Input using uv
        if (uv.HasValue)
        {
            Vector2 force = delta * _forceScale;

            // NOTE : kernel Splat read/write on *_Out in compute shader
            // so it splats on velocityB and dyeB
            _fluidCompute.SetVector("_PointUV", uv.Value);
            _fluidCompute.SetFloat("_Radius", _splatRadiusUV);
            _fluidCompute.SetVector("_Force", force);
            //_fluidCompute.SetVector("_Color", (Vector4)_splatColor); // TODO advect water color ?
            _fluidCompute.SetFloat("_SplatStrength", _splatStrength);

            _fluidCompute.SetTexture(_kernelSplat, "_VelocityOut", _velocityB);
            //_fluidCompute.SetTexture(_kernelSplat, "_DyeOut", _dyeB); // TODO advect water color ?

            UtilsLibrary.Dispatch(_fluidCompute, size, _kernelSplat);
        }

        // STEP 2 : DIVERGENCE + PRESSURE 
        // (a fluid cannot be compressed, it must diverge)

        // 1. Divergence : velocityB -> divergence
        _fluidCompute.SetTexture(_kernelDivergence, "_VelocityForDivergence", _velocityB);
        _fluidCompute.SetTexture(_kernelDivergence, "_DivergenceOut", _divergence);
        UtilsLibrary.Dispatch(_fluidCompute, size, _kernelDivergence);

        // 2. Jacobi : solve Poisson for pressure using divRT
        for (int i = 0; i < _pressureIterations; i++)
        {
            _fluidCompute.SetTexture(_kernelJacobiPressure, "_PressureIn", _pressureA);
            _fluidCompute.SetTexture(_kernelJacobiPressure, "_Divergence", _divergence);
            _fluidCompute.SetTexture(_kernelJacobiPressure, "_PressureOut", _pressureB);
            UtilsLibrary.Dispatch(_fluidCompute, size, _kernelJacobiPressure);
            UtilsLibrary.SwapRT(ref _pressureA, ref _pressureB);
        }

        // 3. Subtract pressure gradient : velocityB - gradient(pA) -> velocityA
        _fluidCompute.SetTexture(_kernelVelocityProjection, "_VelocityForDivergence", _velocityB);
        _fluidCompute.SetTexture(_kernelVelocityProjection, "_PressureIn", _pressureA);
        _fluidCompute.SetTexture(_kernelVelocityProjection, "_VelocityProjectedOut", _velocityA);
        UtilsLibrary.Dispatch(_fluidCompute, size, _kernelVelocityProjection);
        // We use the _velocityA as the output so we wont need to swap again
        // It will used in the next iteration as the velocity

        // FINAL STEP : Swap ping-pong: new state becomes A (B -> A)
        //Swap(ref _velocityA, ref _velocityB); // Already swapped (in step 2, 3. Subtract pressure gradient)
        //Swap(ref _dyeA, ref _dyeB); // TODO advect water color ?

        // Update global for display
        //Shader.SetGlobalTexture("_FluidDyeTex", _dyeA); // TODO advect water color ?
        Shader.SetGlobalTexture("_FluidVelocityTex", _velocityA);
        Shader.SetGlobalTexture("_FluidPressureTex", _pressureA);
    }

    private void SetCommonParams(float stepDt)
    {
        float eps = 1e-06f;
        _fluidCompute.SetInt("_Width", _width);
        _fluidCompute.SetInt("_Height", _height);
        _fluidCompute.SetFloat("_Dt", stepDt);
        
        // NOTE : the fluid simulation is not bound to any time speed
        float dissipationRate = _dissipationDuration <= eps ? 0.0f : 1f / _dissipationDuration;
        _fluidCompute.SetFloat("_DissipationRate", dissipationRate);
        
        _fluidCompute.SetFloat("_Dissipation", _dissipationDuration);
        _fluidCompute.SetVector("_TexelSize", new Vector2(1f / _width, 1f / _height));
    }

    private void Release()
    { 
        // Release the render texture to avoid memory leaks
        if (_velocityA != null) _velocityA.Release();
        if (_velocityB != null) _velocityB.Release();
        //if (_dyeA != null) _dyeA.Release(); // TODO advect water color ?
        //if (_dyeB != null) _dyeB.Release();
        if (_divergence != null) _divergence.Release();
        if (_pressureA != null) _pressureA.Release();
        if (_pressureB != null) _pressureB.Release();
    }

    private void OnDisable()
    {
        Release();
    }
    private void OnDestroy()
    {
        Release();
    }
}
}