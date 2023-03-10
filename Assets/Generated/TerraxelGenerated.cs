using Unity.Collections;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.FinalNoise(pos + new float3(DensityGenerator.SurfaceNoise2D(pos2D, 24.0000f, 0.0100f, -968506, 1),0,0.0000f), 24.0000f, 0.0120f, -999875, 2,0);
    }
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D){
        return DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D, 24.0000f, 0.0100f, -968506, 1),0.0000f), 24.0000f, 0.0120f, -968506, 2);
    }
}