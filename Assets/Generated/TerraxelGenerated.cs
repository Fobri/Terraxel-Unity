using Unity.Collections;
using Unity.Mathematics;
using Terraxel.DataStructures;
using System.Runtime.CompilerServices;


public class TerraxelGenerated
{
    static readonly FastNoiseLite props1 = new FastNoiseLite(1337, 0.0010f, 4, 4.0000f, 0.3000f);
static readonly FastNoiseLite props0 = new FastNoiseLite(1337, 0.0050f, 2, 3.0000f, 0.5000f);
static readonly FastNoiseLite props3 = new FastNoiseLite(1337, 0.0030f, 2, 3.0000f, 0.7000f);
static readonly FastNoiseLite props2 = new FastNoiseLite(1337, 0.0070f, 2, 2.0000f, 0.8000f);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GenerateDensity(float3 pos){
        float2 pos2D = new float2(pos.x, pos.z);
        return DensityGenerator.HeightMapToIsosurface(pos, TerraxelGenerated.GenerateDensity(pos2D));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GenerateDensity(float2 pos2D){
        return (DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 24.0000f, props0),DensityGenerator.SurfaceNoise2D(pos2D + new float2(457.0000f,700.0000f), 24.0000f, props0)), 40.8000f, props1) + DensityGenerator.SurfaceNoise2D(pos2D + new float2(DensityGenerator.SurfaceNoise2D(pos2D + new float2(1.0000f,1.0000f), 24.0000f, props2),0.0000f), 48.0000f, props3));
    }
}