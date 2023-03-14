using Unity.Collections;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.FinalNoise(pos + new float3(DensityGenerator.SurfaceNoise2D(pos2D, 96.0000f, 0.0024f, -999875, 2),0,0.0000f), 48.0000f, 0.0060f, -999875, 2,0);
    }
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D){
        return DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D, 96.0000f, 0.0024f, -999875, 2),0.0000f), 48.0000f, 0.0060f, -999875, 2);
    }
}