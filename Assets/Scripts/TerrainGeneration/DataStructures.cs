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
        public ComputeBuffer gpuBuffer;
        public ComputeBuffer isFullOrEmpty;
        public int3 pos;
        public AsyncGPUReadbackRequest readbackRequest;
        public bool hasRequest;
        public bool isReady;
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
    [System.Serializable]
    public class MeshMaterialPair{
        [SerializeField]
        public Mesh mesh;
        [SerializeField]
        public Material material;

        public MeshMaterialPair(Mesh mesh, Material material){
            this.mesh = mesh;
            this.material = material;
        }
    }
    public struct NativeInstanceData{
        public float2 angleLimit;
        public float3x2 sizeVariation;
        public float density;
        public bool uniformDensity;
        public int maxLod;

        public NativeInstanceData(float2 angleLimit, float3x2 sizeVariation, float density, int maxLod, bool uniformDensity){
            this.angleLimit = angleLimit;
            this.sizeVariation = sizeVariation;
            this.density = density;
            this.uniformDensity = uniformDensity;
            this.maxLod = Octree.depthMultipliers[maxLod];
        }
    }
    public struct JobInstancingData : IDisposable{
        [NativeDisableContainerSafetyRestriction]
        public NativeList<InstanceData> d1;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<InstanceData> d2;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<InstanceData> d3;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<InstanceData> d4;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<InstanceData> d5;

        public bool IsEmpty{
            get{
                return d1.IsEmpty && d2.IsEmpty && d3.IsEmpty && d4.IsEmpty && d5.IsEmpty;
            }
        }

        public NativeList<InstanceData> this[int index]{
            get{
                switch(index){
                    case 0:
                    return d1;
                    case 1:
                    return d2;
                    case 2:
                    return d3;
                    case 3:
                    return d4;
                    case 4:
                    return d5;
                    default:
                    throw new IndexOutOfRangeException("Biome can only have 5 different types of instanced objects");
                }
            }set{
                switch(index){
                    case 0:
                    d1 = value;
                    break;
                    case 1:
                    d2 = value;
                    break;
                    case 2:
                    d3 = value;
                    break;
                    case 3:
                    d4 = value;
                    break;
                    case 4:
                    d5 = value;
                    break;
                    default:
                    throw new IndexOutOfRangeException("Biome can only have 5 different types of instanced objects");
                }
            }
        }

        public void Dispose(){
            for(int i = 0; i < 5; i++){
                if(this[i].IsCreated){
                    MemoryManager.ReturnInstanceData(this[i]);
                }
            }
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
    public struct DensityGenerator{
        
        /*public static sbyte FinalNoise(float3 worldPos, NoiseProperties noiseProperties)
        {
            //pos -= depthMultiplier;
            float value = SurfaceNoise2D(new float2(worldPos.x, worldPos.z), noiseProperties);
            float yPos = noiseProperties.surfaceLevel + worldPos.y;
            float density = (value + noiseProperties.surfaceLevel - yPos) * 0.1f;
            return Convert.ToSByte(math.clamp(-density * 127f, -127f, 127f));
        }*/
        public static float SurfaceNoise2D(float2 worldPos, NoiseProperties noiseProperties)
        {
            
            FastNoiseLite noise = new FastNoiseLite(1337);
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFrequency(noiseProperties.freq);
            noise.SetFractalOctaves(noiseProperties.oct);
            noise.SetFractalLacunarity(noiseProperties.lacunarity);
            noise.SetFractalGain(noiseProperties.gain);
            return SurfaceNoise2D(worldPos, noiseProperties.ampl, noise);
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
        public static float SurfaceNoise2D(float2 worldPos, float ampl, FastNoiseLite noise, int seed = 1337)
        {   
            noise.SetSeed(seed);
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
        public GeneratorString computeGenerator;
        public GeneratorString scriptGenerator; 
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
    public struct GeneratorString{
        public string body;
        public string properties;
        public string functions;
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
            vertexBuffer.Capacity = MemoryManager.assumedVertexCount;
            MemoryManager.ClearArray(indexBuffer.AsArray(), indexBuffer.Length);
            indexBuffer.Length = 0;
            indexBuffer.Capacity = MemoryManager.assumedVertexCount;
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
        public bool hasInstancingPosition;
        public ReuseCell(ushort value){
            x = value;
            y = value;
            z = value;
            hasInstancingPosition = false;
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
        public static float Angle(float3 a, float3 b){
            return (1/math.cos(math.dot(a,b)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion AlignWithNormal(float3 normal, float yRot){
            float4 q = 0;
            var axis = new float3(0,1,0);
            float3 a = math.cross(axis, normal);
            q.xyz = a;
            q.w = 1 + math.dot(axis, normal);
            quaternion randomRotation = quaternion.RotateY(yRot * 3.6f);
            return (quaternion)math.normalize(math.mul((quaternion)q, randomRotation));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateAreaOfTrinagle(float3 n1, float3 n2 , float3 n3)
        {
            float res  = math.pow(((n2.x * n1.y) - (n3.x * n1.y) - (n1.x * n2.y) + (n3.x * n2.y) + (n1.x * n3.y) - (n2.x * n3.y)), 2.0f);
            res += math.pow(((n2.x * n1.z) - (n3.x * n1.z) - (n1.x * n2.z) + (n3.x * n2.z) + (n1.x * n3.z) - (n2.x * n3.z)), 2.0f);
            res += math.pow(((n2.y * n1.z) - (n3.y * n1.z) - (n1.y * n2.z) + (n3.y * n2.z) + (n1.y * n3.z) - (n2.y * n3.z)), 2.0f);
            return math.sqrt(res) * 0.5f;
        }
        public static string floatToString(float value){
            return value.ToString("F4", new CultureInfo("en-US"))+"f";
        }
        public static string float2ToString(float2 value){
            return "new float2("+Utils.floatToString(value.x)+", "+Utils.floatToString(value.y)+")";
        }
        public static string float3ToString(float3 value){
            return "new float3("+Utils.floatToString(value.x)+", "+Utils.floatToString(value.y)+", "+Utils.floatToString(value.z)+")";
        }
        public static string float3x2ToString(float3x2 value){
            return "new float3x2("+float3ToString(value.c0)+", "+float3ToString(value.c1)+")";
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RandomValue(float3 pos){
            int state = (int)(pos.x * 53 * pos.y * 532 * pos.z+ 124123);
            state = (int)(state * 747796405 + 2891336453);
            int result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
            result = (result >> 22) ^ result;
            return result / 4294967295f;
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