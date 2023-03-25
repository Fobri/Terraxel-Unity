using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Terraxel;
using Unity.Collections.LowLevel.Unsafe;
using Terraxel.DataStructures;
using Terraxel.WorldGeneration;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine.Rendering;

public class DensityManager : IDisposable {
    
    DensityData densityData;
    //Queue<NoiseJob> operationQueue = new Queue<NoiseJob>();
    //Queue<KeyValuePair<int3, IntPtr>> operationQueue = new Queue<KeyValuePair<int3, IntPtr>>();
    Queue<int3> operationQueue = new Queue<int3>();
    ComputeShader noiseCompute;
    int kernel;
    const int maxOperations = 4;
    Queue<KeyValuePair<int3, sbyte>> modifications = new Queue<KeyValuePair<int3, sbyte>>();
    DensityResultData currentlyProcessedPosition;
    ComputeBuffer gpuBuffer;
    bool gpuProgramRunning;

    public void Init(ComputeShader noiseShader){
        densityData = new DensityData();
        densityData.densities = new NativeHashMap<int3, IntPtr>(50, Allocator.Persistent);
        densityData.emptyChunks = new NativeHashSet<int3>(100, Allocator.Persistent);
        densityData.fullChunks = new NativeHashSet<int3>(100, Allocator.Persistent);
        noiseCompute = noiseShader;
        gpuBuffer = new ComputeBuffer(8192, 4, ComputeBufferType.Structured);
        kernel = noiseCompute.FindKernel("CSMain");
        noiseCompute.SetBuffer(kernel, "Result", gpuBuffer);
    }

    public BoundingBox[] GetDebugArray(){
        List<BoundingBox> value = new List<BoundingBox>();
        foreach(var key in densityData.densities){
            var bound = new BoundingBox((float3)key.Key + ChunkManager.chunkResolution / 2, new float3(ChunkManager.chunkResolution));
            value.Add(bound);
        }
        /*foreach(var key in densityData.fullChunks){
            var bound = new BoundingBox((float3)key + ChunkManager.chunkResolution / 2, new float3(ChunkManager.chunkResolution));
            value.Add(bound);
        }
        foreach(var key in densityData.emptyChunks){
            var bound = new BoundingBox((float3)key + ChunkManager.chunkResolution / 2, new float3(ChunkManager.chunkResolution));
            value.Add(bound);
        }*/
        return value.ToArray();
    }
    public void QueueModification(int3 pos, sbyte value){
        if(TerraxelWorld.worldState == TerraxelWorld.WorldState.IDLE)
            DoModification(pos, value);
        else
            modifications.Enqueue(new KeyValuePair<int3, sbyte>(pos, value));
    }
    void DoModification(int3 pos, sbyte value){
        if(value == 0) return;
        var chunkPos = Utils.WorldPosToChunkPos(pos);
        var localPosInChunk = math.abs(pos - chunkPos);

        if(densityData.fullChunks.Contains(chunkPos) || densityData.emptyChunks.Contains(chunkPos)){
            LoadDensityAtPosition(chunkPos, false);
            modifications.Enqueue(new KeyValuePair<int3, sbyte>(pos, value));
            densityData.fullChunks.Remove(chunkPos);
            densityData.emptyChunks.Remove(chunkPos);
        }
        else if(densityData.densities.ContainsKey(chunkPos)){
            var data = densityData.densities[chunkPos];
            unsafe{
            sbyte origValue = UnsafeUtility.ReadArrayElement<sbyte>((void*)data, Utils.XyzToIndex(localPosInChunk, ChunkManager.chunkResolution));
            if(value > 0 && (int)origValue + (int)value > 127) value = 127;
            else if(value < 0 && (int)origValue + (int)value < -127) value = -127;
            else value += origValue;
            UnsafeUtility.WriteArrayElement<sbyte>((void*)data, Utils.XyzToIndex(localPosInChunk, ChunkManager.chunkResolution), value);
            }
        }
    }
    void DataReceived(AsyncGPUReadbackRequest request){
        if(request.hasError) throw new Exception("Error in AsyncGPUReadbackRequest");
            /*if(instance.isEmpty.Value || instance.isFull.Value){
                MemoryManager.ReturnDensityMap(instance.densityMap);
                if(instance.isEmpty.Value) densityData.emptyChunks.Add(key);
                if(instance.isFull.Value) densityData.fullChunks.Add(key);
            }else{*/
            /*for(int i = 0; i < currentlyProcessedPosition.densityMap.Length; i++){
                if(currentlyProcessedPosition.densityMap[i] != 0)
                    Debug.Log(currentlyProcessedPosition.densityMap[i]);
            }*/
            unsafe{
            densityData.densities.Add(currentlyProcessedPosition.pos, (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(currentlyProcessedPosition.densityMap));
            }
            //currentlyProcessedPositions.Remove(key);
            //}
            //instance.isEmpty.Dispose();
            //instance.isFull.Dispose();
        //currentlyProcessedPositions.Clear();
        gpuProgramRunning = false;
        ScheduleBatch();
    }
    void ScheduleBatch(){
        if(operationQueue.Count == 0){
            currentlyProcessedPosition = default;
            return;
        }
        //while(operationQueue.Count != 0){
            //if(operationQueue.Count == 0) return;
            var job = operationQueue.Dequeue();
            if(densityData.densities.ContainsKey(job)){
                ScheduleBatch();
                return;
            }
            var data = new DensityResultData();
            data.densityMap = MemoryManager.GetDensityMap();
            noiseCompute.SetVector("offset", new float4(job, 0));
            noiseCompute.Dispatch(kernel, 16, 1, 1);
            data.request = AsyncGPUReadback.RequestIntoNativeArray(ref data.densityMap, gpuBuffer, new Action<AsyncGPUReadbackRequest>(DataReceived));
            data.pos = job;
            currentlyProcessedPosition = data;
            gpuProgramRunning = true;
            /*data.isEmpty = new NativeReference<bool>(job.allowEmptyOrFull ? true : false,Allocator.TempJob);
            data.isFull = new NativeReference<bool>(job.allowEmptyOrFull ? true : false,Allocator.TempJob);
            job.data = data;
            currentlyProcessedPositions.Add((int3)job.offset, data);
            ScheduleParallelForJob(job, MemoryManager.densityMapLength);*/
        //}
    }

    public DensityData GetJobDensityData(){
        return densityData;
    }
    public bool Update(){
        if(IsReady && operationQueue.Count == 0){
            while(modifications.Count > 0){
                var modification = modifications.Dequeue();
                DoModification(modification.Key, modification.Value);
            }
            return true;
        }else if(operationQueue.Count > 0){
            //ScheduleBatch();
        }
        return false;
    }
    public bool IsReady{
        get{
            return !gpuProgramRunning && operationQueue.Count == 0;
        }
    }
    public bool ChunkIsFullOrEmpty(int3 pos){
        return densityData.fullChunks.Contains(pos) || densityData.emptyChunks.Contains(pos);
    }
    void LoadDensityAtPosition(int3 pos, bool allowEmptyOrFull = true){
        /*var noiseJob = new NoiseJob()
        {
            offset = pos,
            size = ChunkManager.chunkResolution,
            depthMultiplier = Octree.depthMultipliers[0],
            noiseProperties = densityData.noiseProperties,
            allowEmptyOrFull = allowEmptyOrFull
        };*/
        operationQueue.Enqueue(pos);
    }
    public void LoadDensityData(float3 center, int radius){
        HashSet<int3> alreadyDataExists = new HashSet<int3>();
        int3 startPos = (int3)center - ChunkManager.chunkResolution * Octree.depthMultipliers[radius];
        for(int x = 0; x < Octree.depthMultipliers[radius] * 2; x++){
            for(int y = 0; y < Octree.depthMultipliers[radius] * 2; y++){
                for(int z = 0; z < Octree.depthMultipliers[radius] * 2; z++){
                    int3 pos = new int3(x,y,z) * ChunkManager.chunkResolution + startPos;
                    if(densityData.ContainsPos(pos) ||densityData.emptyChunks.Contains(pos) ||densityData.fullChunks.Contains(pos)){
                        alreadyDataExists.Add(pos);
                        continue;
                    }
                    LoadDensityAtPosition(pos);
                }
            }
        }
        using (var keys = densityData.densities.GetKeyArray(Allocator.Temp)){
            for(int i = 0; i < keys.Length; i++){
                if(!alreadyDataExists.Contains(keys[i])){
                    UnloadDensityData(keys[i]);
                }
            }
        }
        using(var keys = densityData.fullChunks.ToNativeArray(Allocator.Temp)){
            for(int i = 0; i < keys.Length; i++){
                if(!alreadyDataExists.Contains(keys[i])){
                    densityData.fullChunks.Remove(keys[i]);
                }
            }
        }
        using(var keys = densityData.emptyChunks.ToNativeArray(Allocator.Temp)){
            for(int i = 0; i < keys.Length; i++){
                if(!alreadyDataExists.Contains(keys[i])){
                    densityData.emptyChunks.Remove(keys[i]);
                }
            }
        }
        ScheduleBatch();
    }
    private void UnloadDensityData(int3 pos){
        unsafe{
            var densityMap = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<sbyte>((void*)densityData.densities[pos], MemoryManager.densityMapLength, Allocator.None);
            //NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref densityMap, new AtomicSafetyHandle());
            MemoryManager.ReturnDensityMap(densityMap, true);
        }
        densityData.densities.Remove(pos);
    }

    public void Dispose(){
        densityData.Dispose();
        AsyncGPUReadback.WaitAllRequests();
    }
}
public struct DensityData : IDisposable{
    public NativeHashMap<int3, IntPtr> densities;
    public NativeHashSet<int3> fullChunks;
    public NativeHashSet<int3> emptyChunks;

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte GetDensity(int3 worldPos){
        var chunkPos = Utils.WorldPosToChunkPos(worldPos);

        var localPosInChunk = math.abs(worldPos - chunkPos);

        if(fullChunks.Contains(chunkPos)) return -127;
        if(emptyChunks.Contains(chunkPos)) return 127;
        if(densities.ContainsKey(chunkPos)){
            var data = densities[chunkPos];
            unsafe{
            return UnsafeUtility.ReadArrayElement<sbyte>((void*)data, Utils.XyzToIndex(localPosInChunk, ChunkManager.chunkResolution));
            }
        }
        return GenerateDensity(worldPos);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte GenerateDensity(int3 worldPos){
        return TerraxelGenerated.GenerateDensity(worldPos);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr GetDensityMap(int3 chunkPos){
        unsafe{
        return densities[chunkPos];
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsPos(int3 chunkPos){
        return densities.ContainsKey(chunkPos);
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty(int3 chunkPos){
        return emptyChunks.Contains(chunkPos);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFull(int3 chunkPos){
        return fullChunks.Contains(chunkPos);
    }

    public void Dispose(){
        densities.Dispose();
        fullChunks.Dispose();
        emptyChunks.Dispose();
    }
}