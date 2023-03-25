using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System;
using UnityEngine;
using System.Globalization;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;
using UnityEngine.Rendering;

namespace Terraxel.DataStructures
{

    public enum ChunkState { DIRTY, READY, INVALID, ROOT, QUEUED }
    public enum OnMeshReadyAction { ALERT_PARENT, DISPOSE_CHILDREN }
    public enum DisposeState { NOTHING, POOL, FREE_MESH }
    
    public struct DensityResultData{
        
        //[NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction, WriteOnly]
        public NativeArray<sbyte> densityMap;
        public AsyncGPUReadbackRequest request;
        public int3 pos;
        /*[WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isEmpty;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isFull;*/
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct InstanceData{
        public float4x4 matrix;
        public InstanceData(float4x4 matrix){
            this.matrix = matrix;
        }
    }
    public struct DensityCacheInstance{
        [NativeDisableUnsafePtrRestriction]
        public IntPtr cachedDensityMap;
        public int3 cachedPos;
        public int3 lastEmptyChunk;
        public int3 lastFullChunk;
        public DensityCacheInstance(int3 pos){
            this.cachedDensityMap = default;
            this.cachedPos = pos;
            lastEmptyChunk = new int3(int.MaxValue);
            lastFullChunk = new int3(int.MaxValue);
        }
    }
    public struct TransitionVertexData{
        public float3 Primary;
        public float3 normal;
        public float3 Secondary;
        public int near;
        public float textureIndex;
        public TransitionVertexData(float3 Primary, float3 Secondary, int near, float3 normal, float textureIndex){
            this.Primary = Primary;
            this.Secondary = Secondary;
            this.near = near;
            this.normal = normal;
            this.textureIndex = textureIndex;
        }
    }
    public struct VertexData{
        public float3 vertex;
        public float3 normal;
        public float textureIndex;
        public VertexData(float3 vertex,float3 normal, float textureIndex){
            this.vertex = vertex;
            this.normal = normal;
            this.textureIndex = textureIndex;
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
    [Serializable]
    public class InstancingData{
        [SerializeField]
        public Mesh mesh;
        [SerializeField]
        public Material material;
    }
    public struct JobInstancingData : IDisposable{
        public InstanceProperties c0;
        public InstanceProperties c1;
        public InstanceProperties c2;
        public InstanceProperties c3;
        public InstanceProperties c4;

        public void Dispose(){
            MemoryManager.ReturnInstanceData(c0.matrices);
            MemoryManager.ReturnInstanceData(c1.matrices);
            MemoryManager.ReturnInstanceData(c2.matrices);
            MemoryManager.ReturnInstanceData(c3.matrices);
            MemoryManager.ReturnInstanceData(c4.matrices);
        }
    }
    public struct InstanceProperties{
        public NativeList<InstanceData> matrices;
        public float3 lastInstancePos;
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
            return SurfaceNoise2D(worldPos, noiseProperties.ampl, noiseProperties.freq, noiseProperties.oct, noiseProperties.lacunarity, noiseProperties.gain);
        }
        /*public static sbyte FinalNoise(float3 worldPos, float ampl, float freq, int seed, int oct, float lacunarity, float gain)
        {   
            //pos -= depthMultiplier;
            float value = SurfaceNoise2D(new float2(worldPos.x, worldPos.z), ampl, freq, seed, oct, lacunarity, gain);
            float yPos = worldPos.y;
            float density = (value - yPos) * 0.5f;
            return Convert.ToSByte(math.clamp(-density * 127f, -127f, 127f));
        }*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte HeightMapToIsosurface(float3 worldPos, float height){
            float density = (height - worldPos.y) * 0.5f;
            density *= -127f;
            density = math.max(density, -127f);
            density = math.min(density, 127f);
            return (sbyte)density;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SurfaceNoise2D(float2 worldPos, float ampl, float freq, int oct, float lacunarity, float gain)
        {
            FastNoiseLite noise = new FastNoiseLite(1337);
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFrequency(freq);
            noise.SetFractalOctaves(oct);
            noise.SetFractalLacunarity(lacunarity);
            noise.SetFractalGain(gain);
            return noise.GetNoise(worldPos.x, worldPos.y) * ampl;
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
        public string scriptString;
        public string computeString;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion AlignWithNormal(float3 normal, Unity.Mathematics.Random rng){
            float4 q = 0;
            var axis = new float3(0,1,0);
            float3 a = math.cross(axis, normal);
            q.xyz = a;
            q.w = 1 + math.dot(axis, normal);
            quaternion randomRotation = quaternion.RotateY(rng.NextFloat(0, 360));
            return (quaternion)math.normalize(math.mul((quaternion)q, randomRotation));
        }
        public static string floatToString(float value){
            return value.ToString("F4", new CultureInfo("en-US"))+"f";
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XyzToIndex(int x, int y, int z, int size)
        {
            return x + size * z + size * size * y;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XyzToIndex(int3 index, int size)
        {
            return XyzToIndex(index.x, index.y, index.z, size);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 WorldPosToChunkPos(int3 worldPos){
            //var chunkPos = math.select((int3)(math.floor(worldPos / ChunkManager.chunkResolution) * ChunkManager.chunkResolution), (int3)(math.floor(worldPos / ChunkManager.chunkResolution) * ChunkManager.chunkResolution - ChunkManager.chunkResolution), (worldPos < 0) & (worldPos % ChunkManager.chunkResolution != 0));
           
            var chunkPos = (int3)(math.floor((float3)worldPos / ChunkManager.chunkResolution)) * ChunkManager.chunkResolution;
            //(int4)(math.floor((int4)worldPos / ChunkManager.chunkResolution)) * ChunkManager.chunkResolution;
            //if(positionFix)
                //chunkPos -= math.select(new int4(0), new int4(ChunkManager.chunkResolution), (worldPos < 0) & (worldPos % ChunkManager.chunkResolution != 0));
            //if(worldPos.x < 0 && worldPos.x % ChunkManager.chunkResolution != 0) chunkPos.x -= ChunkManager.chunkResolution;
            //if(worldPos.y < 0 && worldPos.y % ChunkManager.chunkResolution != 0) chunkPos.y -= ChunkManager.chunkResolution;
            //if(worldPos.z < 0 && worldPos.z % ChunkManager.chunkResolution != 0) chunkPos.z -= ChunkManager.chunkResolution;
            return chunkPos;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4 IndexToXyz(int index, int size)
        {
            int4 position = new int4(
                index % size,
                index / (size * size),
                index / size % size, 0);
            return position;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 IndexToXz(int index, int size){
            return new int2(index % size, index / size);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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