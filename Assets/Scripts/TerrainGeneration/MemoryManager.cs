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
    public const int maxConcurrentOperations = 4;
    public const int maxVertexCount = 30000;
    Queue<MeshData> freeMeshDatas;
    MeshData[] meshDatas;
    Queue<NativeArray<ushort4>> freeVertexIndexBuffers;
    NativeArray<ushort4>[] vertexIndexBuffers;

    public void Init(){
        AllocateMeshData();
        AllocateTempBuffers();
    }
    void AllocateTempBuffers(){

        var vertexIndexBufferLength = (ChunkManager.chunkResolution + 1)*(ChunkManager.chunkResolution + 1)*(ChunkManager.chunkResolution + 1);
        freeVertexIndexBuffers = new Queue<NativeArray<ushort4>>();
        for(int i = 0; i < maxConcurrentOperations; i++){
            freeVertexIndexBuffers.Enqueue(new NativeArray<ushort4>(vertexIndexBufferLength, Allocator.Persistent));
        }
        vertexIndexBuffers = freeVertexIndexBuffers.ToArray();
    }
    void AllocateMeshData(){
        var ns = ChunkManager.chunkResolution + 3;
        var size = ns * ns * ns;
        freeMeshDatas = new Queue<MeshData>();
        for(int i = 0; i < maxBufferCount; i++){
            var verts = new NativeArray<VertexData>(maxVertexCount, Allocator.Persistent);
            var indices = new NativeArray<ushort>(maxVertexCount, Allocator.Persistent);
            var densities = new NativeArray<sbyte>(size, Allocator.Persistent);
            freeMeshDatas.Enqueue(new MeshData(verts, indices, densities));
        }
        meshDatas = freeMeshDatas.ToArray();
    }
    public MeshData GetMeshData(){
        if(freeMeshDatas.Count == 0) throw new Exception("No free mesh data available", new InvalidOperationException());
        return freeMeshDatas.Dequeue();
    }
    public NativeArray<ushort4> GetVertexIndexBuffer(){
        if(freeVertexIndexBuffers.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        var thing = freeVertexIndexBuffers.Dequeue();
        if(thing == default) Debug.Log("wut");
        return thing;
    }
    public void ReturnMeshData(MeshData data){
        ClearArray(data.indexBuffer, data.indexBuffer.Length);
        ClearArray(data.vertexBuffer, data.vertexBuffer.Length);
        freeMeshDatas.Enqueue(data);
    }
    public void ReturnVertexIndexBuffer(NativeArray<ushort4> buffer){
        if(buffer == default) throw new Exception("Tried to return invalid buffer", new InvalidCastException());
        ClearArray(buffer, buffer.Length);
        freeVertexIndexBuffers.Enqueue(buffer);
    }

    public int GetFreeVertexIndexBufferCount(){
        return freeVertexIndexBuffers.Count;
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
    }
    unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}