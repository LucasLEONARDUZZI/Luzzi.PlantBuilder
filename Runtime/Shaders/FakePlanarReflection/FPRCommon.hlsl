// only include once the Fake Planar Reflection Common
#ifndef FPRCOMMON_INCLUDE
#define FPRCOMMON_INCLUDE

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv0 : TEXCOORD0; // Texture space for color and alpha (atlas)
    float4 uv1 : TEXCOORD1; // rgb = Lighting info (keep clear)
    float4 uv2 : TEXCOORD2; // xyz = baked growth offsets, w is growth progression
    float3 uv3 : TEXCOORD3; // xy = baked local xz pivots (for objects with several pivot like billboard), z = category
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv0 : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    float4 uv2 : TEXCOORD2;
    float3 uv3 : TEXCOORD3;
    float2 values : TEXCOORD4; // x = decay value, y = decayAlphaMask
    float3 positionWS : TEXCOORD5; // position in world space
};

// We declare this buffer of property to make the material SPR compatible (so it can be batched)
CBUFFER_START(UnityPerMaterial)
float _TimeSpeed;
float _ProgressionRange;
float _Luminosity, _Saturation;
float _ReflectionDeformFreq, _ReflectionDeformAmp, _ReflectionVelocityStrength, _ReflectionTimeFactor, _ReflectionNoiseScale;
float4 _ReflectionColor;
float _PlaneHeight;
float _Billboard, _Flatten, _UseCameraDirection, _PerPivotLifeCycle;
float3 _WindDirection;
float _WindFrequency, _WindAmplitude, _WindLocalRange, _WindSpeedFactor;
float _WaterWavingStrength, _WaterWavingLocalRange, _PlaneSize;
float _Cutoff;
CBUFFER_END

// We declare the atlas texture and its sampler
TEXTURE2D(_AtlasMap);
SAMPLER(sampler_AtlasMap);
// We declare the reflection noise map and its sampler
TEXTURE2D(_ReflectionNoiseMap);
SAMPLER(sampler_ReflectionNoiseMap);

// We retrieve velocity map create by the FluidSim2D.compute
TEXTURE2D(_FluidVelocityTex);
SAMPLER(sampler_FluidVelocityTex);

// Life cycle textures
TEXTURE2D(_GrowthCyclesTex);
SAMPLER(sampler_GrowthCyclesTex);
TEXTURE2D(_DecayCyclesTex);
SAMPLER(sampler_DecayCyclesTex);

// Soil quality texture
TEXTURE2D(_SoilQualityTex);
SAMPLER(sampler_SoilQualityTex);

float3 YBillboardPositionOS(float3 positionOS)
{
	// find frame vectors
    float3 forwardWS = (_UseCameraDirection > 0.5) ? GetCameraForwardWS() : _WorldSpaceCameraPos - GetObjectPositionWS();
    forwardWS.y = 0.0;
    forwardWS = normalize(-forwardWS);
    float3 upWS = float3(0.0, 1.0, 0.0);
    float3 rightWS = float3(forwardWS.z, 0.0, -forwardWS.x);
	
    float3 scaledPositionOS = positionOS * GetObjectScale();
	
    float3 forwardOffsetWS = rightWS * scaledPositionOS.x;
    float3 upOffsetWS = upWS * scaledPositionOS.y;
    float3 rightOffsetWS = forwardWS * scaledPositionOS.z;
	
    return TransformWorldToObject(GetObjectPositionWS() + forwardOffsetWS + upOffsetWS + rightOffsetWS);
}

float3 YFlattenOnCameraPlaneWS(float3 positionWS)
{
    float3 forward = (_UseCameraDirection > 0.5) ? GetCameraForwardWS() : _WorldSpaceCameraPos - GetObjectPositionWS();
    float3 normal = normalize(float3(-forward.x, 0, -forward.z));
    float3 origin = GetObjectPositionWS();
	
    return positionWS - normal * dot(positionWS - origin, normal);
}

// Vertex shader
Varyings Vert(Attributes input, bool isReflection)
{
    Varyings output;

	// Find pivot position : just add local offsets xz sotred in uv3.xy to the object position 
    float3 pivotPosition = GetObjectPositionWS() + float3(input.uv3.xy, 0);
    float2 position = _PerPivotLifeCycle > 0.5 ? pivotPosition.xz : GetObjectPositionWS().xz;
    float2 waterPlaneUV = 0.5 - (position / _PlaneSize);
	
	// Retrieve life cycles values
    float4 growthCycles = SAMPLE_TEXTURE2D_LOD(_GrowthCyclesTex, sampler_GrowthCyclesTex, waterPlaneUV, 0);
    float4 decayCycles = SAMPLE_TEXTURE2D_LOD(_DecayCyclesTex, sampler_DecayCyclesTex, waterPlaneUV, 0);
	
	// Setup growth and decay levels
    int category = input.uv3.z;
    float growthValue = GetChannel(growthCycles, category); // TODO remove saturate(cycleValue * 2.0); // growth happens half of the cycle
    float decayValue = GetChannel(decayCycles, category); // TODO remove saturate((cycleValue * 2.0) - 1.0); // decay happens the other half of the cycle
	
	// Growth mask
    float growthMask = 1.0 - Scanning(input.uv2.w, _ProgressionRange, growthValue);
    float3 growthOffset = input.uv2.xyz;
	
	// Get vertex position in object space
    float3 positionOS = input.positionOS.xyz;
    float3 grownPositionOS = input.positionOS.xyz - (growthOffset * EaseInCirc(growthMask)); // ease in circ give a more natural look (can be tweaked)
    float3 decayedPositionOS = float3(grownPositionOS.x, 0.0, grownPositionOS.z); // just squashed to the floor (TODO make better falling)
	
	// DecayFall mask
    decayValue += (length(growthOffset) * decayValue * 0.1); // happen before if far from growth center
    float decayFallMask = saturate(2.0 * (decayValue - 0.5)); // fall happens in the second half of the decay
	
    float3 newPositionOS = lerp(grownPositionOS, decayedPositionOS, EaseInOutCirc(decayFallMask)); // ease in out circ give a more natural look (can be tweaked)
	
	// Lerp positions object space
    if (_Billboard > 0.5) // billboard == true
    {
        float3 pivotOffset = float3(input.uv3.x, 0.0, input.uv3.y);
        newPositionOS -= pivotOffset; // offset the local centers to manage several pivots
        newPositionOS = YBillboardPositionOS(newPositionOS);
        newPositionOS += pivotOffset;
    }
	
	// Transform vertex position from object space to world space
    float3 positionWS = TransformObjectToWorld(newPositionOS);
	
	// Apply wind offset
    float windSpeed = _TimeSpeed * _WindSpeedFactor;
    float3 windOffsetWS = WindOffsetWS(positionWS, _WindDirection, _WindFrequency, _WindAmplitude, windSpeed, _WindLocalRange);
    positionWS += windOffsetWS;
	
	// Water plane offset
    float2 waterVelocity = SAMPLE_TEXTURE2D_LOD(_FluidVelocityTex, sampler_FluidVelocityTex, waterPlaneUV, 0).xy;
    float3 waterVelocityWS = float3(waterVelocity.x, 0.0, waterVelocity.y);
    float3 waterWaveOffsetWS = SimpleOffsetWS(positionWS, waterVelocityWS, _WaterWavingStrength, _WaterWavingLocalRange);
    positionWS -= waterWaveOffsetWS; // subtract because world space is inverted in unity's plane UV space
	
    if (isReflection)
    {
		// Mirror from planeHeight 
        float mirroredY = 2.0 * _PlaneHeight - positionWS.y;
		// Clamp to object position (pivot should at the base of the object)
        positionWS.y = min(mirroredY, GetObjectPositionWS().y);
		
        float4 positionHCS = TransformWorldToHClip(positionWS);
        float toPlane = abs(_PlaneHeight - positionWS.y);
        float deform = saturate(toPlane) * sin(toPlane * _ReflectionDeformFreq + _Time.y * _TimeSpeed); // * _ReflectionTimeFactor); // * _TimeSpeed);
        positionHCS.x += deform * _ReflectionDeformAmp;
		
        float4 positionWS4 = mul(UNITY_MATRIX_I_VP, positionHCS);
        positionWS = positionWS4.xyz / positionWS4.w;
    }

	// Flatten on camera plane (with y constraint)
    if (_Flatten > 0.5)
    {
        positionWS = YFlattenOnCameraPlaneWS(positionWS);
    }
	
	// UVs
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.uv2 = input.uv2;
    output.uv3 = input.uv3;
	
	// Values the fragment shader needs
    output.values.x = decayValue; // decay value
    float decayAlphaMask = decayValue * 2 - 1; // the fade happens in the second half of decay
    output.values.y = Scanning((1.0 - input.uv2.w), 0.0, decayAlphaMask);
	
	// Transform position from world space to clip space
    output.positionWS = positionWS;
    output.positionHCS = TransformWorldToHClip(positionWS);
	
    return output;
}

// Fragment shader for the base object
half4 FragBase(Varyings input)
{
    
    half4 baseColor = SAMPLE_TEXTURE2D(_AtlasMap, sampler_AtlasMap, input.uv0.xy);
    half3 decayColor = Saturation(baseColor.rgb, _Saturation);
    decayColor *= _Luminosity;
    
    float2 waterPlaneUV = 0.5 - (input.positionWS.xz / _PlaneSize);
    float soilQuality = SAMPLE_TEXTURE2D_LOD(_SoilQualityTex, sampler_SoilQualityTex, waterPlaneUV, 0).x;
    
    float decayColorMask = saturate(EaseOutCirc(input.values.x + (1 - soilQuality)));
    half3 color = lerp(baseColor.rgb, decayColor, decayColorMask);
	
    half alpha = lerp(baseColor.a, 0.0, input.values.y);
    clip(alpha - _Cutoff);

    return half4(color, alpha);
}

// Fragment shader for the reflected object
half4 FragReflection(Varyings input)
{
    //float2 waterPlaneUV = 0.5 - (input.positionWS / _PlaneSize);
    
    half4 atlasColor = SAMPLE_TEXTURE2D(_AtlasMap, sampler_AtlasMap, input.uv0.xy);

    float eps = 1e-06;
    float3 camPos = _WorldSpaceCameraPos;
    float3 toFrag = input.positionWS - camPos;
    float toFragDist = length(toFrag);
    float3 viewDir = (toFragDist > eps) ? (toFrag / toFragDist) : float3(0, 0, 1);
	
    half alpha = lerp(atlasColor.a, 0.0, input.values.y);
    float2 planeUV = float2(0, 0);
	
    if (abs(viewDir.y) > eps)
    {
        float t = (_PlaneHeight - camPos.y) / viewDir.y;
		
        bool hit = (t > 0.0) && (t < toFragDist);
        if (!hit)
        {
            alpha = 0;
        }
        else
        {
            if (t > 0.0)
            {
                float3 planePositionWS = camPos + (viewDir * t);
				
                planeUV = -planePositionWS.xz; // Plane UV is inverted from world position
                planeUV = (planeUV / _PlaneSize) + 0.5;

            }
        }
    }
	
    float2 waterVelocity = SAMPLE_TEXTURE2D_LOD(_FluidVelocityTex, sampler_FluidVelocityTex, planeUV, 0);
    
    half UVDeformFactor = SAMPLE_TEXTURE2D(_ReflectionNoiseMap, sampler_ReflectionNoiseMap, planeUV * _ReflectionNoiseScale * _PlaneSize).r;
    float2 UVDeform = planeUV + (UVDeformFactor * 0.5) + (_Time.y * _ReflectionTimeFactor * _TimeSpeed) + waterVelocity * _ReflectionVelocityStrength;
	
    half noiseMask = SAMPLE_TEXTURE2D(_ReflectionNoiseMap, sampler_ReflectionNoiseMap, UVDeform).r;
    float toPlaneDistY = abs(input.positionWS.y - _PlaneHeight);
	
    half heightMask = EaseInCirc(1 - saturate(toPlaneDistY));
	
    float reflectionMask = max(heightMask, noiseMask) + saturate(-viewDir.y * 0.0);
    alpha *= reflectionMask;
	
    clip(alpha - _Cutoff);
	
    return float4(_ReflectionColor.rgb * heightMask, 1);
}

#endif // FPRCOMMON_INCLUDE