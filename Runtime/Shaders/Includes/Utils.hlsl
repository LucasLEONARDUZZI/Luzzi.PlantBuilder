// only include once
#ifndef UTILS_INCLUDE
#define UTILS_INCLUDE

float2 Rotate2D(float2 uv, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2
        (
            c * uv.x - s * uv.y,
            s * uv.x + c * uv.y
        );
}

float RandomRange01(float2 seed)
{
    return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
}

float Scanning(float value, float range, float level)
{
    float invRange = 1.0 / (max(range, 1e-06));
    
    return 1.0 - saturate(((range + value) - (level + level * range)) * invRange);
}

float3 Saturation(float3 color, float saturation)
{
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722)); // same coeff as shader graph
    return lerp(luminance.xxx, color, saturation);
}

float3 GetObjectPositionWS()
{
    return unity_ObjectToWorld._m03_m13_m23;
}

float3 GetObjectScale()
{
    float3 xaxis = unity_ObjectToWorld._m00_m01_m02;
    float3 yaxis = unity_ObjectToWorld._m10_m11_m12;
    float3 zaxis = unity_ObjectToWorld._m20_m21_m22;

    return float3
    (
        length(xaxis),
        length(yaxis),
        length(zaxis)
    );
}

float3 GetCameraForwardWS()
{
    return normalize(UNITY_MATRIX_I_V._m02_m12_m22); // forward world (selon pipeline)
}

float GetChannel(float4 v, int i)
{
    return (i == 0) ? v.x : (i == 1) ? v.y : (i == 2) ? v.z : v.w;
}

#endif // UTILS_INCLUDE