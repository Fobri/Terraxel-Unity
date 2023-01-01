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
    public const int densityMapCount = 8;
    public const int maxVertexCount = 30000;
    Queue<NativeArray<float>> freeDensityMaps;
    Queue<NativeArray<VertexData>> freeVertexBuffers;
    Queue<NativeArray<ushort>> freeIndexBuffers;
    Queue<NativeArray<ushort4>> freeVertexIndexBuffers;
    NativeArray<VertexData>[] vertexBuffers;
    NativeArray<ushort>[] indexBuffers;
    NativeArray<float>[] densityBuffers;
    NativeArray<ushort4>[] vertexIndexBuffers;

    public void AllocateDensityMaps(int densityMapLength, int vertexIndexBufferLength){
        freeDensityMaps = new Queue<NativeArray<float>>();
        freeVertexIndexBuffers = new Queue<NativeArray<ushort4>>();
        for(int i = 0; i < densityMapCount; i++){
            freeDensityMaps.Enqueue(new NativeArray<float>(densityMapLength, Allocator.Persistent));
            freeVertexIndexBuffers.Enqueue(new NativeArray<ushort4>(vertexIndexBufferLength, Allocator.Persistent));
        }
        vertexIndexBuffers = freeVertexIndexBuffers.ToArray();
        densityBuffers = freeDensityMaps.ToArray();
    }
    public void AllocateMeshData(){
        freeVertexBuffers = new Queue<NativeArray<VertexData>>();
        freeIndexBuffers = new Queue<NativeArray<ushort>>();
        for(int i = 0; i < maxBufferCount; i++){
            freeVertexBuffers.Enqueue(new NativeArray<VertexData>(maxVertexCount, Allocator.Persistent));
            freeIndexBuffers.Enqueue(new NativeArray<ushort>(maxVertexCount, Allocator.Persistent));
        }
        vertexBuffers = freeVertexBuffers.ToArray();
        indexBuffers = freeIndexBuffers.ToArray();
    }
    public NativeArray<float> GetDensityMap(){
        if(freeDensityMaps.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        return freeDensityMaps.Dequeue();
    }
    public NativeArray<ushort4> GetVertexIndexBuffer(){
        if(freeVertexIndexBuffers.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        return freeVertexIndexBuffers.Dequeue();
    }
    public NativeArray<VertexData> GetVertexBuffer(){
        if(freeVertexBuffers.Count == 0) throw new Exception("No free vertex buffer available", new InvalidOperationException());
        return freeVertexBuffers.Dequeue();
    }
    public NativeArray<ushort> GetIndexBuffer(){
        if(freeIndexBuffers.Count == 0) throw new Exception("No free index buffer available", new InvalidOperationException());
        return freeIndexBuffers.Dequeue();
    }
    public void ReturnVertexBuffer(NativeArray<VertexData> buffer){
        ClearArray(buffer, buffer.Length);
        freeVertexBuffers.Enqueue(buffer);
    }
    public void ReturnIndexBuffer(NativeArray<ushort> buffer){
        ClearArray(buffer, buffer.Length);
        freeIndexBuffers.Enqueue(buffer);
    }
    public void ReturnDensityMap(NativeArray<float> map){
        freeDensityMaps.Enqueue(map);
    }
    public void ReturnVertexIndexBuffer(NativeArray<ushort4> buffer){
        ClearArray(buffer, buffer.Length);
        freeVertexIndexBuffers.Enqueue(buffer);
    }
    public bool VertexBufferAvailable{
        get{
            return freeVertexBuffers.Count > 0;
        }
    }
    public bool DensityMapAvailable{
        get{
            return freeDensityMaps.Count > 0;
        }
    }

    public int GetFreeBufferCount(){
        return freeVertexBuffers.Count;
    }
    public int GetFreeDensityCount(){
        return freeDensityMaps.Count;
    }

    public void Dispose(){
        foreach(var container in densityBuffers){
            container.Dispose();
        }
        foreach(var container in vertexBuffers){
            container.Dispose();
        }
        foreach(var container in indexBuffers){
            container.Dispose();
        }
        foreach(var container in vertexIndexBuffers){
            container.Dispose();
        }
    }
    unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}