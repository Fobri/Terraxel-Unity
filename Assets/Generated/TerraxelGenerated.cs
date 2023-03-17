using Unity.Collections;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.HeightMapToIsosurface(pos, TerraxelGenerated.GenerateDensity(pos2D));
    }
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D){
        return (DensityGenerator.SurfaceNoise2D(pos2D + new float2(0.0000f,DensityGenerator.SurfaceNoise2D(pos2D, 24.0000f, 0.0040f, 62, 2, 2.0000f, 2.0000f)), 19.2000f, 0.0004f, 62, 3, 2.0000f, 2.0000f) + DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 19.2000f, 0.0050f, 62, 2, 3.0000f, 2.0000f),0.0000f), 24.0000f, 0.0020f, 62, 2, 2.0000f, 2.0000f));
    }
}