using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WorldGeneration;
using Unity.Mathematics;
using Unity.Collections;
using WorldGeneration.DataStructures;
using Unity.Jobs;
using UnityEngine.Rendering;
using System;
#if ODIN_INSPECTOR    
using Sirenix.OdinInspector;
using ReadOnly = Sirenix.OdinInspector.ReadOnlyAttribute;
#endif
using System.Linq;
using UnityEditor;
using TMPro;
using Unity.Profiling;
public class ChunkManager
{
    GameObject simpleChunkPrefab;
    GameObject chunkPrefab;
    public bool shouldUpdateTree = false;
    public const int chunkResolution = 32;
    public const int lodLevels = 5;
    Transform poolParent;
    Transform activeParent;
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    List<ChunkData> chunkDatas;
    Queue<ChunkData> disposeQueue;
    PriorityQueue<ChunkData> meshQueue;
    Queue<ChunkData> chunkPool;
    Queue<GameObject> objectPool;
    Queue<SimpleMeshData> simpleChunkPool;
    public ChunkData chunkTree {get; private set;}
    int currentMeshQueueIndex = -1;
    public List<ChunkData> GetDebugArray(){
        return chunkDatas;
    }
    public void Init(Transform poolParent, Transform activeParent, GameObject simpleChunkPrefab, GameObject chunkPrefab){
        this.simpleChunkPrefab = simpleChunkPrefab;
        this.chunkPrefab = chunkPrefab;
        meshQueue = new PriorityQueue<ChunkData>(lodLevels);
        chunkPool = new Queue<ChunkData>();
        disposeQueue = new Queue<ChunkData>();
        objectPool = new Queue<GameObject>();
        simpleChunkPool = new Queue<SimpleMeshData>();
        this.poolParent = poolParent;
        this.activeParent = activeParent;
        var meshBuffers = MemoryManager.Get2DMeshData(simpleChunkPrefab.GetComponent<MeshFilter>().sharedMesh);
        VertexAttributeDescriptor[] layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
                };
        while(meshBuffers.Count > 0){
            var meshData = meshBuffers.Dequeue();
            meshData.worldObject = UnityEngine.Object.Instantiate(simpleChunkPrefab);
            meshData.worldObject.GetComponent<MeshFilter>().sharedMesh = new Mesh();
            meshData.worldObject.SetActive(false);
            var mesh = meshData.worldObject.GetComponent<MeshFilter>().sharedMesh;
            mesh.SetVertexBufferParams(1089, layout);
            mesh.SetIndexBufferParams(6144, IndexFormat.UInt16);
            meshData.worldObject.transform.SetParent(poolParent);
            simpleChunkPool.Enqueue(meshData);
        }

        chunkDatas = new List<ChunkData>();
        chunkTree = new ChunkData();
        chunkTree.chunkState = ChunkData.ChunkState.ROOT;
    }
    public bool IsReady{
        get{
            return meshQueue.Count == 0 && MemoryManager.GetFreeVertexIndexBufferCount() == MemoryManager.maxConcurrentOperations;
        }
    }
    public bool Update(){
        var disposeCount = disposeQueue.Count;
        for(int i = 0; i < disposeCount; i++){
            var chunk = disposeQueue.Dequeue();
            DisposeChunk(chunk);
        }
        for(int i = 0; i < lodLevels; i++){
            meshQueue[i] = new Queue<ChunkData>(meshQueue[i].Where(x => x.disposeStatus == ChunkData.DisposeState.NOTHING));
        }
        if(meshQueue.Count > 0){
            while(meshQueue.Count > 0 && MemoryManager.GetFreeVertexIndexBufferCount() > 0){
                var nextMeshQueueIndex = meshQueue.PeekQueue();
                if(currentMeshQueueIndex != nextMeshQueueIndex){
                    if(TerraxelWorld.worldUpdatePending){
                        while(meshQueue.Count > 0){
                            DisposeChunk(meshQueue.Dequeue());
                            return false;
                        }
                    }
                    currentMeshQueueIndex = nextMeshQueueIndex;
                }
                if(meshQueue.TryDequeue(currentMeshQueueIndex, out var toBeProcessed)){
                    if(!toBeProcessed.meshData.IsCreated && MemoryManager.GetFreeMeshDataCount() == 0) {
                        meshQueue.Enqueue(toBeProcessed, toBeProcessed.depth);
                        break;
                    }
                    UpdateChunk(toBeProcessed);
                }
            }
        }else if(MemoryManager.GetFreeVertexIndexBufferCount() == MemoryManager.maxConcurrentOperations){
            return true;
        }
        return false;
    }
    public void DisposeChunk(ChunkData chunk){
        if(chunk.chunkState == ChunkData.ChunkState.DIRTY){
            disposeQueue.Enqueue(chunk);
            return;
        }
        if(chunk.disposeStatus != ChunkData.DisposeState.NOTHING){
            if(chunk.disposeStatus == ChunkData.DisposeState.POOL)
                PoolChunk(chunk);
            else if(chunk.disposeStatus == ChunkData.DisposeState.FREE_MESH)
                FreeChunkBuffers(chunk);
        }
    }
    public void PoolChunk(ChunkData chunk){
        FreeChunkBuffers(chunk);
        chunkPool.Enqueue(chunk);
    }
    public void FreeChunkBuffers(ChunkData chunk){
        chunkDatas.Remove(chunk);
        if(chunk.simpleMesh != null){
            chunk.simpleMesh.worldObject.SetActive(false);
            chunk.simpleMesh.worldObject.transform.SetParent(poolParent);
            simpleChunkPool.Enqueue(chunk.simpleMesh);
            chunk.simpleMesh = null;
        }
        else if(chunk.worldObject != null){
            chunk.worldObject.name = "Pooled chunk";
            chunk.worldObject.transform.SetParent(poolParent);
            chunk.worldObject.SetActive(true);
            chunk.worldObject.GetComponent<MeshFilter>().sharedMesh.Clear();
            chunk.worldObject.GetComponent<MeshCollider>().sharedMesh = null;
            for(int i = 0; i < 6; i++){
                chunk.worldObject.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh.Clear();
            }
            objectPool.Enqueue(chunk.worldObject);
        }
        chunk.hasMesh = false;
        chunk.worldObject = null;
        if(chunk.meshData.IsCreated){
            MemoryManager.ReturnMeshData(chunk.meshData);
            chunk.meshData = default;
        }
    }
    public GameObject GetChunkObject(){
        if(objectPool.Count > 0){
            var chunk = objectPool.Dequeue();
            chunk.transform.SetParent(activeParent);
            return chunk;
        }
        else{
            var chunk = UnityEngine.Object.Instantiate(chunkPrefab);
            chunk.GetComponent<MeshFilter>().sharedMesh = new Mesh();
            for(int i = 0; i < 6; i++){
                chunk.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh = new Mesh();
            }
            chunk.transform.SetParent(activeParent);
            return chunk;
        }
    }
    public void RegenerateChunkMesh(int3 worldPos, float radius){
        radius *= 2;
        var chunk = chunkTree.Query(new BoundingBox((float3)worldPos, new float3(radius)));
        List<Octree> toUpdate = new List<Octree>();
        if(chunk != null) toUpdate.Add(chunk);
        chunkTree.QueryColliding(new BoundingBox((float3)worldPos, new float3(radius)), toUpdate);
        for(int i = 0; i < toUpdate.Count; i++){
            if(!toUpdate[i].IsReady || meshQueue.Contains(toUpdate[i] as ChunkData)) continue;
            UpdateChunk(toUpdate[i] as ChunkData);
            shouldUpdateTree = true;
        }
        if(chunk == null) chunk = chunkTree;
    }
    public void RegenerateChunk(ChunkData chunk){
        if(chunkDatas.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return;
        }
        chunk.chunkState = ChunkData.ChunkState.INVALID;
        chunk.disposeStatus = ChunkData.DisposeState.NOTHING;
        chunk.hasMesh = true;
        //chunk.worldObject = newChunk;
        chunkDatas.Add(chunk);
        UpdateChunk(chunk);
    }
    public ChunkData GenerateChunk(float3 pos, int depth, BoundingBox bounds){
        if(chunkDatas.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return null;
        }
        ChunkData newChunkData = null;
        if(chunkPool.Count > 0){
            newChunkData = chunkPool.Dequeue();
            //newChunkData.worldObject = newChunk;
            newChunkData.region = bounds;
            newChunkData.depth = depth;
            newChunkData.chunkState = ChunkData.ChunkState.INVALID;
            newChunkData.disposeStatus = ChunkData.DisposeState.NOTHING;
        }else{
         newChunkData = new ChunkData(null, bounds, depth);
        }
        newChunkData.hasMesh = true;
        chunkDatas.Add(newChunkData);
        UpdateChunk(newChunkData);
        return newChunkData;
    }
    void UpdateChunk(ChunkData chunkData){
        if(TerraxelWorld.worldState != TerraxelWorld.WorldState.MESH_UPDATE){
            chunkData.chunkState = ChunkData.ChunkState.QUEUED;
            meshQueue.Enqueue(chunkData, chunkData.depth);
            return;
        }
        /*if(chunkData.depth == 0){
            if(TerraxelWorld.DensityManager.ChunkIsFullOrEmpty((int3)chunkData.WorldPosition)){
                chunkData.OnMeshReady();
                chunkData.FreeChunkMesh();
                return;
            }
        }*/
        /*if(chunkData.depth > 1){
            if(chunkData.WorldPosition.y != 0){
                float dst = math.distance(new float2(TerraxelWorld.playerBounds.center.x, TerraxelWorld.playerBounds.center.z), new float2(chunkData.region.center.x, chunkData.region.center.z));
                if(dst < 150){
                    
                    if(MemoryManager.GetFreeVertexIndexBufferCount() == 0){
                        chunkData.chunkState = ChunkData.ChunkState.QUEUED;
                        meshQueue.Enqueue(chunkData, chunkData.depth);
                        return;
                    }
                    chunkData.vertexIndexBuffer = MemoryManager.GetVertexIndexBuffer();
                    chunkData.meshData = MemoryManager.GetMeshData();
                    chunkData.vertCount = 0;
                    chunkData.idxCount = 0;
                    chunkData.chunkState = ChunkData.ChunkState.DIRTY;
                    chunkData.UpdateMesh();
                    return;
                }
                chunkData.OnMeshReady();
                chunkData.FreeChunkMesh();
                return;
            }
            
            chunkData.simpleMesh = simpleChunkPool.Dequeue();
            chunkData.simpleMesh.worldObject.transform.SetParent(activeParent);
            chunkData.vertCount = 1089;
            chunkData.idxCount = 6144;
            chunkData.chunkState = ChunkData.ChunkState.DIRTY;
            chunkData.UpdateMesh();
            return;
        }*/
        /*float dst = math.distance(new float2(TerraxelWorld.playerBounds.center.x, TerraxelWorld.playerBounds.center.z), new float2(chunkData.region.center.x, chunkData.region.center.z));
        if(dst > 32f * Octree.depthMultipliers[2] && chunkData.depth > 1){
            if(chunkData.WorldPosition.y != 0){
                chunkData.OnMeshReady();
                chunkData.FreeChunkMesh();
                return;
            }
            
            chunkData.simpleMesh = simpleChunkPool.Dequeue();
            chunkData.simpleMesh.worldObject.transform.SetParent(activeParent);
            chunkData.vertCount = 1089;
            chunkData.indexCount = 6144;
            chunkData.chunkState = ChunkData.ChunkState.DIRTY;
            chunkData.UpdateMesh();
            return;
        }*/
        if(MemoryManager.GetFreeVertexIndexBufferCount() == 0){
            chunkData.chunkState = ChunkData.ChunkState.QUEUED;
            meshQueue.Enqueue(chunkData, chunkData.depth);
            return;
        }
        if(chunkData.vertexIndexBuffer.vertexIndices == default)
            chunkData.vertexIndexBuffer = MemoryManager.GetVertexIndexBuffer();
        else
            chunkData.vertexIndexBuffer.ClearBuffers();
        if(!chunkData.meshData.IsCreated)
            chunkData.meshData = MemoryManager.GetMeshData();
        else
            chunkData.meshData.ClearBuffers();
        chunkData.vertCount = 0;
        chunkData.idxCount = 0;
        chunkData.chunkState = ChunkData.ChunkState.DIRTY;
        chunkData.UpdateMesh();
    }
}
