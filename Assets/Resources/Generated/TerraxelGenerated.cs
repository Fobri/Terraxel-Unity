using Unity.Collections;
using Unity.Mathematics;
using Terraxel.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    static readonly FastNoiseLite props0 = new FastNoiseLite(1337, 0.0070f, 2, 2.0000f, 0.3000f);
static readonly FastNoiseLite props1 = new FastNoiseLite(1337, 0.0070f, 2, 2.0000f, 0.3000f);
static readonly FastNoiseLite props2 = new FastNoiseLite(1337, 0.0030f, 3, 3.0000f, 0.3000f);
static readonly FastNoiseLite props4 = new FastNoiseLite(1337, 0.0004f, 2, 3.0000f, 0.5000f);
static readonly FastNoiseLite props5 = new FastNoiseLite(1337, 0.0005f, 4, 3.0000f, 0.5000f);
static readonly FastNoiseLite props8 = new FastNoiseLite(1337, 0.0004f, 3, 5.0000f, 0.3000f);
static readonly FastNoiseLite props6 = new FastNoiseLite(1337, 0.0002f, 2, 2.0000f, 2.0000f);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos, int seed = 1337){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.HeightMapToIsosurface(pos, TerraxelGenerated.GenerateDensity(pos2D, seed));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D, int seed = 1337){
        float op6 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(1.0000f,1.0000f), 240.0000f, props6, seed);
float op0 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(253.0000f,43.0000f), 19.2000f, props0, seed);
float op1 = DensityGenerator.SurfaceNoise2D(pos2D, 19.2000f, props1, seed);
float op2 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op1,op0), 48.0000f, props2, seed);
float op3 = (op2 * 2);
float op4 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op3,op3), 24.0000f, props4, seed);
float op5 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op4,op4), 192.0000f, props5, seed);
float op7 = (op5 * 2);
float op8 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op7,op7), 120.0000f, props8, seed);
float op9 = (op8 + op6);


        return op9;
    }
}