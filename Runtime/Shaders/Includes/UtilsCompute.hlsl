
/**
 * UtilsCompute.hlsl
 *
 * Bibliothèque utilitaire pour compute shaders Unity.
 * Fournit des fonctions pour la lecture sécurisée de textures (float, float2, float4),
 * le sampling bilinéaire personnalisé, la génération de nombres pseudo-aléatoires,
 * et la manipulation de canaux float4.
 *
 * Utilisé dans :
 *   - Fluid2D.compute
 *   - EcoProg.compute
 *
 */

#ifndef UTILS_COMPUTE_INCLUDED
#define UTILS_COMPUTE_INCLUDED

float ReadScalar(Texture2D<float> tex, int2 size, int2 p)
{
    p = clamp(p, int2(0, 0), int2(size.x - 1, size.y - 1));
    return tex.Load(int3(p, 0));
}

float2 ReadVector2(Texture2D<float2> tex, int2 size, int2 p)
{
    p = clamp(p, int2(0, 0), int2(size.x - 1, size.y - 1));
    return tex.Load(int3(p, 0));
}

float4 ReadVector4(Texture2D<float4> tex, int2 size, int2 p)
{
    p = clamp(p, int2(0, 0), int2(size.x - 1, size.y - 1));
    return tex.Load(int3(p, 0));
}

// Bilinear sampling (float2)
float2 SampleBilinearVelocity(Texture2D<float2> tex, int2 size, float2 uv)
{
	// Convert uv to texel
    float2 position = uv * float2(size.x, size.y) - 0.5;
	
	// Find corners texels
    int2 i0 = int2(floor(position));
    int2 i1 = i0 + int2(1, 0);
    int2 i2 = i0 + int2(0, 1);
    int2 i3 = i0 + int2(1, 1);

    float2 fracPos = frac(position);

	// Load values at corners
    float2 v0 = ReadVector2(tex, size, i0);
    float2 v1 = ReadVector2(tex, size, i1);
    float2 v2 = ReadVector2(tex, size, i2);
    float2 v3 = ReadVector2(tex, size, i3);
	
	// We need to lerp values to sample the texel center
	//..v0.○----->•<-----○.v1..
	//..........a.|............
	//............v............
	//............•.result.....
	//............^............
	//..........b.|............
	//..v2.○----->•<-----○.v3..
	
	// Lerp horizontally lower and upper corners
    float2 a = lerp(v0, v1, fracPos.x);
    float2 b = lerp(v2, v3, fracPos.x);
	// Lerp both values vertically to finish the sampling
    return lerp(a, b, fracPos.y);
}

float RandomRange01(float2 seed)
{
    return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
}

float GetChannel(float4 v, int i)
{
    return (i == 0) ? v.x : (i == 1) ? v.y : (i == 2) ? v.z : v.w;
}

void SetChannel(inout float4 v, int i, float value)
{
    if (i == 0)
    {
        v.x = value;
    }
    else if (i == 1)
    {
        v.y = value;
    }
    else if (i == 2)
    {
        v.z = value;
    }
    else if (i == 3)
    {
        v.w = value;
    }
}

#endif // UTILS_COMPUTE_INCLUDED