using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Mathematics;

using Unity.Collections.LowLevel.Unsafe;

public class MemoryManager : IDisposable{
    
    public const int maxBufferCount = 128*2;
    public const int densityMapCount = 32;
    public const int maxVertexCount = 15000;
    Queue<NativeArray<float>> freeDensityMaps;
    Queue<NativeArray<Vector3>> freeVertexBuffers;
    Queue<NativeArray<uint>> freeIndexBuffers;
    Queue<NativeArray<uint4>> freeVertexIndexBuffers;
    NativeArray<Vector3>[] vertexBuffers;
    NativeArray<uint>[] indexBuffers;
    NativeArray<float>[] densityBuffers;
    NativeArray<uint4>[] vertexIndexBuffers;

    public void AllocateDensityMaps(int densityMapLength, int vertexIndexBufferLength){
        freeDensityMaps = new Queue<NativeArray<float>>();
        freeVertexIndexBuffers = new Queue<NativeArray<uint4>>();
        for(int i = 0; i < densityMapCount; i++){
            freeDensityMaps.Enqueue(new NativeArray<float>(densityMapLength, Allocator.Persistent));
            freeVertexIndexBuffers.Enqueue(new NativeArray<uint4>(vertexIndexBufferLength, Allocator.Persistent));
        }
        vertexIndexBuffers = freeVertexIndexBuffers.ToArray();
        densityBuffers = freeDensityMaps.ToArray();
    }
    public void AllocateMeshData(){
        freeVertexBuffers = new Queue<NativeArray<Vector3>>();
        freeIndexBuffers = new Queue<NativeArray<uint>>();
        for(int i = 0; i < maxBufferCount; i++){
            freeVertexBuffers.Enqueue(new NativeArray<Vector3>(maxVertexCount, Allocator.Persistent));
            freeIndexBuffers.Enqueue(new NativeArray<uint>(maxVertexCount, Allocator.Persistent));
        }
        vertexBuffers = freeVertexBuffers.ToArray();
        indexBuffers = freeIndexBuffers.ToArray();
    }
    public NativeArray<float> GetDensityMap(){
        if(freeDensityMaps.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        return freeDensityMaps.Dequeue();
    }
    public NativeArray<uint4> GetVertexIndexBuffer(){
        if(freeVertexIndexBuffers.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        return freeVertexIndexBuffers.Dequeue();
    }
    public NativeArray<Vector3> GetVertexBuffer(){
        if(freeVertexBuffers.Count == 0) throw new Exception("No free vertex buffer available", new InvalidOperationException());
        return freeVertexBuffers.Dequeue();
    }
    public NativeArray<uint> GetIndexBuffer(){
        if(freeIndexBuffers.Count == 0) throw new Exception("No free index buffer available", new InvalidOperationException());
        return freeIndexBuffers.Dequeue();
    }
    public void ReturnVertexBuffer(NativeArray<Vector3> buffer){
        ClearArray(buffer, buffer.Length);
        freeVertexBuffers.Enqueue(buffer);
    }
    public void ReturnIndexBuffer(NativeArray<uint> buffer){
        ClearArray(buffer, buffer.Length);
        freeIndexBuffers.Enqueue(buffer);
    }
    public void ReturnDensityMap(NativeArray<float> map){
        freeDensityMaps.Enqueue(map);
    }
    public void ReturnVertexIndexBuffer(NativeArray<uint4> buffer){
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