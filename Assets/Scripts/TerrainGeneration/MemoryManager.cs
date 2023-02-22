using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Mathematics;
using WorldGeneration.DataStructures;
using Unity.Collections.LowLevel.Unsafe;
using WorldGeneration;

public class MemoryManager : IDisposable{
    
    public const int maxBufferCount = 128*4;
    public const int densityCount = 128*6;
    public const int maxConcurrentOperations = 4;
    public const int maxVertexCount = 10000;
    public const int densityMapLength = (ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*(ChunkManager.chunkResolution);
    Queue<MeshData> freeMeshDatas;
    MeshData[] meshDatas;
    Queue<TempBuffer> freeVertexIndexBuffers;
    Queue<NativeArray<sbyte>> freeDensityMaps;
    NativeArray<sbyte> densityMap;
    TempBuffer[] vertexIndexBuffers;
    List<NativeArray<int2>> meshStarts = new List<NativeArray<int2>>();

    public void Init(){
        AllocateMeshData();
        AllocateTempBuffers();
    }
    void AllocateTempBuffers(){

        freeVertexIndexBuffers = new Queue<TempBuffer>();
        for(int i = 0; i < maxConcurrentOperations; i++){
            var buf1 = new NativeArray<ushort3>(densityMapLength, Allocator.Persistent);
            var buf2 = new NativeArray<CellIndices>((ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*6, Allocator.Persistent);
            freeVertexIndexBuffers.Enqueue(new TempBuffer(buf1, buf2));
        }
        vertexIndexBuffers = freeVertexIndexBuffers.ToArray();
    }
    void AllocateMeshData(){
        freeMeshDatas = new Queue<MeshData>();
        freeDensityMaps = new Queue<NativeArray<sbyte>>();
        densityMap = new NativeArray<sbyte>(densityMapLength * densityCount, Allocator.Persistent);
        for(int i = 0; i < maxBufferCount; i++){
            var verts = new NativeList<TransitionVertexData>(maxVertexCount, Allocator.Persistent);
            var indices = new NativeList<ushort>(maxVertexCount, Allocator.Persistent);
            freeMeshDatas.Enqueue(new MeshData(verts, indices));
        }
        for(int i = 0; i < densityCount; i++){
            var densities = densityMap.GetSubArray(i*densityMapLength, densityMapLength);
            freeDensityMaps.Enqueue(densities);
        }
        meshDatas = freeMeshDatas.ToArray();
    }
    public MeshData GetMeshData(){
        if(freeMeshDatas.Count == 0) throw new Exception("No free mesh data available", new InvalidOperationException());
        return freeMeshDatas.Dequeue();
    }
    public TempBuffer GetVertexIndexBuffer(){
        if(freeVertexIndexBuffers.Count == 0) throw new Exception("No free vertex index buffer available", new InvalidOperationException());
        var thing = freeVertexIndexBuffers.Dequeue();
        return thing;
    }
    public NativeArray<sbyte> GetDensityMap(){
        if(freeDensityMaps.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        var thing = freeDensityMaps.Dequeue();
        if(thing == default) Debug.Log("wut");
        return thing;
    }
    public NativeArray<int2> GetMeshCounterArray(){
        var meshStart = new NativeArray<int2>(7, Allocator.Persistent);
        meshStarts.Add(meshStart);
        return meshStart;
    }
    public void ReturnMeshData(MeshData data){
        ClearArray(data.indexBuffer.AsArray(), data.indexBuffer.Length);
        data.indexBuffer.Length = 0;
        data.indexBuffer.Capacity = maxVertexCount;
        ClearArray(data.vertexBuffer.AsArray(), data.vertexBuffer.Length);
        data.vertexBuffer.Length = 0;
        data.vertexBuffer.Capacity = maxVertexCount;
        freeMeshDatas.Enqueue(data);
    }
    public void ReturnDensityMap(NativeArray<sbyte> map){
        freeDensityMaps.Enqueue(map);
    }
    public void ReturnVertexIndexBuffer(TempBuffer buffer){
        if(buffer.vertexIndices == default) throw new Exception("Tried to return invalid buffer", new InvalidCastException());
        ClearArray(buffer.transitionVertexIndices, buffer.transitionVertexIndices.Length);
        ClearArray(buffer.vertexIndices, buffer.vertexIndices.Length);
        freeVertexIndexBuffers.Enqueue(buffer);
    }

    public int GetFreeVertexIndexBufferCount(){
        return freeVertexIndexBuffers.Count;
    }
    public int GetFreeDensityMapCount(){
        return freeDensityMaps.Count;
    }
    public int GetFreeMeshDataCount(){
        return freeMeshDatas.Count;
    }

    public void Dispose(){
        foreach(var data in meshDatas){
            data.Dispose();
        }
        foreach(var buffer in vertexIndexBuffers){
            buffer.Dispose();
        }
        foreach(var buffer in meshStarts){
            buffer.Dispose();
        }
        densityMap.Dispose();
    }
    public static unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}