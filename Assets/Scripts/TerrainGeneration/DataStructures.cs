using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System;
using UnityEngine;

namespace WorldGeneration.DataStructures
{

    
    public struct DensityResultData{
        
        [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction, WriteOnly]
        public NativeArray<sbyte> densityMap;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isEmpty;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isFull;
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
    }
    public class SimpleMeshData{
        public GameObject worldObject;
        public NativeArray<VertexData> buffer;
        public NativeArray<float> heightMap;
        public static NativeArray<ushort> indices;
    }
    public struct DensityGenerator{
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte FinalNoise(float3 worldPos, NoiseProperties noiseProperties)
        {
            //pos -= depthMultiplier;
            float value = SurfaceNoise2D(new float2(worldPos.x, worldPos.z), noiseProperties);
            float yPos = noiseProperties.surfaceLevel + worldPos.y;
            float density = (value + noiseProperties.surfaceLevel - yPos) * 0.1f;
            return Convert.ToSByte(math.clamp(-density * 127f, -127f, 127f));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SurfaceNoise2D(float2 worldPos, NoiseProperties noiseProperties)
        {
            float total = 0;
            var _ampl = noiseProperties.ampl;
            var _freq = noiseProperties.freq;
            for (int i = 0; i < noiseProperties.oct; i++)
            {
                total += noise.snoise(math.float2((worldPos.x + noiseProperties.seed) * _freq, (worldPos.y + noiseProperties.seed) * _freq)) * _ampl;

                //_ampl *= 2;
                _freq *= 0.5f;
            }
            //total = total % 5f;
            return total / noiseProperties.oct;
        }
        public static float SurfaceNoise2D(float2 worldPos, float ampl, float freq, int seed, int oct)
        {
            float total = 0;
            var _ampl = ampl;
            var _freq = freq;
            for (int i = 0; i < oct; i++)
            {
                total += noise.snoise(math.float2((worldPos.x + seed) * _freq, (worldPos.y + seed) * _freq)) * _ampl;

                //_ampl *= 2;
                _freq *= 0.5f;
            }
            //total = total % 5f;
            return total / oct;
        }
    }
    public struct MeshData : System.IDisposable{
        
        public NativeList<TransitionVertexData> vertexBuffer;
        public NativeList<ushort> indexBuffer;
        [WriteOnly]
        public NativeList<Matrix4x4> grassPositions;

        public MeshData(NativeList<TransitionVertexData> vertexBuffer, NativeList<ushort> indexBuffer, NativeList<Matrix4x4> grassPositions){
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
            this.grassPositions = grassPositions;
        }
        public void ClearBuffers(){
            MemoryManager.ClearArray(vertexBuffer.AsArray(), vertexBuffer.Length);
            vertexBuffer.Length = 0;
            vertexBuffer.Capacity = MemoryManager.maxVertexCount;
            MemoryManager.ClearArray(indexBuffer.AsArray(), indexBuffer.Length);
            indexBuffer.Length = 0;
            indexBuffer.Capacity = MemoryManager.maxVertexCount;
            MemoryManager.ClearArray(grassPositions.AsArray(), grassPositions.Length);
            grassPositions.Length = 0;
            grassPositions.Capacity = MemoryManager.grassAmount;
        }
        public bool IsCreated{
            get{
                return vertexBuffer.IsCreated;
            }
        }
        public void Dispose(){
            indexBuffer.Dispose();
            vertexBuffer.Dispose();
            grassPositions.Dispose();
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
        public static int XyzToIndex(int x, int y, int z, int size)
        {
            return y * size * size + z * size + x;
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
        public static int3 IndexToXyz(int index, int size)
        {
            int3 position = new int3(
                index % size,
                index / (size * size),
                index / size % size);
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