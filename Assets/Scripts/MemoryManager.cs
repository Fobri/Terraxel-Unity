using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;

using Unity.Collections.LowLevel.Unsafe;

public class MemoryManager : IDisposable{
    
    public const int maxBufferCount = 128*2;
    const int densityMapCount = 32;
    Queue<NativeArray<float>> freeDensityMaps;
    Queue<NativeArray<Vector3>> freeVertexBuffers;
    Queue<NativeArray<int>> freeIndexBuffers;
    NativeArray<Vector3>[] vertexBuffers;
    NativeArray<int>[] indexBuffers;
    NativeArray<float>[] densityBuffers;

    public void AllocateDensityMaps(int length){
        freeDensityMaps = new Queue<NativeArray<float>>();
        for(int i = 0; i < densityMapCount; i++){
            freeDensityMaps.Enqueue(new NativeArray<float>(length, Allocator.Persistent));
        }
        densityBuffers = freeDensityMaps.ToArray();
    }
    public void AllocateMeshData(int maxVertexCount, int maxIndexCount){
        freeVertexBuffers = new Queue<NativeArray<Vector3>>();
        freeIndexBuffers = new Queue<NativeArray<int>>();
        for(int i = 0; i < maxBufferCount; i++){
            freeVertexBuffers.Enqueue(new NativeArray<Vector3>(maxVertexCount, Allocator.Persistent));
            freeIndexBuffers.Enqueue(new NativeArray<int>(maxIndexCount, Allocator.Persistent));
        }
        vertexBuffers = freeVertexBuffers.ToArray();
        indexBuffers = freeIndexBuffers.ToArray();
    }
    public NativeArray<float> GetDensityMap(){
        if(freeDensityMaps.Count == 0) throw new Exception("No free density map available", new InvalidOperationException());
        return freeDensityMaps.Dequeue();
    }
    public NativeArray<Vector3> GetVertexBuffer(){
        if(freeVertexBuffers.Count == 0) throw new Exception("No free vertex buffer available", new InvalidOperationException());
        return freeVertexBuffers.Dequeue();
    }
    public NativeArray<int> GetIndexBuffer(){
        if(freeIndexBuffers.Count == 0) throw new Exception("No free index buffer available", new InvalidOperationException());
        return freeIndexBuffers.Dequeue();
    }
    public void ReturnVertexBuffer(NativeArray<Vector3> buffer){
        ClearArray(buffer, buffer.Length);
        freeVertexBuffers.Enqueue(buffer);
    }
    public void ReturnIndexBuffer(NativeArray<int> buffer){
        ClearArray(buffer, buffer.Length);
        freeIndexBuffers.Enqueue(buffer);
    }
    public void ReturnDensityMap(NativeArray<float> map){
        freeDensityMaps.Enqueue(map);
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
    }
    unsafe void ClearArray<T>(NativeArray<T> to_clear, int length) where T : struct
        {
            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(to_clear),
                UnsafeUtility.SizeOf<T>() * length);
        }
}