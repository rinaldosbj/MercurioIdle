#ifndef DYSON_OVERDRAW_INCLUDED
#define DYSON_OVERDRAW_INCLUDED
#if defined(DYSON_MEASURE_OVERDRAW)
RWStructuredBuffer<uint> _DysonOverdrawPixels : register(u1);
float4 _DysonOverdrawSize;
#define DYSON_OVERDRAW_EARLY [earlydepthstencil]
void DysonCountFragment(float2 position, uint surviving) {
    uint2 pixel=(uint2)position;
    if (pixel.x >= (uint)_DysonOverdrawSize.x || pixel.y >= (uint)_DysonOverdrawSize.y) return;
    uint address=(pixel.y*(uint)_DysonOverdrawSize.x+pixel.x)*10+DYSON_OVERDRAW_LAYER+surviving*5;
    InterlockedAdd(_DysonOverdrawPixels[address],1);
}
#else
#define DYSON_OVERDRAW_EARLY
void DysonCountFragment(float2 position, uint surviving) {}
#endif
#endif
