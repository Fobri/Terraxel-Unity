using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WorldGeneration;
using Unity.Mathematics;
using Unity.Collections;
using DataStructures;
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
public class ChunkManager : MonoBehaviour, IDisposable
{
    public enum WorldState { DENSITY_UPDATE, MESH_UPDATE, IDLE }
    [ReadOnly]
    public WorldState worldState = WorldState.IDLE;
    public GameObject player;
    public static BoundingBox playerBounds;
    public static bool shouldUpdateTree = false;
    public const int chunkResolution = 32;
    public const int lodLevels = 6;
    public WorldGeneration.NoiseData noiseData;
    static WorldGeneration.NoiseData staticNoiseData;
    public GameObject chunkPrefab;
    static GameObject staticChunkPrefab;
    static Transform poolParent;
    static Transform activeParent;
    public static MemoryManager memoryManager;
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    static List<ChunkData> chunkDatas;
    static Queue<ChunkData> densityQueue;
    static Queue<ChunkData> meshQueue;
    static Queue<ChunkData> chunkPool;
    static Queue<GameObject> objectPool;
    ChunkData chunkTree;

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
        public bool draw{
            get{
                return position || chunkState || depth || genTime || vertexCount || indexCount || maxVertexCount;
            }
        }
    }
#endif
public TextMeshProUGUI[] debugLabels;
    public void Start(){
        playerBounds = new BoundingBox(player.transform.position, new float3(chunkResolution * (int)math.pow(2, 2)));
        memoryManager = new MemoryManager();
        densityQueue = new Queue<ChunkData>();
        meshQueue = new Queue<ChunkData>();
        chunkPool = new Queue<ChunkData>();
        objectPool = new Queue<GameObject>();
        memoryManager.Init();
        chunkDatas = new List<ChunkData>();
        staticChunkPrefab = chunkPrefab;
        staticNoiseData = noiseData;
        poolParent = transform.GetChild(0);
        activeParent = transform.GetChild(1);
        chunkTree = new ChunkData();
        chunkTree.chunkState = ChunkData.ChunkState.ROOT;
        chunkTree.UpdateTreeRecursive();
        if(debugMode){
            int elemCount = MemoryManager.maxBufferCount * MemoryManager.maxVertexCount;
            var ns = ChunkManager.chunkResolution + 3;
            var size = ns * ns * ns;
            int nmSize = MemoryManager.maxBufferCount * size;
            totalMemoryAllocated = elemCount * sizeof(float)*6 + elemCount * sizeof(ushort) + nmSize * sizeof(float);
            totalMemoryAllocated = totalMemoryAllocated / 1000000;
        }
    }
    void Update(){
        if(debugMode){
            memoryUsed = vertCount * sizeof(float) * 6 + indexCount * sizeof(ushort);
            memoryUsed = memoryUsed / 1000000;
            memoryWasted = totalMemoryAllocated - memoryUsed;
            freeDensityMaps = memoryManager.GetFreeVertexIndexBufferCount();
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
        bool densityUpdates = false;
        bool meshUpdates = false;
        for(int i = 0; i < chunkDatas.Count; i++){
            if(debugMode){
                vertCount += chunkDatas[i].vertCount;
                indexCount += chunkDatas[i].indexCount;
                totalGenTime += chunkDatas[i].genTime;
            }
            var chunk = chunkDatas[i];
            if(chunk.generationState == ChunkData.GenerationState.DENSITY && chunk.chunkState != ChunkData.ChunkState.READY) densityUpdates = true;
            else if (chunk.generationState == ChunkData.GenerationState.MESH && chunk.chunkState != ChunkData.ChunkState.READY) meshUpdates = true;
            if(chunk.chunkState == ChunkData.ChunkState.DIRTY){
                if(chunk.jobHandle.IsCompleted){
                    chunk.jobHandle.Complete();
                    if(chunk.generationState == ChunkData.GenerationState.MESH)
                        chunk.ApplyMesh();
                    else{
                        chunk.generationState = ChunkData.GenerationState.MESH;
                        UpdateChunk(chunk);
                    }
                }
                continue;
            }
            else if(chunk.disposeStatus != ChunkData.DisposeState.NOTHING){
                if(chunk.disposeStatus == ChunkData.DisposeState.POOL)
                    PoolChunk(chunk);
                else if(chunk.disposeStatus == ChunkData.DisposeState.FREE_MESH)
                    FreeChunkBuffers(chunk);
            }
        }
        if(!densityUpdates && worldState == WorldState.DENSITY_UPDATE){
            if(meshUpdates){}
        }
        if(worldState == WorldState.DENSITY_UPDATE){
            densityQueue = new Queue<ChunkData>(densityQueue.Where(x => x.disposeStatus == ChunkData.DisposeState.NOTHING));
            if(densityQueue.Count > 0){
                while(densityQueue.Count > 0){
                    var toBeProcessed = densityQueue.Dequeue();
                    UpdateChunk(toBeProcessed);
                }
            }else if(!densityUpdates){
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
            if(densityQueue.Count > 0) worldState = WorldState.DENSITY_UPDATE;
            else if(meshQueue.Count > 0) worldState = WorldState.MESH_UPDATE;
        }
        if(math.distance(playerBounds.center, player.transform.position) > 10f){
            playerBounds.center = player.transform.position;
            shouldUpdateTree = true;
        }
        if(shouldUpdateTree){
            shouldUpdateTree = false;
            chunkTree.UpdateTreeRecursive();
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
        chunk.generationState = ChunkData.GenerationState.DENSITY;
        chunk.hasMesh = true;
        //chunk.worldObject = newChunk;
        UpdateChunk(chunk);
        chunkDatas.Add(chunk);
    }
    public static ChunkData GenerateChunk(Vector3 pos, int depth, BoundingBox bounds){
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
            newChunkData.generationState = ChunkData.GenerationState.DENSITY;
        }else{
         newChunkData = new ChunkData(null, bounds, depth);
        }
        
        newChunkData.hasMesh = true;
        UpdateChunk(newChunkData);

        chunkDatas.Add(newChunkData);
        return newChunkData;
    }
    static void UpdateChunk(ChunkData chunkData){
        chunkData.genTime = Time.realtimeSinceStartup;
        if(chunkData.generationState == ChunkData.GenerationState.DENSITY){
            if(!chunkData.meshData.IsCreated){
                if(memoryManager.GetFreeMeshDataCount() > 0){
                    chunkData.meshData = memoryManager.GetMeshData();
                }else{
                    densityQueue.Enqueue(chunkData);
                    chunkData.chunkState = ChunkData.ChunkState.QUEUED;
                    return;
                }
            }
            chunkData.chunkState = ChunkData.ChunkState.DIRTY;
            chunkData.jobHandle = ScheduleDensityJob(chunkData);
        }
        else{
            if(memoryManager.GetFreeVertexIndexBufferCount() == 0){
                chunkData.chunkState = ChunkData.ChunkState.QUEUED;
                meshQueue.Enqueue(chunkData);
                return;
            }
            chunkData.vertexIndexBuffer = memoryManager.GetVertexIndexBuffer();
            chunkData.vertCount = 0;
            chunkData.indexCount = 0;
            chunkData.vertexCounter = new Counter(Allocator.Persistent);
            chunkData.indexCounter = new Counter(Allocator.Persistent);
            chunkData.chunkState = ChunkData.ChunkState.DIRTY;
            chunkData.jobHandle = ScheduleMeshJob(chunkData);
        }
    }
    static JobHandle ScheduleDensityJob(ChunkData chunkData){
        var noiseJob = new NoiseJob()
        {
            ampl = staticNoiseData.ampl,
            freq = staticNoiseData.freq,
            oct = staticNoiseData.oct,
            offset = chunkData.WorldPosition - chunkData.depthMultiplier,
            seed = staticNoiseData.offset,
            surfaceLevel = staticNoiseData.surfaceLevel,
            noiseMap = chunkData.meshData.densityBuffer,
            size = chunkResolution + 3,
            depthMultiplier = chunkData.depthMultiplier
            //pos = WorldSetup.positions
        };
        return noiseJob.Schedule((chunkResolution + 3) * (chunkResolution + 3) * (chunkResolution + 3), 64);
    }
    static JobHandle ScheduleMeshJob(ChunkData chunkData)
    {
        var marchingJob = new MarchingJob()
        {
            densities = chunkData.meshData.densityBuffer,
            isolevel = 0f,
            chunkSize = chunkResolution + 1,
            vertices = chunkData.meshData.vertexBuffer,
            //triangles = chunkData.indices,
            vertexCounter = chunkData.vertexCounter,
            depthMultiplier = chunkData.depthMultiplier,
            vertexIndices = chunkData.vertexIndexBuffer
            
        };
        var marchingHandle = marchingJob.Schedule((chunkResolution + 1) * (chunkResolution + 1) * (chunkResolution + 1), 32);

        var vertexSharingJob = new VertexSharingJob()
        {
            triangles = chunkData.meshData.indexBuffer,
            chunkSize = chunkResolution + 1,
            counter = chunkData.indexCounter,
            vertexIndices = chunkData.vertexIndexBuffer
        };
        return vertexSharingJob.Schedule((chunkResolution + 1) * (chunkResolution + 1) * (chunkResolution + 1), 32, marchingHandle);
    }
    public void OnDisable(){
        Dispose();
    }

    public void Dispose(){
        memoryManager.Dispose();
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
            RenderOctree(chunkTree);
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
                    Handles.Label(offset, chunkDatas[i].genTime.ToString());
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
            }
        }
    }
    void RenderOctree(Octree tree)
    {
        if (tree == null)
            return;
        if(tree.octants == null) return;
        foreach(BoundingBox b in tree.octants)
        {
            b.Draw();
        }

        if (tree.children == null)
            return;

        foreach (Octree child in tree.children)
        {
            if (child == null || child.octants == null)
                continue;

            foreach(BoundingBox b in child.octants)
            {
                b.Draw();
                RenderOctree(child);
            }
        }
    }
#endif
}
