    
// Fake Planar Reflection shader
Shader "Custom/FPR_Master"
{
    Properties
    {
        [Header(MAIN SETTINGS)]
        [Space(5)]
        _AtlasMap("Atlas Texture", 2D) = "black" {}

        [Header(GROWTH)]
        [Space(5)]
        _ProgressionRange("Progression Range", Float) = 0.5
        
        [Header(DECAY)]
        [Space(5)]
        _Luminosity("Luminosity", Float) = 0.5
        _Saturation("Saturation", Float) = 0.5

        [Header(FAKE PLANAR REFLECTION)]
        [Space(5)]
        _ReflectionNoiseMap("Noise Map", 2D) = "black" {}
        _ReflectionColor("Color", Color) = (0.6,0.6,0.6,0.5)
        _ReflectionDeformFreq("Deform Frequency", Float) = 64
        _ReflectionDeformAmp("Deform Amplitude", Float) = 0.1
        _ReflectionNoiseScale("Noise Scale", Float) = 1
        _ReflectionVelocityStrength("Water Velocity Strength", Float) = 0.5
        _PlaneHeight("Plane Height (Y)", Float) = 0.0
        _ReflectionTimeFactor("Reflection Time Factor", Float) = 1
        
        [Header(MISC)]
        [Space(5)]
        [Toggle] _Billboard("Y Billboard", Float) = 0
        [Toggle] _Flatten("Y Flatten", Float) = 0
        [Toggle] _UseCameraDirection("Use Camera Direction", Float) = 1
        [Toggle] _PerPivotLifeCycle ("Per Pivot Cycle", Float) = 0

        [Header(WIND)]
        [Space(5)]
        _WindDirection("World Direction", Vector) = (1,0,0)
        _WindFrequency("Frequency", Float) = 2
        _WindAmplitude("Amplitude", Float) = 0.1
        _WindLocalRange("Y Local Range", Float) = 5
        _WindSpeedFactor("SpeedFactor", Float) = 1

        [Header(WATER WAVES)]
        [Space(5)]
        _WaterWavingStrength("Strength", Float) = 1
        _WaterWavingLocalRange("Y Local Range", Float) = 5
        _PlaneSize("Plane Size", Float) = 5

        [Header(ALPHA)]
        [Space(5)]
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../Includes/Utils.hlsl"
        #include "../Includes/EaseCurves.hlsl"
        #include "../Includes/Wind.hlsl"
        #include "FPRCommon.hlsl"

        ENDHLSL

        // PASS 1 : Base object
        Pass
        {
            Name "FPR_BASE"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                return Vert(input, false);
            }

            half4 frag(Varyings input) : SV_Target
            {
                return FragBase(input);
            }

            ENDHLSL
        }

        // PASS 2 : Y mirror reflection object
        Pass
        {
            Name "FPR_REFLECTION"
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                return Vert(input, true);
            }

            half4 frag(Varyings input) : SV_Target
            {
                return FragReflection(input);
            }

            ENDHLSL
        }

    }

    FallBack Off
}
