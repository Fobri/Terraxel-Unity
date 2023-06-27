#pragma kernel CSMain
#include "Assets/Shaders/FastNoiseLite/FastNoiseLite.hlsl"

int seed;

float noise(float2 worldPos, float amplitude, fnl_state props){
    return fnlGetNoise2D(props, worldPos.x, worldPos.y) * amplitude;
}

static const fnl_state props0 = fnlCreateState(seed, 0.00700f, 2, 2.00000f, 0.30000f);
static const fnl_state props1 = fnlCreateState(seed, 0.00700f, 2, 2.00000f, 0.30000f);
static const fnl_state props2 = fnlCreateState(seed, 0.00300f, 3, 3.00000f, 0.30000f);
static const fnl_state props4 = fnlCreateState(seed, 0.00040f, 2, 3.00000f, 0.50000f);
static const fnl_state props5 = fnlCreateState(seed, 0.00050f, 4, 3.00000f, 0.50000f);
static const fnl_state props8 = fnlCreateState(seed, 0.00040f, 3, 5.00000f, 0.30000f);
static const fnl_state props7 = fnlCreateState(seed, 0.00003f, 2, 2.00000f, 0.50000f);


float finalNoise(int3 pos){
    float op0 = noise(pos.xz + float2(253.00000f,43.00000f), 19.20000f, props0);
float op1 = noise(pos.xz + float2(0.00000f,0.00000f), 19.20000f, props1);
float op2 = noise(pos.xz + float2(op1,op0), 48.00000f, props2);
float op3 = (op2 * 2);
float op4 = noise(pos.xz + float2(op3,op3), 24.00000f, props4);
float op5 = noise(pos.xz + float2(op4,op4), 192.00000f, props5);
float op6 = (op5 * 2);
float op7 = noise(pos.xz + float2(op6,op6), 960.00000f, props7);
float op8 = noise(pos.xz + float2(op6,op6), 72.00000f, props8);
float op9 = (op8 + op7);


    float value = op9;
    return value;
}