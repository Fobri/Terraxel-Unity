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
    ComputeShader[] computes = new ComputeShader[TerraxelConstants.maxConcurrentGPUOperations];
    ComputeShader cs;
    DensityResultData[] currentlyGenerating = new DensityResultData[TerraxelConstants.maxConcurrentGPUOperations];
    int kernel;
    CommandBuffer commandBuffer;
    Queue<KeyValuePair<int3, sbyte>> modifications = new Queue<KeyValuePair<int3, sbyte>>();
    //DensityResultData currentlyProcessedPosition;
    //Queue<DensityResultData> requestQueue = new Queue<DensityResultData>();
    //DensityResultData currentRequest;
    bool gpuProgramRunning;

    public void Init(ComputeShader noiseShader){
        densityData = new DensityData();
        densityData.densities = new NativeHashMap<int3, IntPtr>(50, Allocator.Persistent);
        densityData.emptyChunks = new NativeHashSet<int3>(100, Allocator.Persistent);
        densityData.fullChunks = new NativeHashSet<int3>(100, Allocator.Persistent);
        cs = noiseShader;
        commandBuffer = new CommandBuffer();
        commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        kernel = noiseShader.FindKernel("CSMain");
        for(int i = 0; i < TerraxelConstants.maxConcurrentGPUOperations; i++){
            computes[i] = UnityEngine.Object.Instantiate(cs);
            currentlyGenerating[i].gpuBuffer = new ComputeBuffer(8192, 4, ComputeBufferType.Structured);
            currentlyGenerating[i].gpuBuffer.name = "DensityBuffer" + i.ToString();
            currentlyGenerating[i].isFullOrEmpty = new ComputeBuffer(2, 4, ComputeBufferType.Structured);
            computes[i].SetBuffer(kernel, "Result", currentlyGenerating[i].gpuBuffer);
            computes[i].SetBuffer(kernel, "FullOrEmpty", currentlyGenerating[i].isFullOrEmpty);
            computes[i].SetInt("seed", TerraxelWorld.seed);
            commandBuffer.DispatchCompute(computes[i], kernel, 16, 1, 1);
        }
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
    /*void DataReceived(AsyncGPUReadbackRequest request){
        if(request.hasError) throw new Exception("Error in AsyncGPUReadbackRequest");
        unsafe{
        densityData.densities.Add(currentRequest.pos, (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(currentRequest.densityMap));
        }
        currentRequest.gpuBuffer.Release();
        if(requestQueue.Count > 0) {
            currentRequest = requestQueue.Dequeue();
            AsyncGPUReadback.RequestIntoNativeArray(ref currentRequest.densityMap, currentRequest.gpuBuffer, new Action<AsyncGPUReadbackRequest>(DataReceived));
        }else{
            currentRequest = default;
            gpuProgramRunning = false;
        }
    }*/
    void UpdateInternal(){
        bool allJobsReady = true;
        for(int i = 0; i < TerraxelConstants.maxConcurrentGPUOperations; i++){
            if(currentlyGenerating[i].requestState == DensityResultData.RequestState.READY) continue;
            allJobsReady = false;
            if(currentlyGenerating[i].requestState == DensityResultData.RequestState.NOREQ){
                if(!currentlyGenerating[i].isFullOrEmpty.IsValid()) throw new Exception("Buffer not valid");
                currentlyGenerating[i].readbackRequest = AsyncGPUReadback.Request(currentlyGenerating[i].isFullOrEmpty);
                currentlyGenerating[i].requestState = DensityResultData.RequestState.EMPTY;
            }

            if(currentlyGenerating[i].readbackRequest.done){
                if(currentlyGenerating[i].readbackRequest.hasError) {throw new Exception("Error in AsyncGPUReadback");}
                if(currentlyGenerating[i].requestState == DensityResultData.RequestState.EMPTY){
                    var isEmptyOrFull = currentlyGenerating[i].readbackRequest.GetData<int>();
                    if(isEmptyOrFull[0] == 0 || isEmptyOrFull[1] == 0){
                        //densityData.densities.Remove(currentlyGenerating[i].pos);
                        //MemoryManager.ReturnDensityMap(currentlyGenerating[i].densityMap);
                        if(isEmptyOrFull[0] == 0){
                            densityData.fullChunks.Add(currentlyGenerating[i].pos);
                        }
                        else if(isEmptyOrFull[1] == 0){
                            densityData.emptyChunks.Add(currentlyGenerating[i].pos);
                        }
                        currentlyGenerating[i].requestState = DensityResultData.RequestState.READY;
                    }else{
                        if(!currentlyGenerating[i].gpuBuffer.IsValid()) throw new Exception("Buffer not valid");
                        currentlyGenerating[i].readbackRequest = AsyncGPUReadback.Request(currentlyGenerating[i].gpuBuffer);
                        currentlyGenerating[i].requestState = DensityResultData.RequestState.DATA;
                    }
                }
                else if(currentlyGenerating[i].requestState == DensityResultData.RequestState.DATA){
                    if(MemoryManager.GetFreeDensityMapCount() > 0) {
                        currentlyGenerating[i].densityMap = MemoryManager.GetDensityMap();
                        currentlyGenerating[i].densityMap.CopyFrom(currentlyGenerating[i].readbackRequest.GetData<sbyte>());
                        unsafe{
                        densityData.densities.Add(currentlyGenerating[i].pos, (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(currentlyGenerating[i].densityMap));
                        }
                    }
                    currentlyGenerating[i].requestState = DensityResultData.RequestState.READY;
                }
            }
        }
        if(allJobsReady){
            ScheduleBatch();
        }
    }
    void ScheduleBatch(){
        //commandBuffer.Clear();
        //commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        for(int i = 0; i < TerraxelConstants.maxConcurrentGPUOperations; i++){
            if(operationQueue.Count == 0){
                gpuProgramRunning = false;
                return;
            }
            var job = operationQueue.Dequeue();
            if(densityData.densities.ContainsKey(job)){
                currentlyGenerating[i].requestState = DensityResultData.RequestState.READY;
                continue;
            }
            var data = currentlyGenerating[i];
            computes[i].SetVector("offset", new float4(job, 0));
            //noiseCompute.Dispatch(kernel, 16, 1, 1);
            data.pos = job;
            data.requestState = DensityResultData.RequestState.NOREQ;
            data.readbackRequest = default;
            data.isFullOrEmpty.SetData(new int[] {0,0});
            currentlyGenerating[i] = data;
            //requestQueue.Enqueue(data);
        }
        Graphics.ExecuteCommandBufferAsync(commandBuffer, ComputeQueueType.Default);
        //currentRequest = requestQueue.Dequeue();
        //AsyncGPUReadback.RequestIntoNativeArray(ref currentRequest.densityMap, currentRequest.gpuBuffer, new Action<AsyncGPUReadbackRequest>(DataReceived));
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
        }else{
            UpdateInternal();
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
        int3 startPos = (int3)center - ChunkManager.chunkResolution * radius - ChunkManager.chunkResolution;
        for(int x = 0; x < radius * 2 + 2; x++){
            for(int y = 0; y < radius * 2 + 2; y++){
                for(int z = 0; z < radius * 2 + 2; z++){
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
        gpuProgramRunning = true;
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
        foreach(var data in currentlyGenerating){
            data.gpuBuffer.Release();
            data.isFullOrEmpty.Release();
        }
        commandBuffer.Release();
    }
}
public struct DensityData : IDisposable{
    public NativeHashMap<int3, IntPtr> densities;
    public NativeHashSet<int3> fullChunks;
    public NativeHashSet<int3> emptyChunks;

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