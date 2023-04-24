#pragma kernel CSMain
#include "Assets/Shaders/FastNoiseLite/FastNoiseLite.hlsl"

int seed;

float noise(float2 worldPos, float amplitude, fnl_state props){
    return fnlGetNoise2D(props, worldPos.x, worldPos.y) * amplitude;
}

static const fnl_state props0 = fnlCreateState(seed, 0.0070f, 2, 2.0000f, 0.3000f);
static const fnl_state props1 = fnlCreateState(seed, 0.0070f, 2, 2.0000f, 0.3000f);
static const fnl_state props2 = fnlCreateState(seed, 0.0030f, 3, 3.0000f, 0.3000f);
static const fnl_state props4 = fnlCreateState(seed, 0.0004f, 2, 3.0000f, 0.5000f);
static const fnl_state props5 = fnlCreateState(seed, 0.0005f, 4, 3.0000f, 0.5000f);
static const fnl_state props8 = fnlCreateState(seed, 0.0004f, 3, 5.0000f, 0.3000f);
static const fnl_state props6 = fnlCreateState(seed, 0.0002f, 2, 2.0000f, 2.0000f);


float finalNoise(int3 pos){
    float op6 = noise(pos.xz + float2(1.0000f,1.0000f), 240.0000f, props6);
float op0 = noise(pos.xz + float2(253.0000f,43.0000f), 19.2000f, props0);
float op1 = noise(pos.xz, 19.2000f, props1);
float op2 = noise(pos.xz + float2(op1,op0), 48.0000f, props2);
float op3 = (op2 * 2);
float op4 = noise(pos.xz + float2(op3,op3), 24.0000f, props4);
float op5 = noise(pos.xz + float2(op4,op4), 192.0000f, props5);
float op7 = (op5 * 2);
float op8 = noise(pos.xz + float2(op7,op7), 120.0000f, props8);
float op9 = (op8 + op6);


    float value = op9;
    return value;
}