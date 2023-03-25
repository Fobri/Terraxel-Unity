using Unity.Collections;
using Unity.Mathematics;
using Terraxel.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.HeightMapToIsosurface(pos, TerraxelGenerated.GenerateDensity(pos2D));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D){
        return (DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 24.0000f, 0.0050f,2, 3.0000f, 0.5000f),DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 24.0000f, 0.0050f,2, 3.0000f, 0.5000f)), 40.8000f, 0.0005f,4, 4.0000f, 0.3000f) + DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(1.0000f,1.0000f), 24.0000f, 0.0070f,2, 2.0000f, 0.8000f),0.0000f), 48.0000f, 0.0016f,2, 3.0000f, 0.7000f));
    }
}