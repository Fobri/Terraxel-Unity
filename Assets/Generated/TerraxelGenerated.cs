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
        return (DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 19.2000f, 0.0050f, 62, 2, 3.0000f, 2.0000f, false),0.0000f), 24.0000f, 0.0020f, 62, 3, 2.0000f, 1.0000f, false) + DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 19.2000f, 0.0050f, 62, 2, 3.0000f, 2.0000f, false),0.0000f), -4.8000f, 0.0010f, 62, 3, 3.0000f, 1.3000f, true));
    }
}