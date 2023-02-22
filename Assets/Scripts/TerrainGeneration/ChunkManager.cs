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
public class ChunkManager : MonoBehaviour, IDisposable
{
    public enum WorldState { DENSITY_UPDATE, MESH_UPDATE, IDLE }
    [ShowInInspector]
    public static WorldState worldState = WorldState.IDLE;
    public GameObject player;
    public static BoundingBox playerBounds;
    public static bool shouldUpdateTree = false;
    public const int chunkResolution = 32;
    public const int lodLevels = 4;
    public WorldGeneration.NoiseData noiseData;
    public static WorldGeneration.NoiseData staticNoiseData;
    public GameObject chunkPrefab;
    static GameObject staticChunkPrefab;
    static Transform poolParent;
    static Transform activeParent;
    public static MemoryManager memoryManager;
    public static DensityManager densityManager;
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    static List<ChunkData> chunkDatas;
    static Queue<ChunkData> disposeQueue;
    static Queue<ChunkData> meshQueue;
    static Queue<ChunkData> chunkPool;
    static Queue<GameObject> objectPool;
    public static ChunkData chunkTree {get; private set;}

    //DEBUG
    [DisableInPlayMode]
    public bool debugMode;
#if ODIN_INSPECTOR    
[BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int chunkCount;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int freeBufferCount;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly, InfoBox("This should be the total allocated vertex buffer count. Should be @MemoryManager.maxBufferCount")]
#endif
    public int totalChunksAccountedFor;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly, InfoBox("Temporary buffers available. Should be @MemoryManager.densityMapCount")]
#endif
    public int freeDensityMaps;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly, InfoBox("Memory amount displayed in MB")]
#endif
    public int totalMemoryAllocated;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int memoryUsed;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int memoryWasted;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int vertCount;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int indexCount;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public float totalGenTime;
#if UNITY_EDITOR
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
#endif
    public bool drawPlayerBounds;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
#endif
    public bool drawChunkBounds;
    #if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
#endif
    public bool drawDensityMaps;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
#endif
    public ChunkDebugView drawChunkVariables;
    [Serializable]
    public class ChunkDebugView{
        public bool position;
        public bool chunkState;
        public bool depth;
        public bool genTime;
        public bool vertexCount;
        public bool indexCount;
        public bool maxVertexCount;
        public bool dirMask;
        public bool draw{
            get{
                return position || chunkState || depth || genTime || vertexCount || indexCount || maxVertexCount || dirMask;
            }
        }
    }
#endif
public TextMeshProUGUI[] debugLabels;
    public void Start(){
        playerBounds = new BoundingBox(player.transform.position, new float3(chunkResolution * (int)math.pow(2, 2)));
        memoryManager = new MemoryManager();
        densityManager = new DensityManager();
        meshQueue = new Queue<ChunkData>();
        chunkPool = new Queue<ChunkData>();
        disposeQueue = new Queue<ChunkData>();
        objectPool = new Queue<GameObject>();
        memoryManager.Init();
        densityManager.Init();
        chunkDatas = new List<ChunkData>();
        staticChunkPrefab = chunkPrefab;
        staticNoiseData = noiseData;
        poolParent = transform.GetChild(0);
        activeParent = transform.GetChild(1);
        densityManager.LoadDensityData(new float3(0));
        chunkTree = new ChunkData();
        chunkTree.chunkState = ChunkData.ChunkState.ROOT;
        chunkTree.UpdateTreeRecursive();
        if(debugMode){
            int elemCount = MemoryManager.maxBufferCount * MemoryManager.maxVertexCount;
            var ns = ChunkManager.chunkResolution + 1;
            var size = ns * ns * ns;
            int nmSize = MemoryManager.maxBufferCount * size;
            totalMemoryAllocated = elemCount * sizeof(float)*6 + elemCount * sizeof(ushort) + nmSize * sizeof(sbyte);
            totalMemoryAllocated = totalMemoryAllocated / 1000000;
        }
    }
    void Update(){
        if(debugMode){
            memoryUsed = vertCount * sizeof(float) * 6 + 
                        indexCount * sizeof(ushort) + 
                        (ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1) * sizeof(sbyte) * chunkCount;
            memoryUsed = memoryUsed / 1000000;
            memoryWasted = totalMemoryAllocated - memoryUsed;
            freeDensityMaps = memoryManager.GetFreeDensityMapCount();
            totalChunksAccountedFor = chunkCount + freeBufferCount;
            chunkCount = chunkDatas.Count;
            freeBufferCount = memoryManager.GetFreeMeshDataCount();
            debugLabels[0].text = $"Total Memory allocated: {totalMemoryAllocated} Mb";
            debugLabels[1].text = $"Memory used: {memoryUsed} Mb";
            debugLabels[2].text = $"Free Memory: {memoryWasted} Mb";
            debugLabels[3].text = $"Currently processing {MemoryManager.maxConcurrentOperations - freeDensityMaps}/{MemoryManager.maxConcurrentOperations}";
            debugLabels[4].text = $"Chunk count: {chunkCount}/{MemoryManager.maxBufferCount}";
            debugLabels[5].text = $"Free vertex buffers: {freeBufferCount}";
            debugLabels[6].text = $"Total chunk generation time: {totalGenTime}";
            debugLabels[7].text = $"Vertex count: {vertCount}";
            debugLabels[8].text = $"Index count: {indexCount}";
        }
        vertCount = 0;
        indexCount = 0;
        totalGenTime = 0f;
        bool meshUpdates = false;
        if(debugMode){
            for(int i = 0; i < chunkDatas.Count; i++){
                    vertCount += chunkDatas[i].vertCount;
                    indexCount += chunkDatas[i].indexCount;
                    totalGenTime += chunkDatas[i].genTime;
            }
        }
        var disposeCount = disposeQueue.Count;
        for(int i = 0; i < disposeCount; i++){
            var chunk = disposeQueue.Dequeue();
            DisposeChunk(chunk);
        }
        if(worldState == WorldState.DENSITY_UPDATE){
            if(!densityManager.HasPendingUpdates && densityManager.JobsReady){
                worldState = WorldState.IDLE;
            }
        }
        else if(worldState == WorldState.MESH_UPDATE){
            meshQueue = new Queue<ChunkData>(meshQueue.Where(x => x.disposeStatus == ChunkData.DisposeState.NOTHING));
            if(meshQueue.Count > 0){
                while(meshQueue.Count > 0 && memoryManager.GetFreeVertexIndexBufferCount() > 0){
                    var toBeProcessed = meshQueue.Dequeue();
                    if(!toBeProcessed.meshData.IsCreated && memoryManager.GetFreeMeshDataCount() == 0) {
                        meshQueue.Enqueue(toBeProcessed);
                        break;
                    }
                    UpdateChunk(toBeProcessed);
                }
            }else if(!meshUpdates){
                worldState = WorldState.IDLE;
            }
        }
        if(worldState == WorldState.IDLE){
            if(densityManager.HasPendingUpdates) worldState = WorldState.DENSITY_UPDATE;
            else if(meshQueue.Count > 0 || meshUpdates) worldState = WorldState.MESH_UPDATE;
        }
        if(math.distance(playerBounds.center, player.transform.position) > 10f){
            playerBounds.center = player.transform.position;
            shouldUpdateTree = true;
        }
        if(shouldUpdateTree){
            shouldUpdateTree = false;
            chunkTree.UpdateTreeRecursive();
            for(int i = 0; i < chunkDatas.Count; i++){
                //TODO: useless, figure out better way to only refresh states of chunks that are adjacent to mesh updates
                chunkDatas[i].RefreshRenderState();
            }
        }
    }
    public static void DisposeChunk(ChunkData chunk){
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
    public static void PoolChunk(ChunkData chunk){
        FreeChunkBuffers(chunk);
        chunkPool.Enqueue(chunk);
    }
    public static void FreeChunkBuffers(ChunkData chunk){
        chunkDatas.Remove(chunk);
        //Destroy(chunk.worldObject);
        //chunk.worldObject.SetActive(false);
        if(chunk.worldObject != null){
            chunk.worldObject.name = "Pooled chunk";
            chunk.worldObject.transform.SetParent(poolParent);
            chunk.worldObject.GetComponent<MeshFilter>().sharedMesh.Clear();
            for(int i = 0; i < 6; i++){
                chunk.worldObject.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh.Clear();
            }
            objectPool.Enqueue(chunk.worldObject);
        }
        chunk.hasMesh = false;
        chunk.worldObject = null;
        if(chunk.meshData.IsCreated){
            memoryManager.ReturnMeshData(chunk.meshData);
            chunk.meshData = default;
        }
    }
    public static GameObject GetChunkObject(){
        if(objectPool.Count > 0){
            var chunk = objectPool.Dequeue();
            chunk.transform.SetParent(activeParent);
            return chunk;
        }
        else{
            var chunk = Instantiate(staticChunkPrefab);
            chunk.GetComponent<MeshFilter>().sharedMesh = new Mesh();
            for(int i = 0; i < 6; i++){
                chunk.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh = new Mesh();
            }
            chunk.transform.SetParent(activeParent);
            return chunk;
        }
    }
    public static void RegenerateChunk(ChunkData chunk){
        if(chunkDatas.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return;
        }
        /*GameObject newChunk = GetChunkObject();
        newChunk.name = $"Chunk {chunk.WorldPosition.x}, {chunk.WorldPosition.y}, {chunk.WorldPosition.z}";
        newChunk.transform.position = chunk.WorldPosition;*/
        chunk.chunkState = ChunkData.ChunkState.INVALID;
        chunk.disposeStatus = ChunkData.DisposeState.NOTHING;
        chunk.hasMesh = true;
        //chunk.worldObject = newChunk;
        UpdateChunk(chunk);
        chunkDatas.Add(chunk);
    }
    public static ChunkData GenerateChunk(float3 pos, int depth, BoundingBox bounds){
        if(chunkDatas.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return null;
        }
        /*GameObject newChunk = GetChunkObject();
        newChunk.name = $"Chunk {pos.x}, {pos.y}, {pos.z}";
        newChunk.transform.position = pos;*/
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
        UpdateChunk(newChunkData);

        chunkDatas.Add(newChunkData);
        return newChunkData;
    }
    static void UpdateChunk(ChunkData chunkData){
        
        if(memoryManager.GetFreeVertexIndexBufferCount() == 0 || worldState != WorldState.MESH_UPDATE){
            chunkData.chunkState = ChunkData.ChunkState.QUEUED;
            meshQueue.Enqueue(chunkData);
            return;
        }
        chunkData.vertexIndexBuffer = memoryManager.GetVertexIndexBuffer();
        chunkData.meshData = memoryManager.GetMeshData();
        chunkData.vertCount = 0;
        chunkData.indexCount = 0;
        chunkData.vertexCounter = new Counter(Allocator.Persistent);
        chunkData.indexCounter = new Counter(Allocator.Persistent);
        chunkData.chunkState = ChunkData.ChunkState.DIRTY;
        chunkData.UpdateMesh();
    }
    public void OnDisable(){
        Dispose();
    }

    public void Dispose(){
        foreach(var chunk in chunkDatas){
            chunk.CompleteJobs();
        }
        memoryManager.Dispose();
        densityManager.Dispose();
    }

    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if(!debugMode) return;
        
        if(drawPlayerBounds){
            Gizmos.DrawWireCube(playerBounds.center, playerBounds.bounds);
        }
        if(drawChunkBounds){
            Gizmos.DrawWireCube(Vector3.zero, new float3(chunkResolution * math.pow(2, lodLevels)));
        }
        if(drawDensityMaps){
            var maps = densityManager.GetDebugArray();
            foreach(var bound in maps){
                Gizmos.DrawWireCube(bound.center, bound.bounds);
            }
        }
        if(drawChunkVariables.draw){
            GUI.color = Color.green;
            for(int i = 0; i < chunkDatas.Count; i++){
                float3 offset = chunkDatas[i].WorldPosition + chunkResolution * chunkDatas[i].depthMultiplier / 2;
                if(drawChunkVariables.depth){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].depth.ToString());
                }
                if(drawChunkVariables.chunkState){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].chunkState.ToString());
                }
                if(drawChunkVariables.genTime){
                    offset.y += 4f;
                    Handles.Label(offset, math.round(chunkDatas[i].genTime * 1000).ToString() + "ms");
                }
                if(drawChunkVariables.vertexCount){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].vertCount.ToString());
                }
                if(drawChunkVariables.indexCount){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].indexCount.ToString());
                }
                if(drawChunkVariables.maxVertexCount){
                    offset.y += 4f;
                    Handles.Label(offset, MemoryManager.maxVertexCount.ToString());
                }
                if(drawChunkVariables.dirMask){
                    offset.y += 4f;
                    Handles.Label(offset, Utils.DirectionMaskToString(chunkDatas[i].dirMask));
                }
                if(drawChunkBounds){
                    Gizmos.DrawWireCube(chunkDatas[i].region.center, chunkDatas[i].region.bounds);
                }
            }
        }
    }
#endif
}
