using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Mathematics;
using DataStructures;
using Unity.Collections.LowLevel.Unsafe;
using WorldGeneration;

public class MemoryManager : IDisposable{
    
    public const int maxBufferCount = 128*4;
    public const int densityCount = 128*6;
    public const int maxConcurrentOperations = 4;
    public const int maxVertexCount = 30000;
    public const int densityMapLength = (ChunkManager.chunkResolution)*(ChunkManager.chunkResolution)*(ChunkManager.chunkResolution);
    Queue<MeshData> freeMeshDatas;
    MeshData[] meshDatas;
    Queue<NativeArray<ushort4>> freeVertexIndexBuffers;
    Queue<NativeArray<sbyte>> freeDensityMaps;
    NativeArray<sbyte> densityMap;
    NativeArray<ushort4>[] vertexIndexBuffers;

    public void Init(){
        AllocateMeshData();
        AllocateTempBuffers();
    }
    void AllocateTempBuffers(){

        var vertexIndexBufferLength = densityMapLength;
        freeVertexIndexBuffers = new Queue<NativeArray<ushort4>>();
        for(int i = 0; i < maxConcurrentOperations; i++){
            freeVertexIndexBuffers.Enqueue(new NativeArray<ushort4>(vertexIndexBufferLength, Allocator.Persistent));
        }
        vertexIndexBuffers = freeVertexIndexBuffers.ToArray();
    }
    void AllocateMeshData(){
        freeMeshDatas = new Queue<MeshData>();
        freeDensityMaps = new Queue<NativeArray<sbyte>>();
        densityMap = new NativeArray<sbyte>(densityMapLength * densityCount, Allocator.Persistent);
        for(int i = 0; i < maxBufferCount; i++){
            var verts = new NativeArray<VertexData>(maxVertexCount, Allocator.Persistent);
            var indices = new NativeArray<ushort>(maxVertexCount, Allocator.Persistent);
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
    public NativeArray<ushort4> GetVertexIndexBuffer(){
        if(freeVertexIndexBuffers.Count == 0) throw new Exception("No free vertex index buffer available", new InvalidOperationException());
        var thing = freeVertexIndexBuffers.Dequeue();
        if(thing == default) Debug.Log("wut");
        return thing;
    }
    public NativeArray<sbyte> GetDensityMap(){
        if(freeDensityMaps.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        var thing = freeDensityMaps.Dequeue();
        if(thing == default) Debug.Log("wut");
        return thing;
    }
    public void ReturnMeshData(MeshData data){
        ClearArray(data.indexBuffer, data.indexBuffer.Length);
        ClearArray(data.vertexBuffer, data.vertexBuffer.Length);
        freeMeshDatas.Enqueue(data);
    }
    public void ReturnDensityMap(NativeArray<sbyte> map){
        freeDensityMaps.Enqueue(map);
    }
    public void ReturnVertexIndexBuffer(NativeArray<ushort4> buffer){
        if(buffer == default) throw new Exception("Tried to return invalid buffer", new InvalidCastException());
        ClearArray(buffer, buffer.Length);
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
        densityMap.Dispose();
    }
    unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}