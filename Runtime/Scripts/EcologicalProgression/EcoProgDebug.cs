using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEngine;

namespace Luzzi.PlantSystem
{
[Serializable]
public enum EcoProgLayers
{
    None,
    Velocity, Pressure,
    SoilQuality, Presence, SoilAttractivity,
    Growth1, Growth2, Growth3, Growth4,
    Decay1, Decay2, Decay3, Decay4,
}

public class EcoProgDebug : MonoBehaviour
{
    [SerializeField]
    private MeshRenderer _plane;

    [SerializeField]
    private Material _debugMaterial;
    private Material _planeMaterial;

    [SerializeField]
    EcoProgLayers _layer = EcoProgLayers.None;
    EcoProgLayers _lastLayer = EcoProgLayers.None;

    private Texture _textureDebug;

    // Start is called before the first frame update
    void Start()
    {

#if !UNITY_EDITOR
    {
        Destroy();
    }
#endif

        _planeMaterial = _plane.material;
    }

    // Update is called once per frame
    void Update()
    {
        if (_layer == _lastLayer) return;

        if (_layer == EcoProgLayers.None)
        {
            _plane.material = _planeMaterial;
            _lastLayer = EcoProgLayers.None;
            return;
        }

        _plane.material = _debugMaterial;
        Vector4 channels = new Vector4();
        float oneMinus = 0;

        if (_layer == EcoProgLayers.Velocity)
        {
            _textureDebug = Shader.GetGlobalTexture("_FluidVelocityTex");
            channels = new Vector4(1, 1, 0, 0);
            oneMinus = 0;
        }

        if (_layer == EcoProgLayers.Pressure)
        {
            _textureDebug = Shader.GetGlobalTexture("_FluidPressureTex");
            channels = new Vector4(1, 0, 0, 0);
            oneMinus = 0;
        }

        if (_layer == EcoProgLayers.SoilQuality || _layer == EcoProgLayers.Presence)
        {
            _textureDebug = Shader.GetGlobalTexture("_SoilQualityTex");
            channels = new Vector4(1, 0, 0, 0);
            oneMinus = _layer == EcoProgLayers.Presence ? 1 : 0;
        }

        if (_layer == EcoProgLayers.SoilAttractivity)
        {
            _textureDebug = Shader.GetGlobalTexture("_SoilAttractivityTex");
            channels = new Vector4(1, 1, 0, 0);
            oneMinus = 0;
        }

        if (_layer == EcoProgLayers.Growth1 ||
            _layer == EcoProgLayers.Growth2 ||
            _layer == EcoProgLayers.Growth3 ||
            _layer == EcoProgLayers.Growth4)
        {
            _textureDebug = Shader.GetGlobalTexture("_GrowthCyclesTex");
            if (_layer == EcoProgLayers.Growth1) channels = new Vector4(1, 0, 0, 0);
            if (_layer == EcoProgLayers.Growth2) channels = new Vector4(0, 1, 0, 0);
            if (_layer == EcoProgLayers.Growth3) channels = new Vector4(0, 0, 1, 0);
            if (_layer == EcoProgLayers.Growth4) channels = new Vector4(0, 0, 0, 1);
            oneMinus = 0;
        }

        if (_layer == EcoProgLayers.Decay1 ||
            _layer == EcoProgLayers.Decay2 ||
            _layer == EcoProgLayers.Decay3 ||
            _layer == EcoProgLayers.Decay4)
        {
            _textureDebug = Shader.GetGlobalTexture("_DecayCyclesTex");
            if (_layer == EcoProgLayers.Decay1) channels = new Vector4(1, 0, 0, 0);
            if (_layer == EcoProgLayers.Decay2) channels = new Vector4(0, 1, 0, 0);
            if (_layer == EcoProgLayers.Decay3) channels = new Vector4(0, 0, 1, 0);
            if (_layer == EcoProgLayers.Decay4) channels = new Vector4(0, 0, 0, 1);
            oneMinus = 0;
        }

        if (_textureDebug != null)
        {
            _debugMaterial.SetTexture("_DebugTexture", _textureDebug);
            _debugMaterial.SetVector("_Channels", channels);
            _debugMaterial.SetFloat("_OneMinus", oneMinus);
        }

        _lastLayer = _layer;
    }
}
}