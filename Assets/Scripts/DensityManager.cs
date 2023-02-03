using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using WorldGeneration;
using Unity.Collections.LowLevel.Unsafe;
using DataStructures;
using System.Runtime.CompilerServices;
using System.Threading;

public class DensityManager : JobRunner, IDisposable {
    
    DensityData densityData;
    //Queue<NoiseJob> operationQueue = new Queue<NoiseJob>();
    //Queue<KeyValuePair<int3, IntPtr>> operationQueue = new Queue<KeyValuePair<int3, IntPtr>>();
    Queue<NoiseJob> operationQueue = new Queue<NoiseJob>();
    Dictionary<int3, DensityResultData> currentlyProcessedPositions = new Dictionary<int3, DensityResultData>();

    public void Init(){
        densityData = new DensityData();
        densityData.densities = new NativeHashMap<int3, IntPtr>(50, Allocator.Persistent);
        densityData.emptyChunks = new NativeHashSet<int3>(100, Allocator.Persistent);
        densityData.fullChunks = new NativeHashSet<int3>(100, Allocator.Persistent);
    }

    public BoundingBox[] GetDebugArray(){
        List<BoundingBox> value = new List<BoundingBox>();
        foreach(var key in densityData.densities){
            var bound = new BoundingBox((float3)key.Key + ChunkManager.chunkResolution / 2, new float3(ChunkManager.chunkResolution));
            value.Add(bound);
        }
        return value.ToArray();
    }
    internal override void OnJobsReady()
    {
        foreach(var key in currentlyProcessedPositions.Keys){
            var instance = currentlyProcessedPositions[key];
            if(instance.isEmpty.Value || instance.isFull.Value){
                ChunkManager.memoryManager.ReturnDensityMap(instance.densityMap);
                if(instance.isEmpty.Value) densityData.emptyChunks.Add(key);
                if(instance.isFull.Value) densityData.fullChunks.Add(key);
            }else{
                unsafe{
                densityData.densities.Add(key, (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(instance.densityMap));
                }
            }
            instance.isEmpty.Dispose();
            instance.isFull.Dispose();
        }
        currentlyProcessedPositions.Clear();
        ScheduleBatch();
    }
    void ScheduleBatch(){
        if(operationQueue.Count == 0){
            return;
        }
        for(int i = 0; i < 8; i++){
            if(operationQueue.Count == 0) return;
            var job = operationQueue.Dequeue();
            var data = new DensityResultData();
            data.densityMap = ChunkManager.memoryManager.GetDensityMap();
            data.isEmpty = new NativeReference<bool>(true,Allocator.TempJob);
            data.isFull = new NativeReference<bool>(true,Allocator.TempJob);
            job.data = data;
            currentlyProcessedPositions.Add((int3)job.offset, data);
            ScheduleParallelJob(job, MemoryManager.densityMapLength);
        }
    }

    public DensityData GetJobDensityData(){
        return densityData;
    }

    public bool JobsReady{
        get{
            return IsReady;
        }
    }
    public bool HasPendingUpdates{
        get{
            return operationQueue.Count > 0;
        }
    }

    public void LoadDensityData(float3 center){
        int3 startPos = (int3)center - ChunkManager.chunkResolution * Octree.depthMultipliers[ChunkManager.lodLevels-1];
        for(int y = startPos.y; y < math.abs(startPos.y); y+=ChunkManager.chunkResolution){
            for(int z = startPos.z; z < math.abs(startPos.z); z+=ChunkManager.chunkResolution){
                for(int x = startPos.x; x < math.abs(startPos.x); x+=ChunkManager.chunkResolution){
                    float3 pos = new float3(x,y,z);
                    var noiseJob = new NoiseJob()
                    {
                        ampl = ChunkManager.staticNoiseData.ampl,
                        freq = ChunkManager.staticNoiseData.freq,
                        oct = ChunkManager.staticNoiseData.oct,
                        offset = pos,
                        seed = ChunkManager.staticNoiseData.offset,
                        surfaceLevel = ChunkManager.staticNoiseData.surfaceLevel,
                        //noiseMap = densityMap,
                        size = ChunkManager.chunkResolution,
                        depthMultiplier = Octree.depthMultipliers[0]
                        //pos = WorldSetup.positions
                    };
                    operationQueue.Enqueue(noiseJob);
                }
            }
        }
        ScheduleBatch();
    }

    /*private void Unload(int3 pos){
        
        if(densityData.densities.ContainsKey(pos)){
                unsafe{
                var densityMap = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<sbyte>((void*)densityData.densities[pos], MemoryManager.densityMapLength, Allocator.None);
                ChunkManager.memoryManager.ReturnDensityMap(densityMap);
            }
            densityData.densities.Remove(pos);
        }
    }
    public void UnloadDensityAtPosition(int3 pos){
        
        unloadQueue.Enqueue(pos);
    }*/

    public void Dispose(){
        densityData.Dispose();
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
        return 127;
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