using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System;
using UnityEngine;
using System.Globalization;
using System.Runtime.InteropServices;

namespace WorldGeneration.DataStructures
{

    public enum ChunkState { DIRTY, READY, INVALID, ROOT, QUEUED }
    public enum OnMeshReadyAction { ALERT_PARENT, DISPOSE_CHILDREN }
    public enum DisposeState { NOTHING, POOL, FREE_MESH }
    
    public struct DensityResultData{
        
        [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction, WriteOnly]
        public NativeArray<sbyte> densityMap;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isEmpty;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isFull;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassInstanceData{
        public float4x4 matrix;
        public GrassInstanceData(float4x4 matrix){
            this.matrix = matrix;
        }
    }
    public struct DensityCacheInstance{
        [NativeDisableUnsafePtrRestriction]
        public IntPtr cachedDensityMap;
        public int3 cachedPos;
        public DensityCacheInstance(int3 pos){
            this.cachedDensityMap = default;
            this.cachedPos = pos;
        }
    }
    public struct TransitionVertexData{
        public float3 Primary;
        public float3 normal;
        public float3 Secondary;
        public int near;
        public TransitionVertexData(float3 Primary, float3 Secondary, int near, float3 normal){
            this.Primary = Primary;
            this.Secondary = Secondary;
            this.near = near;
            this.normal = normal;
        }
    }
    public struct VertexData{
        public float3 vertex;
        public float3 normal;
        public VertexData(float3 vertex,float3 normal){
            this.vertex = vertex;
            this.normal = normal;
        }
    }
    [Serializable]
    public struct NoiseProperties
    {
        public float surfaceLevel;
        public float freq;
        public float ampl;
        public int oct;
        public float seed;
        public float lacunarity;
        public float gain;
    }
    public class SimpleMeshData : IDisposable{
        public NativeArray<VertexData> vertexBuffer;
        public NativeArray<ushort> indexBuffer;
        public NativeArray<float> heightMap;
        //public static NativeArray<ushort> indices;

        public bool IsCreated{
            get{
                return indexBuffer.IsCreated;
            }
        }
        public void Dispose(){
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            heightMap.Dispose();
        }
    }
    public struct DensityGenerator{
        
        /*public static sbyte FinalNoise(float3 worldPos, NoiseProperties noiseProperties)
        {
            //pos -= depthMultiplier;
            float value = SurfaceNoise2D(new float2(worldPos.x, worldPos.z), noiseProperties);
            float yPos = noiseProperties.surfaceLevel + worldPos.y;
            float density = (value + noiseProperties.surfaceLevel - yPos) * 0.1f;
            return Convert.ToSByte(math.clamp(-density * 127f, -127f, 127f));
        }*/
        public static float SurfaceNoise2D(float2 worldPos, NoiseProperties noiseProperties, bool ad)
        {
            return SurfaceNoise2D(worldPos, noiseProperties.ampl, noiseProperties.freq, (int)noiseProperties.seed, noiseProperties.oct, noiseProperties.lacunarity, noiseProperties.gain, ad);
        }
        /*public static sbyte FinalNoise(float3 worldPos, float ampl, float freq, int seed, int oct, float lacunarity, float gain)
        {   
            //pos -= depthMultiplier;
            float value = SurfaceNoise2D(new float2(worldPos.x, worldPos.z), ampl, freq, seed, oct, lacunarity, gain);
            float yPos = worldPos.y;
            float density = (value - yPos) * 0.5f;
            return Convert.ToSByte(math.clamp(-density * 127f, -127f, 127f));
        }*/
        public static sbyte HeightMapToIsosurface(float3 worldPos, float height){
            float yPos = worldPos.y;
            float density = (height - yPos) * 0.5f;
            return (sbyte)(math.clamp(-density * 127f, -127f, 127f));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float SurfaceNoise2D(float2 worldPos, float ampl, float freq, int seed, int oct, float lacunarity, float gain, bool ad)
        {
            float total = 0;
            float2 dsum = 0;
            if(ad){
                for (int i = 0; i < oct; i++)
                {
                    //Unity.Burst.CompilerServices.Loop.ExpectVectorized();
                    float3 n = noise.srdnoise(math.float2(worldPos * freq));
                    dsum += new float2(n.y, n.z);
                    total += (n.x + 1) * 0.5f * ampl* (1/(1+math.dot(dsum, dsum)));

                    ampl *= gain;
                    freq *= lacunarity;
                }
            }
            else{
                for (int i = 0; i < oct; i++)
                {
                    //Unity.Burst.CompilerServices.Loop.ExpectVectorized();
                    total += (noise.snoise(math.float2(worldPos * freq)) + 1) * 0.5f * ampl;
                    ampl *= gain;
                    freq *= lacunarity;
                }
            }
            //total = total % 5f;
            //total /= oct;
            return total;
        }
        public static float SurfaceNoise3D(float3 worldPos, float ampl, float freq, int seed, int oct)
        {
            float total = 0;
            var _ampl = ampl;
            var _freq = freq;
            for (int i = 0; i < oct; i++)
            {
                total += (noise.snoise((worldPos + seed) * freq) + 1) * 0.5f * _ampl;

                _ampl *= 2;
                _freq *= 2f;
            }
            //total = total % 5f;
            //total /= oct;
            return total;
        }
    }
    public class NoiseGraphInput{
        public string generatorString;
        public string generator2DString;
        public float[] previewValues;
        public float this[int index]{
            get{
            return previewValues[index];
            }   
            set{
                previewValues[index] = value;
            }
        }
    }
    public struct MeshData : System.IDisposable{
        
        public NativeList<TransitionVertexData> vertexBuffer;
        public NativeList<ushort> indexBuffer;

        public MeshData(NativeList<TransitionVertexData> vertexBuffer, NativeList<ushort> indexBuffer){
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
        }
        public void ClearBuffers(){
            MemoryManager.ClearArray(vertexBuffer.AsArray(), vertexBuffer.Length);
            vertexBuffer.Length = 0;
            vertexBuffer.Capacity = MemoryManager.maxVertexCount;
            MemoryManager.ClearArray(indexBuffer.AsArray(), indexBuffer.Length);
            indexBuffer.Length = 0;
            indexBuffer.Capacity = MemoryManager.maxVertexCount;
        }
        public bool IsCreated{
            get{
                return vertexBuffer.IsCreated;
            }
        }
        public void Dispose(){
            indexBuffer.Dispose();
            vertexBuffer.Dispose();
        }
    }
    public struct ReuseCell{
        ushort x;
        ushort y;
        ushort z;
        public byte caseIdx;
        public ReuseCell(ushort value){
            x = value;
            y = value;
            z = value;
            caseIdx = 0;
        }
        public ushort this[int index]{
            get{
                switch(index){
                    case 1: return x;
                    case 2: return y;
                    case 3: return z;
                    
                    default: throw new System.IndexOutOfRangeException();
                }
            }set{
                switch(index){
                    case 1: x = value; break;
                    case 2: y = value; break;
                    case 3: z = value; break;
                    
                    default: throw new System.IndexOutOfRangeException();
                }
            }
        }
    }
    public struct TempBuffer : IDisposable{
        public NativeArray<ReuseCell> vertexIndices;
        public NativeArray<CellIndices> transitionVertexIndices;
        public TempBuffer(NativeArray<ReuseCell> vertexIndices, NativeArray<CellIndices> transitionVertexIndices){
            this.vertexIndices = vertexIndices;
            this.transitionVertexIndices = transitionVertexIndices;
        }
        public void ClearBuffers(){
            MemoryManager.ClearArray(vertexIndices, vertexIndices.Length);
            MemoryManager.ClearArray(transitionVertexIndices, transitionVertexIndices.Length);
        }
        public void Dispose(){
            vertexIndices.Dispose();
            transitionVertexIndices.Dispose();
        }
    }
    public class Utils{
        public static string floatToString(float value){
            return value.ToString("F4", new CultureInfo("en-US"))+"f";
        }
        public static int XyzToIndex(int x, int y, int z, int size)
        {
            return x + size * (z + size * y);
        }
        public static int XyzToIndex(int3 index, int size)
        {
            return XyzToIndex(index.x, index.y, index.z, size);
        }
        public static int3 WorldPosToChunkPos(int3 worldPos, bool positionFix = true){
            //var chunkPos = math.select((int3)(math.floor(worldPos / ChunkManager.chunkResolution) * ChunkManager.chunkResolution), (int3)(math.floor(worldPos / ChunkManager.chunkResolution) * ChunkManager.chunkResolution - ChunkManager.chunkResolution), (worldPos < 0) & (worldPos % ChunkManager.chunkResolution != 0));
            
            var chunkPos = (int3)(math.floor(worldPos / ChunkManager.chunkResolution)) * ChunkManager.chunkResolution;
            if(positionFix)
                chunkPos -= math.select(new int3(0), new int3(ChunkManager.chunkResolution), (worldPos < 0) & (worldPos % ChunkManager.chunkResolution != 0));
            //if(worldPos.x < 0 && worldPos.x % ChunkManager.chunkResolution != 0) chunkPos.x -= ChunkManager.chunkResolution;
            //if(worldPos.y < 0 && worldPos.y % ChunkManager.chunkResolution != 0) chunkPos.y -= ChunkManager.chunkResolution;
            //if(worldPos.z < 0 && worldPos.z % ChunkManager.chunkResolution != 0) chunkPos.z -= ChunkManager.chunkResolution;
            return chunkPos;
        }
        public static int4 IndexToXyz(int index, int size)
        {
            int4 position = new int4(
                index % size,
                index / (size * size),
                index / size % size, 0);
            return position;
        }
        public static int2 IndexToXz(int index, int size){
            return new int2(index % size, index / size);
        }
        public static int XzToIndex(int2 index, int size){
            return index.y * size + index.x;
        }

        public static string DirectionMaskToString(byte dirMask){
            string value = "";
            if((dirMask & 0b_0000_0001) == 0b_0000_0001) value += "Front, ";
            if((dirMask & 0b_0000_0010) == 0b_0000_0010) value += "Back, ";
            if((dirMask & 0b_0000_0100) == 0b_0000_0100) value += "Up, ";
            if((dirMask & 0b_0000_1000) == 0b_0000_1000) value += "Down, ";
            if((dirMask & 0b_0001_0000) == 0b_0001_0000) value += "Left, ";
            if((dirMask & 0b_0010_0000) == 0b_0010_0000) value += "Right, ";
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x3 DecodeNeighborMask(byte dirMask){
            return new bool2x3(new bool2((dirMask & 0b_0000_0001) == 0b_0000_0001, (dirMask & 0b_0000_0010) == 0b_0000_0010), 
                                                    new bool2((dirMask & 0b_0000_0100) == 0b_0000_0100, (dirMask & 0b_0000_1000) == 0b_0000_1000),
                                                    new bool2((dirMask & 0b_0010_0000) == 0b_0010_0000, (dirMask & 0b_0001_0000) == 0b_0001_0000));
            
        }
    }
}