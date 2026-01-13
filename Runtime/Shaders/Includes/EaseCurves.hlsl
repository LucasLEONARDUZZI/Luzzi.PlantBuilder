// only include once
#ifndef EASECURVES_INCLUDE
#define EASECURVES_INCLUDE

float EaseInCirc(float value)
{
    return 1.0 - sqrt(max(0.0, 1.0 - (value * value)));

}

float EaseOutCirc(float value)
{
    float t = (1.0 - value);
    return sqrt(max(0.0, 1.0 - (t * t)));

}

float EaseInOutCirc(float value)
{
    value = saturate(value);
    float mid = step(0.5, value);
    
    float easeIn = EaseInCirc(value * 2.0) * 0.5;
    float easeOut = EaseOutCirc((value * 2.0) - 1.0) * 0.5 + 0.5;

    return lerp(easeIn, easeOut, mid);
}

#endif