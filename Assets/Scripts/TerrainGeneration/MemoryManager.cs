using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using Unity.Collections.LowLevel.Unsafe;
using WorldGeneration;

public class MemoryManager{
    class MemoryAllocation<T> : IDisposable where T : IDisposable{
        public MemoryAllocation(){
            freeInstances = new Queue<T>();
        }
        public void InitArray(){
            allInstances = freeInstances.ToArray();
        }
        public int Count{
            get{
                return freeInstances.Count;
            }
        }
        public void Enqueue(T t){
            freeInstances.Enqueue(t);
        }
        public T Dequeue(){
            return freeInstances.Dequeue();
        }
        public void Dispose(){
            foreach(var buffer in allInstances){
                buffer.Dispose();
            }
        }
        public Queue<T> freeInstances;
        public T[] allInstances;
    }
    public const int maxBufferCount = 128*4;
    public const int simpleMeshAmount = 128*4;
    public const int densityCount = 128*6;
    public const int maxConcurrentOperations = 4;
    public const int maxVertexCount = 10000;
    public const int densityMapLength = (ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*(ChunkManager.chunkResolution);
    public const int grassAmount = 10000;
    static MemoryAllocation<MeshData> meshDatas;
    static MemoryAllocation<SimpleMeshData> simpleMeshDatas;
    static MemoryAllocation<TempBuffer> vertexIndexBuffers;
    static Queue<NativeArray<sbyte>> freeDensityMaps;
    static NativeArray<sbyte> densityMap;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    static AtomicSafetyHandle densitySafetyHandle;
#endif
    static List<NativeArray<int2>> meshStarts = new List<NativeArray<int2>>();

    public static void Init(){
        AllocateMeshData();
        AllocateTempBuffers();
    }
    public static void AllocateSimpleMeshData(Mesh source){
        /*var verts = source.vertices;
        NativeArray<VertexData> vertices = new NativeArray<VertexData>(verts.Length, Allocator.Temp);
        var tris = source.triangles;
        NativeArray<ushort> indices = new NativeArray<ushort>(tris.Length, Allocator.Persistent);
        for(int i = 0; i < tris.Length; i++){
            indices[i] = (ushort)tris[i];
        }
        SimpleMeshData.indices = indices;
        for(int i = 0; i < verts.Length; i++){
            vertices[i] = new VertexData(math.round((float3)(verts[i] * 100)), 0f);
        }*/
        simpleMeshDatas = new MemoryAllocation<SimpleMeshData>();
        for(int i = 0; i < simpleMeshAmount; i++){
            var vertBuffer = new NativeArray<VertexData>(Chunk2D.vertexCount, Allocator.Persistent);
            var indexBuffer = new NativeArray<ushort>(Chunk2D.indexCount, Allocator.Persistent);
            var heightMap = new NativeArray<float>(4489, Allocator.Persistent);
            //buffer.CopyFrom(vertices);
            SimpleMeshData data = new SimpleMeshData();
            data.vertexBuffer = vertBuffer;
            data.indexBuffer = indexBuffer;
            data.heightMap = heightMap;
            simpleMeshDatas.Enqueue(data);
        }
        simpleMeshDatas.InitArray();
        //vertices.Dispose();
    }
    static void AllocateTempBuffers(){

        vertexIndexBuffers = new MemoryAllocation<TempBuffer>();
        for(int i = 0; i < maxConcurrentOperations; i++){
            var buf1 = new NativeArray<ReuseCell>(densityMapLength, Allocator.Persistent);
            var buf2 = new NativeArray<CellIndices>((ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*6, Allocator.Persistent);
            vertexIndexBuffers.Enqueue(new TempBuffer(buf1, buf2));
        }
        vertexIndexBuffers.InitArray();
    }
    static void AllocateMeshData(){
        meshDatas = new MemoryAllocation<MeshData>();
        freeDensityMaps = new Queue<NativeArray<sbyte>>();
        densityMap = new NativeArray<sbyte>(densityMapLength * densityCount, Allocator.Persistent);
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        unsafe{
            densitySafetyHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(densityMap);
        }
        #endif
        for(int i = 0; i < maxBufferCount; i++){
            var verts = new NativeList<TransitionVertexData>(maxVertexCount, Allocator.Persistent);
            var indices = new NativeList<ushort>(maxVertexCount, Allocator.Persistent);
            //var grassPositions = new NativeList<Matrix4x4>(grassAmount, Allocator.Persistent);
            meshDatas.freeInstances.Enqueue(new MeshData(verts, indices));
        }
        for(int i = 0; i < densityCount; i++){
            var densities = densityMap.GetSubArray(i*densityMapLength, densityMapLength);
            freeDensityMaps.Enqueue(densities);
        }
        meshDatas.InitArray();
    }
    public static MeshData GetMeshData(){
        if(meshDatas.Count == 0) throw new Exception("No free mesh data available", new InvalidOperationException());
        return meshDatas.Dequeue();
    }
    public static SimpleMeshData GetSimpleMeshData(){
        if(simpleMeshDatas.Count == 0) throw new Exception("No free mesh data available", new InvalidOperationException());
        return simpleMeshDatas.Dequeue();
    }
    public static TempBuffer GetVertexIndexBuffer(){
        if(vertexIndexBuffers.Count == 0) throw new Exception("No free vertex index buffer available", new InvalidOperationException());
        var thing = vertexIndexBuffers.Dequeue();
        return thing;
    }
    public static NativeArray<sbyte> GetDensityMap(){
        if(freeDensityMaps.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        var thing = freeDensityMaps.Dequeue();
        return thing;
    }
    public static NativeArray<int2> GetMeshCounterArray(){
        var meshStart = new NativeArray<int2>(7, Allocator.Persistent);
        meshStarts.Add(meshStart);
        return meshStart;
    }
    public static void ReturnMeshData(MeshData data){
        ClearArray(data.indexBuffer.AsArray(), data.indexBuffer.Length);
        data.indexBuffer.Length = 0;
        data.indexBuffer.Capacity = maxVertexCount;
        ClearArray(data.vertexBuffer.AsArray(), data.vertexBuffer.Length);
        data.vertexBuffer.Length = 0;
        data.vertexBuffer.Capacity = maxVertexCount;
        meshDatas.Enqueue(data);
    }
    public static void ReturnSimpleMeshData(SimpleMeshData data){
        ClearArray(data.indexBuffer, data.indexBuffer.Length);
        simpleMeshDatas.Enqueue(data);
    }
    public static void ReturnDensityMap(NativeArray<sbyte> map, bool assignSafetyHandle = false){
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        if(assignSafetyHandle){
            unsafe{
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref map, densitySafetyHandle);
            }
        }
        #endif
        freeDensityMaps.Enqueue(map);
    }
    public static void ReturnVertexIndexBuffer(TempBuffer buffer){
        if(buffer.vertexIndices == default) throw new Exception("Tried to return invalid buffer", new InvalidCastException());
        ClearArray(buffer.transitionVertexIndices, buffer.transitionVertexIndices.Length);
        ClearArray(buffer.vertexIndices, buffer.vertexIndices.Length);
        vertexIndexBuffers.Enqueue(buffer);
    }

    public static int GetFreeVertexIndexBufferCount(){
        return vertexIndexBuffers.Count;
    }
    public static int GetFreeDensityMapCount(){
        return freeDensityMaps.Count;
    }
    public static int GetFreeMeshDataCount(){
        return meshDatas.Count;
    }

    public static void Dispose(){
        meshDatas.Dispose();
        vertexIndexBuffers.Dispose();
        foreach(var buffer in meshStarts){
            buffer.Dispose();
        }
        simpleMeshDatas.Dispose();
        //SimpleMeshData.indices.Dispose();
        densityMap.Dispose();
    }
    public static unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}