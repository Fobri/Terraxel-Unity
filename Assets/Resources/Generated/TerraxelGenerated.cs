using Unity.Collections;
using Unity.Mathematics;
using Terraxel.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    static readonly FastNoiseLite props0 = new FastNoiseLite(1337, 0.00700f, 2, 2.00000f, 0.30000f);
static readonly FastNoiseLite props1 = new FastNoiseLite(1337, 0.00700f, 2, 2.00000f, 0.30000f);
static readonly FastNoiseLite props2 = new FastNoiseLite(1337, 0.00300f, 3, 3.00000f, 0.30000f);
static readonly FastNoiseLite props4 = new FastNoiseLite(1337, 0.00040f, 2, 3.00000f, 0.50000f);
static readonly FastNoiseLite props5 = new FastNoiseLite(1337, 0.00050f, 4, 3.00000f, 0.50000f);
static readonly FastNoiseLite props8 = new FastNoiseLite(1337, 0.00040f, 3, 5.00000f, 0.30000f);
static readonly FastNoiseLite props7 = new FastNoiseLite(1337, 0.00003f, 2, 2.00000f, 0.50000f);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos, int seed = 1337){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.HeightMapToIsosurface(pos, TerraxelGenerated.GenerateDensity(pos2D, seed));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D, int seed = 1337){
        float op0 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(253.00000f,43.00000f), 19.20000f, props0, seed);
float op1 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(0.00000f,0.00000f), 19.20000f, props1, seed);
float op2 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op1,op0), 48.00000f, props2, seed);
float op3 = (op2 * 2);
float op4 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op3,op3), 24.00000f, props4, seed);
float op5 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op4,op4), 192.00000f, props5, seed);
float op6 = (op5 * 2);
float op7 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op6,op6), 960.00000f, props7, seed);
float op8 = DensityGenerator.SurfaceNoise2D(pos2D + new float2(op6,op6), 72.00000f, props8, seed);
float op9 = (op8 + op7);


        return op9;
    }
}