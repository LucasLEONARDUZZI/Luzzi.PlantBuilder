using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Luzzi.PlantSystem
{

// Ecological Progression Controller
public class EcoProgController : MonoBehaviour
{
    [Header("GENERAL")]
    [SerializeField]
    private ComputeShader _ecoProgCompute;

    [SerializeField, Range(64, 1024)]
    private int _width = 512;
    [SerializeField, Range(64, 1024)]
    private int _height = 512;
    [SerializeField]
    private float _timeSpeed = 1.0f;
    [SerializeField]
    private float _deltaTime = 1 / 60f;
    private float _updateTimer = 0f;

    [Space(5)]
    [Header("SOIL QUALITY / PLAYER PRESENCE")]
    [SerializeField]
    private float _velocityStrength = 0.1f;
    [SerializeField]
    private float _pressureStrength = 4f;
    [SerializeField]
    private float _recoveryDuration = 120f;
    [SerializeField]
    private float _attractivityMinDelta = 1e-4f;

    [Space(5)]
    [Header("ECOLOGICAL PROGRESSION")]

    [SerializeField, Range(0f, 0.1f), Tooltip("to avoid oscillation around thresholds")]
    private float _thresholdMargin = 0.02f;

    [Header("Plant Category 1")]
    [SerializeField, Range(0f, 1f)]
    private float _threshold1 = 0.1f;
    [SerializeField, Tooltip("in seconds")]
    private float _growthDuration1 = 60f;
    [SerializeField, Tooltip("in seconds")]
    private float _decayDuration1 = 3f;

    [Header("Plant Category 2")]
    [SerializeField, Range(0f, 1f)]
    private float _threshold2 = 0.3f;
    [SerializeField, Tooltip("in seconds")]
    private float _growthDuration2 = 120f;
    [SerializeField, Tooltip("in seconds")]
    private float _decayDuration2 = 3f;

    [Header("Plant Category 3")]
    [SerializeField, Range(0f, 1f)]
    private float _threshold3 = 0.6f;
    [SerializeField, Tooltip("in seconds")]
    private float _growthDuration3 = 180f;
    [SerializeField, Tooltip("in seconds")]
    private float _decayDuration3 = 3f;

    [Header("Plant Category 4")]
    [SerializeField, Range(0f, 1f)]
    private float _threshold4 = 0.9f;
    [SerializeField, Tooltip("in seconds")]
    private float _growthDuration4 = 300f;
    [SerializeField, Tooltip("in seconds")]
    private float _decayDuration4 = 3f;

    private RenderTexture _soilQualityA, _soilQualityB;
    private RenderTexture _growthCyclesA, _growthCyclesB;
    private RenderTexture _decayCyclesA, _decayCyclesB;
    private RenderTexture _soilAttractivity;
    private Texture _velocity;
    private Texture _pressure;
    private int _kernelInitCycles, _kernelSoilQuality, _kernelUpdateCycles, _kernelSoilAttrativity;

    private void OnEnable()
    {
        // Retrieve kernel from the compute shader
        _kernelSoilQuality = _ecoProgCompute.FindKernel("ComputeSoilQuality");
        _kernelUpdateCycles = _ecoProgCompute.FindKernel("UpdateLifeCycles");
        _kernelInitCycles = _ecoProgCompute.FindKernel("InitLifeCycles");
        _kernelSoilAttrativity = _ecoProgCompute.FindKernel("ComputeSoilAttractivity");

        // Get the velocity computed by the FluidSim2D
        _velocity = Shader.GetGlobalTexture("_FluidVelocityTex");
        _pressure = Shader.GetGlobalTexture("_FluidPressureTex");

        // Create render textures
        _soilQualityA = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RFloat);
        _soilQualityB = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RFloat);
        // NOTE : we need this level of precision for soil quality
        // Because the recovery duration can be max around 17s with a RHalf and deltatime of 1/60
        // the recovery value on a frame will be less than the precision of the texture
        // so it won't update.
        // With a RFloat the recovery duration can be around 3 days with deltatime of 1/60 (very overkill haha)
        _growthCyclesA = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.ARGBHalf, FilterMode.Point);
        _growthCyclesB = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.ARGBHalf, FilterMode.Point);
        _decayCyclesA = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.ARGBHalf, FilterMode.Point);
        _decayCyclesB = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.ARGBHalf, FilterMode.Point);
        _soilAttractivity = UtilsLibrary.CreateRT(_width, _height, RenderTextureFormat.RGHalf);

        // Clear render textures for clean init
        UtilsLibrary.ClearRT(_soilQualityA);
        UtilsLibrary.ClearRT(_soilQualityB);
        UtilsLibrary.ClearRT(_growthCyclesA);
        UtilsLibrary.ClearRT(_growthCyclesB);
        UtilsLibrary.ClearRT(_decayCyclesA);
        UtilsLibrary.ClearRT(_decayCyclesB);
        UtilsLibrary.ClearRT(_soilAttractivity);

        // Init life cycle textures
        SetParams(0f);
        Vector2Int size = new Vector2Int(_width, _height);
        _ecoProgCompute.SetTexture(_kernelInitCycles, "_SoilQualityOut", _soilQualityA);
        _ecoProgCompute.SetTexture(_kernelInitCycles, "_GrowthCyclesOut", _growthCyclesA);
        _ecoProgCompute.SetTexture(_kernelInitCycles, "_DecayCyclesOut", _decayCyclesA);
        UtilsLibrary.Dispatch(_ecoProgCompute, size, _kernelInitCycles);

        // Assign to global
        Shader.SetGlobalTexture("_SoilQualityTex", _soilQualityA);
        Shader.SetGlobalTexture("_GrowthCyclesTex", _growthCyclesA);
        Shader.SetGlobalTexture("_DecayCyclesTex", _decayCyclesA);
        Shader.SetGlobalTexture("_SoilAttractivityTex", _soilAttractivity);
    }

    // Use late update to make sure FluidSim2D was computed before
    void LateUpdate()
    {
        if (_deltaTime > 0f)
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer < _deltaTime)
            {
                return;
            }
            _updateTimer = 0f;
        }

        Shader.SetGlobalFloat("_TimeSpeed", _timeSpeed);

        float stepDeltaTime = _deltaTime > 0f ? _deltaTime : Time.deltaTime;
        SetParams(stepDeltaTime);
        Vector2Int size = new Vector2Int(_width, _height);

        // 1. compute soil quality
        _velocity = Shader.GetGlobalTexture("_FluidVelocityTex");
        _pressure = Shader.GetGlobalTexture("_FluidPressureTex");

        _ecoProgCompute.SetTexture(_kernelSoilQuality, "_VelocityTex", _velocity);
        _ecoProgCompute.SetTexture(_kernelSoilQuality, "_PressureTex", _pressure);
        _ecoProgCompute.SetTexture(_kernelSoilQuality, "_SoilQualityIn", _soilQualityA);
        _ecoProgCompute.SetTexture(_kernelSoilQuality, "_SoilQualityOut", _soilQualityB);
        UtilsLibrary.Dispatch(_ecoProgCompute, size, _kernelSoilQuality);

        // 2. update life cycles (growth and decay)
        _ecoProgCompute.SetTexture(_kernelUpdateCycles, "_SoilQualityIn", _soilQualityB); // just updated before
        _ecoProgCompute.SetTexture(_kernelUpdateCycles, "_GrowthCyclesIn", _growthCyclesA);
        _ecoProgCompute.SetTexture(_kernelUpdateCycles, "_GrowthCyclesOut", _growthCyclesB);
        _ecoProgCompute.SetTexture(_kernelUpdateCycles, "_DecayCyclesIn", _decayCyclesA);
        _ecoProgCompute.SetTexture(_kernelUpdateCycles, "_DecayCyclesOut", _decayCyclesB);
        UtilsLibrary.Dispatch(_ecoProgCompute, size, _kernelUpdateCycles);

        // 3. compute soil attractivity
        _ecoProgCompute.SetTexture(_kernelSoilAttrativity, "_SoilQualityIn", _soilQualityB);
        _ecoProgCompute.SetTexture(_kernelSoilAttrativity, "_SoilAttractivityOut", _soilAttractivity);
        UtilsLibrary.Dispatch(_ecoProgCompute, size, _kernelSoilAttrativity);

        // swap textures for ping-pong
        UtilsLibrary.SwapRT(ref _soilQualityA, ref _soilQualityB);
        UtilsLibrary.SwapRT(ref _growthCyclesA, ref _growthCyclesB);
        UtilsLibrary.SwapRT(ref _decayCyclesA, ref _decayCyclesB);

        // Expose global textures to be used in other shaders
        Shader.SetGlobalTexture("_SoilQualityTex", _soilQualityA);
        Shader.SetGlobalTexture("_GrowthCyclesTex", _growthCyclesA);
        Shader.SetGlobalTexture("_DecayCyclesTex", _decayCyclesA);
        Shader.SetGlobalTexture("_SoilAttractivityTex", _soilAttractivity);
    }
    private void SetParams(float stepDt)
    {
        float eps = 1e-06f;
        // Commons
        _ecoProgCompute.SetInt("_Width", _width);
        _ecoProgCompute.SetInt("_Height", _height);
        _ecoProgCompute.SetFloat("_Dt", stepDt);

        // Soil quality
        float recoveryRate = _recoveryDuration <= eps ? 0.0f : _timeSpeed / _recoveryDuration;
        _ecoProgCompute.SetFloat("_RecoveryRate", recoveryRate);
        _ecoProgCompute.SetFloat("_VelocityStrength", _velocityStrength);
        _ecoProgCompute.SetFloat("_PressureStrength", _pressureStrength);

        // Life Cycles
        Vector4 growthSpeeds = new Vector4
            (
                _growthDuration1 <= eps ? 0f : _timeSpeed / _growthDuration1,
                _growthDuration2 <= eps ? 0f : _timeSpeed / _growthDuration2,
                _growthDuration3 <= eps ? 0f : _timeSpeed / _growthDuration3,
                _growthDuration4 <= eps ? 0f : _timeSpeed / _growthDuration4
            );
        _ecoProgCompute.SetVector("_GrowthSpeeds", growthSpeeds);
        Vector4 decaySpeeds = new Vector4
            (
                _decayDuration1 <= eps ? 0f : _timeSpeed / _decayDuration1,
                _decayDuration2 <= eps ? 0f : _timeSpeed / _decayDuration2,
                _decayDuration3 <= eps ? 0f : _timeSpeed / _decayDuration3,
                _decayDuration4 <= eps ? 0f : _timeSpeed / _decayDuration4
            );
        _ecoProgCompute.SetVector("_DecaySpeeds", decaySpeeds);
        _ecoProgCompute.SetVector("_Thresholds", new Vector4(_threshold1, _threshold2, _threshold3, _threshold4));
        _ecoProgCompute.SetFloat("_Margin", _thresholdMargin);

        // Soil attractivity
        _ecoProgCompute.SetFloat("_MinDelta", _attractivityMinDelta);
    }

    private void Release()
    {
        if (_soilQualityA != null) _soilQualityA.Release();
        if (_soilQualityB != null) _soilQualityB.Release();
        if (_growthCyclesA != null) _growthCyclesA.Release();
        if (_growthCyclesB != null) _growthCyclesB.Release();
        if (_decayCyclesA != null) _decayCyclesA.Release();
        if (_decayCyclesB != null) _decayCyclesB.Release();
        if (_soilAttractivity != null) _soilAttractivity.Release();
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
