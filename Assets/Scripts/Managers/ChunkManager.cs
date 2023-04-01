using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terraxel;
using Unity.Mathematics;
using Unity.Collections;
using Terraxel.DataStructures;
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
    GameObject chunkPrefab;
    public bool shouldUpdateTree = false;
    public const int chunkResolution = 32;
    public const int lodLevels = 5;
    public const int simpleChunkTreshold = 1;
    Transform poolParent;
    Transform activeParent;
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    List<BaseChunk> activeChunks;
    Queue<BaseChunk> disposeQueue;
    PriorityQueue<BaseChunk> meshQueue;
    Queue<BaseChunk> chunkPool3D;
    Queue<BaseChunk> chunkPool2D;
    Queue<GameObject> objectPool;
    public BaseChunk chunkTree {get; private set;}
    int currentMeshQueueIndex = -1;
    public List<BaseChunk> GetDebugArray(){
        return activeChunks;
    }
    public void Init(Transform poolParent, Transform activeParent, GameObject chunkPrefab){
        this.chunkPrefab = chunkPrefab;
        meshQueue = new PriorityQueue<BaseChunk>(lodLevels);
        chunkPool3D = new Queue<BaseChunk>();
        chunkPool2D = new Queue<BaseChunk>();
        disposeQueue = new Queue<BaseChunk>();
        objectPool = new Queue<GameObject>();
        this.poolParent = poolParent;
        this.activeParent = activeParent;
        MemoryManager.AllocateSimpleMeshData();

        activeChunks = new List<BaseChunk>();
        chunkTree = new Chunk3D();
        chunkTree.chunkState = ChunkState.ROOT;
    }
    void AddToMeshQueue(BaseChunk chunk){
        int depth = chunk.depth;
        meshQueue.Enqueue(chunk, depth);
    }
    public bool IsReady{
        get{
            return meshQueue.Count == 0 && MemoryManager.GetFreeVertexIndexBufferCount() == MemoryManager.maxConcurrentOperations;
        }
    }
    public void RenderChunks(Plane[] frustumPlanes){
        chunkTree.RenderChunksRecursive(frustumPlanes);
    }
    public bool Update(){
        var disposeCount = disposeQueue.Count;
        for(int i = 0; i < disposeCount; i++){
            var chunk = disposeQueue.Dequeue();
            DisposeChunk(chunk);
        }
        for(int i = 0; i < lodLevels; i++){
            meshQueue[i] = new Queue<BaseChunk>(meshQueue[i].Where(x => x.disposeStatus == DisposeState.NOTHING));
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
                    if(!toBeProcessed.CanBeCreated) {
                        AddToMeshQueue(toBeProcessed);
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
    public void DisposeChunk(BaseChunk chunk){
        if(chunk.chunkState == ChunkState.DIRTY){
            disposeQueue.Enqueue(chunk);
            return;
        }
        if(chunk.disposeStatus != DisposeState.NOTHING){
            if(chunk.disposeStatus == DisposeState.POOL)
                PoolChunk(chunk);
            else if(chunk.disposeStatus == DisposeState.FREE_MESH)
                FreeChunkBuffers(chunk);
        }
    }
    public void PoolChunk(BaseChunk chunk){
        FreeChunkBuffers(chunk);
        if(chunk is Chunk3D)  chunkPool3D.Enqueue(chunk); else chunkPool2D.Enqueue(chunk);
    }
    public void FreeChunkBuffers(BaseChunk chunk){
        activeChunks.Remove(chunk);
        if(chunk is Chunk3D){
            var _chunk = chunk as Chunk3D;
            if(_chunk.worldObject != null){
            _chunk.worldObject.name = "Pooled chunk";
            _chunk.worldObject.transform.SetParent(poolParent);
            _chunk.worldObject.SetActive(true);
            _chunk.worldObject.GetComponent<MeshCollider>().sharedMesh = null;
            objectPool.Enqueue(_chunk.worldObject);
            _chunk.worldObject = null;
            }
        }
        chunk.FreeBuffers();
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
            if(!toUpdate[i].IsReady || meshQueue.Contains(toUpdate[i] as BaseChunk)) continue;
            UpdateChunk(toUpdate[i] as BaseChunk);
            shouldUpdateTree = true;
        }
        if(chunk == null) chunk = chunkTree;
    }
    public void RegenerateChunk(BaseChunk chunk){
        if(activeChunks.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return;
        }
        chunk.chunkState = ChunkState.INVALID;
        chunk.disposeStatus = DisposeState.NOTHING;
        chunk.hasMesh = true;
        //chunk.worldObject = newChunk;
        activeChunks.Add(chunk);
        UpdateChunk(chunk);
    }
    public Chunk2D GetNewChunk2D(BoundingBox bounds, int depth){
        BaseChunk newChunk = null;
        if(chunkPool2D.Count > 0){
            newChunk = chunkPool2D.Dequeue();
        }else{
            newChunk = new Chunk2D(bounds, depth);
        }
        activeChunks.Add(newChunk);
        return newChunk as Chunk2D;
    }
    public Chunk3D GetNewChunk3D(BoundingBox bounds, int depth){
        BaseChunk newChunk = null;
        if(chunkPool3D.Count > 0){
            newChunk = chunkPool3D.Dequeue();
        }else{
            newChunk = new Chunk3D(bounds, depth);
        }
        activeChunks.Add(newChunk);
        return newChunk as Chunk3D;
    }
    public BaseChunk GenerateChunk(float3 pos, int depth, BoundingBox bounds){
        if(activeChunks.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return null;
        }
        BaseChunk newChunk = null;
        //float dst2D = math.distance(new float2(TerraxelWorld.playerBounds.center.x, TerraxelWorld.playerBounds.center.z), new float2(bounds.center.x, bounds.center.z));
        if(depth > simpleChunkTreshold){
            newChunk = GetNewChunk2D(bounds, depth);
        }else{
            newChunk = GetNewChunk3D(bounds, depth);
        }
        
        //newBaseChunk.worldObject = newChunk;
        newChunk.region = bounds;
        newChunk.depth = depth;
        newChunk.chunkState = ChunkState.INVALID;
        newChunk.disposeStatus = DisposeState.NOTHING;
        newChunk.hasMesh = true;
        UpdateChunk(newChunk);
        return newChunk;
    }
    void UpdateChunk(BaseChunk chunk){
        if(TerraxelWorld.worldState != TerraxelWorld.WorldState.MESH_UPDATE || MemoryManager.GetFreeVertexIndexBufferCount() == 0){
            chunk.chunkState = ChunkState.QUEUED;
            AddToMeshQueue(chunk);
            return;
        }
        /*if(BaseChunk.depth == 0){
            if(TerraxelWorld.DensityManager.ChunkIsFullOrEmpty((int3)BaseChunk.WorldPosition)){
                BaseChunk.OnMeshReady();
                BaseChunk.FreeChunkMesh();
                return;
            }
        }*/
        if(!chunk.CanBeCreated){
            chunk.chunkState = ChunkState.QUEUED;
            AddToMeshQueue(chunk);
            return;
        }
        chunk.ScheduleMeshUpdate();
    }
}
