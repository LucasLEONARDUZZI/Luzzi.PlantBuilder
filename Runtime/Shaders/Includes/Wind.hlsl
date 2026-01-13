#ifndef WIND_INCLUDE
#define WIND_INCLUDE

float3 WindOffsetWS(float3 positionWS, float3 direction, float frequency, float amplitude, float speed, float range)
{
    direction = SafeNormalize(direction);
    
    float along = dot(positionWS, direction) * frequency;
    float wave1 = sin(along + _Time.y * speed);
    // TODO second wave for more noise?
    
    float localY = positionWS.y - GetObjectPositionWS().y;
    
    float distFactor = 1.0 / max(1e-06, range);
    return localY * distFactor * wave1 * amplitude;
}

float3 SimpleOffsetWS(float3 positionWS, float3 offset, float amplitude, float range)
{
    float localY = positionWS.y - GetObjectPositionWS().y;
    float distFactor = 1.0 / max(1e-06, range);
    
    return offset * localY * distFactor * amplitude;
}

#endif // WIND_INCLUDE