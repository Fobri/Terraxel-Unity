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
public class ChunkManager : MonoBehaviour, IDisposable
{
    public GameObject player;
    public static BoundingBox playerBounds;
    public static bool shouldUpdateTree = false;
    public const int chunkResolution = 32;
    public const int lodLevels = 6;
    public WorldGeneration.NoiseData noiseData;
    static WorldGeneration.NoiseData staticNoiseData;
    public GameObject chunkPrefab;
    static GameObject staticChunkPrefab;
    public static MemoryManager memoryManager;
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    static List<ChunkData> chunkDatas;
    static Queue<ChunkData> processQueue;
    static Queue<ChunkData> chunkPool;
    ChunkData chunkTree;

#if UNITY_EDITOR
    //DEBUG
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
    public void Start(){
        playerBounds = new BoundingBox(player.transform.position, new float3(chunkResolution * (int)math.pow(2, 2)));
        memoryManager = new MemoryManager();
        processQueue = new Queue<ChunkData>();
        chunkPool = new Queue<ChunkData>();
        memoryManager.AllocateDensityMaps(GetDensityMapLength(), (chunkResolution + 1)*(chunkResolution + 1)*(chunkResolution + 1));
        memoryManager.AllocateMeshData();
        chunkDatas = new List<ChunkData>();
        staticChunkPrefab = chunkPrefab;
        staticNoiseData = noiseData;
        chunkTree = new ChunkData();
        chunkTree.chunkState = ChunkData.ChunkState.ROOT;
        chunkTree.UpdateTreeRecursive();
#if UNITY_EDITOR
        int elemCount = MemoryManager.maxBufferCount * MemoryManager.maxVertexCount;
        totalMemoryAllocated = elemCount * sizeof(float)*3 + elemCount * sizeof(ushort);
        totalMemoryAllocated = totalMemoryAllocated / 1000000;
#endif
    }
    void Update(){
#if UNITY_EDITOR
        if(debugMode){
            memoryUsed = vertCount * sizeof(float) * 3 + indexCount * sizeof(ushort);
            memoryUsed = memoryUsed / 1000000;
            memoryWasted = totalMemoryAllocated - memoryUsed;
            freeDensityMaps = memoryManager.GetFreeDensityCount();
            totalChunksAccountedFor = chunkCount + freeBufferCount;
            chunkCount = chunkDatas.Count;
            freeBufferCount = memoryManager.GetFreeBufferCount();
        }
        vertCount = 0;
        indexCount = 0;
#endif
        for(int i = 0; i < chunkDatas.Count; i++){
#if UNITY_EDITOR
            if(debugMode){
                vertCount += chunkDatas[i].vertCount;
                indexCount += chunkDatas[i].indexCount;
            }
#endif
            var chunk = chunkDatas[i];
            if(chunk.chunkState == ChunkData.ChunkState.DIRTY){
                if(chunk.meshJobHandle.IsCompleted){
                    chunk.meshJobHandle.Complete();
                    chunk.ApplyMesh();
                }
                continue;
            }
            else if(chunk.disposeStatus != ChunkData.DisposeStatus.NOTHING){
                if(chunk.disposeStatus == ChunkData.DisposeStatus.POOL)
                    PoolChunk(chunk);
                else if(chunk.disposeStatus == ChunkData.DisposeStatus.FREE_MESH)
                    FreeChunkBuffers(chunk);
            }
        }
        processQueue = new Queue<ChunkData>(processQueue.Where(x => x.disposeStatus == ChunkData.DisposeStatus.NOTHING));
        if(processQueue.Count > 0){
            if(memoryManager.DensityMapAvailable && memoryManager.VertexBufferAvailable){
                var toBeProcessed = processQueue.Dequeue();
                GenerateMesh(toBeProcessed);
            }
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
        Destroy(chunk.worldObject);
        if(chunk.indices.IsCreated){
            memoryManager.ReturnIndexBuffer(chunk.indices);
            chunk.indices = default;
        }
        if(chunk.vertices.IsCreated){
            memoryManager.ReturnVertexBuffer(chunk.vertices);
            chunk.vertices = default;
        }
        if(chunk.densityMap != default){ 
            memoryManager.ReturnDensityMap(chunk.densityMap);
            chunk.densityMap = default;
        }
        if(chunk.vertexIndexBuffer != default){
            memoryManager.ReturnVertexIndexBuffer(chunk.vertexIndexBuffer);
            chunk.vertexIndexBuffer = default;
        }
    }
    public static void RegenerateChunk(ChunkData chunk){
        if(chunkDatas.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return;
        }
        GameObject newChunk = Instantiate(staticChunkPrefab);
        newChunk.name = $"Chunk {chunk.WorldPosition.x}, {chunk.WorldPosition.y}, {chunk.WorldPosition.z}";
        newChunk.transform.position = chunk.WorldPosition;
        newChunk.GetComponent<MeshFilter>().sharedMesh = new Mesh();
        chunk.chunkState = ChunkData.ChunkState.INVALID;
        chunk.disposeStatus = ChunkData.DisposeStatus.NOTHING;

        chunk.worldObject = newChunk;
        GenerateMesh(chunk);
        chunkDatas.Add(chunk);
    }
    public static ChunkData GenerateChunk(Vector3 pos, int depth, BoundingBox bounds){
        if(chunkDatas.Count >= MemoryManager.maxBufferCount){ 
            shouldUpdateTree = true;
            return null;
        }
        GameObject newChunk = Instantiate(staticChunkPrefab);
        newChunk.name = $"Chunk {pos.x}, {pos.y}, {pos.z}";
        newChunk.transform.position = pos;
        newChunk.GetComponent<MeshFilter>().sharedMesh = new Mesh();
        ChunkData newChunkData = null;
        if(chunkPool.Count > 0){
            newChunkData = chunkPool.Dequeue();
            newChunkData.worldObject = newChunk;
            newChunkData.region = bounds;
            newChunkData.depth = depth;
            newChunkData.chunkState = ChunkData.ChunkState.INVALID;
            newChunkData.disposeStatus = ChunkData.DisposeStatus.NOTHING;
        }else{
         newChunkData = new ChunkData(newChunk, bounds, depth);
        }
        
        GenerateMesh(newChunkData);

        chunkDatas.Add(newChunkData);
        return newChunkData;
    }
    static void GenerateMesh(ChunkData chunkData)
    {
        if(!memoryManager.DensityMapAvailable || !memoryManager.VertexBufferAvailable){
            processQueue.Enqueue(chunkData);
            return;
        }
        chunkData.chunkState = ChunkData.ChunkState.DIRTY;
#if UNITY_EDITOR
        chunkData.genTime = Time.realtimeSinceStartup;
#endif
        chunkData.vertCount = 0;
        chunkData.indexCount = 0;
        chunkData.vertices = memoryManager.GetVertexBuffer();
        chunkData.indices = memoryManager.GetIndexBuffer();
        chunkData.vertexCounter = new Counter(Allocator.Persistent);
        chunkData.indexCounter = new Counter(Allocator.Persistent);
        chunkData.densityMap = memoryManager.GetDensityMap();
        chunkData.vertexIndexBuffer = memoryManager.GetVertexIndexBuffer();

        var noiseJob = new NoiseJob()
        {
            ampl = staticNoiseData.ampl,
            freq = staticNoiseData.freq,
            oct = staticNoiseData.oct,
            offset = chunkData.WorldPosition - chunkData.depthMultiplier,
            seed = staticNoiseData.offset,
            surfaceLevel = staticNoiseData.surfaceLevel,
            noiseMap = chunkData.densityMap,
            size = chunkResolution + 3,
            depthMultiplier = chunkData.depthMultiplier
            //pos = WorldSetup.positions
        };
        var noiseHandle = noiseJob.Schedule((chunkResolution + 3) * (chunkResolution + 3) * (chunkResolution + 3), 64);

        
        
        var marchingJob = new MarchingJob()
        {
            densities = chunkData.densityMap,
            isolevel = 0f,
            chunkSize = chunkResolution + 1,
            vertices = chunkData.vertices,
            //triangles = chunkData.indices,
            vertexCounter = chunkData.vertexCounter,
            depthMultiplier = chunkData.depthMultiplier,
            vertexIndices = chunkData.vertexIndexBuffer
            
        };
        var marchingHandle = marchingJob.Schedule((chunkResolution + 1) * (chunkResolution + 1) * (chunkResolution + 1), 32, noiseHandle);

        var vertexSharingJob = new VertexSharingJob()
        {
            triangles = chunkData.indices,
            chunkSize = chunkResolution + 1,
            counter = chunkData.indexCounter,
            vertexIndices = chunkData.vertexIndexBuffer
        };
        chunkData.meshJobHandle = vertexSharingJob.Schedule((chunkResolution + 1) * (chunkResolution + 1) * (chunkResolution + 1), 32, marchingHandle);
    }

    int GetDensityMapLength(){
        var noiseMapSize = chunkResolution + 3;
        return noiseMapSize * noiseMapSize * noiseMapSize;
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
