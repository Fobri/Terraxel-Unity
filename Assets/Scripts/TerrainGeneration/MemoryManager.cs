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
    
    public const int maxBufferCount = 128*4;
    public const int simpleMeshAmount = 64;
    public const int densityCount = 128*6;
    public const int maxConcurrentOperations = 4;
    public const int maxVertexCount = 10000;
    public const int densityMapLength = (ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*(ChunkManager.chunkResolution);
    public const int grassAmount = 10000;
    static Queue<MeshData> freeMeshDatas;
    static Queue<SimpleMeshData> freeSimpleMeshDatas;
    static MeshData[] meshDatas;
    static Queue<TempBuffer> freeVertexIndexBuffers;
    static Queue<NativeArray<sbyte>> freeDensityMaps;
    static NativeArray<sbyte> densityMap;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    static AtomicSafetyHandle densitySafetyHandle;
#endif
    static TempBuffer[] vertexIndexBuffers;
    static SimpleMeshData[] simpleMeshes;
    static List<NativeArray<int2>> meshStarts = new List<NativeArray<int2>>();

    public static void Init(){
        AllocateMeshData();
        AllocateTempBuffers();
    }
    public static void AllocateSimpleMeshData(Mesh source){
        var verts = source.vertices;
        NativeArray<VertexData> vertices = new NativeArray<VertexData>(verts.Length, Allocator.Temp);
        var tris = source.triangles;
        NativeArray<ushort> indices = new NativeArray<ushort>(tris.Length, Allocator.Persistent);
        for(int i = 0; i < tris.Length; i++){
            indices[i] = (ushort)tris[i];
        }
        SimpleMeshData.indices = indices;
        for(int i = 0; i < verts.Length; i++){
            vertices[i] = new VertexData(math.round((float3)(verts[i] * 100)), 0f);
        }
        freeSimpleMeshDatas = new Queue<SimpleMeshData>();
        for(int i = 0; i < simpleMeshAmount; i++){
            var buffer = new NativeArray<VertexData>(verts.Length, Allocator.Persistent);
            var heightMap = new NativeArray<float>((ChunkManager.chunkResolution + 3) * (ChunkManager.chunkResolution + 3), Allocator.Persistent);
            buffer.CopyFrom(vertices);
            SimpleMeshData data = new SimpleMeshData();
            data.buffer = buffer;
            data.heightMap = heightMap;
            freeSimpleMeshDatas.Enqueue(data);
        }
        simpleMeshes = freeSimpleMeshDatas.ToArray();
        vertices.Dispose();
    }
    static void AllocateTempBuffers(){

        freeVertexIndexBuffers = new Queue<TempBuffer>();
        for(int i = 0; i < maxConcurrentOperations; i++){
            var buf1 = new NativeArray<ReuseCell>(densityMapLength, Allocator.Persistent);
            var buf2 = new NativeArray<CellIndices>((ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*6, Allocator.Persistent);
            freeVertexIndexBuffers.Enqueue(new TempBuffer(buf1, buf2));
        }
        vertexIndexBuffers = freeVertexIndexBuffers.ToArray();
    }
    static void AllocateMeshData(){
        freeMeshDatas = new Queue<MeshData>();
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
            freeMeshDatas.Enqueue(new MeshData(verts, indices));
        }
        for(int i = 0; i < densityCount; i++){
            var densities = densityMap.GetSubArray(i*densityMapLength, densityMapLength);
            freeDensityMaps.Enqueue(densities);
        }
        meshDatas = freeMeshDatas.ToArray();
    }
    public static MeshData GetMeshData(){
        if(freeMeshDatas.Count == 0) throw new Exception("No free mesh data available", new InvalidOperationException());
        return freeMeshDatas.Dequeue();
    }
    public static SimpleMeshData GetSimpleMeshData(){
        if(freeSimpleMeshDatas.Count == 0) throw new Exception("No free mesh data available", new InvalidOperationException());
        return freeSimpleMeshDatas.Dequeue();
    }
    public static TempBuffer GetVertexIndexBuffer(){
        if(freeVertexIndexBuffers.Count == 0) throw new Exception("No free vertex index buffer available", new InvalidOperationException());
        var thing = freeVertexIndexBuffers.Dequeue();
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
        freeMeshDatas.Enqueue(data);
    }
    public static void ReturnSimpleMeshData(SimpleMeshData data){
        freeSimpleMeshDatas.Enqueue(data);
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
        freeVertexIndexBuffers.Enqueue(buffer);
    }

    public static int GetFreeVertexIndexBufferCount(){
        return freeVertexIndexBuffers.Count;
    }
    public static int GetFreeDensityMapCount(){
        return freeDensityMaps.Count;
    }
    public static int GetFreeMeshDataCount(){
        return freeMeshDatas.Count;
    }

    public static void Dispose(){
        foreach(var data in meshDatas){
            data.Dispose();
        }
        foreach(var buffer in vertexIndexBuffers){
            buffer.Dispose();
        }
        foreach(var buffer in meshStarts){
            buffer.Dispose();
        }
        foreach(var buffer in simpleMeshes){
            buffer.buffer.Dispose();
            buffer.heightMap.Dispose();
        }
        SimpleMeshData.indices.Dispose();
        densityMap.Dispose();
    }
    public static unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}