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
using Sirenix.OdinInspector;
using System.Linq;
using UnityEditor;
using ReadOnly = Sirenix.OdinInspector.ReadOnlyAttribute;
using TMPro;
public class ChunkManager : MonoBehaviour, IDisposable
{
    public GameObject player;
    public static BoundingBox playerBounds;
    public const int chunkResolution = 32;
    public const int lodLevels = 6;
    public WorldGeneration.NoiseData noiseData;
    static WorldGeneration.NoiseData staticNoiseData;
    public GameObject chunkPrefab;
    static GameObject staticChunkPrefab;
    public static MemoryManager memoryManager;
    [ShowInInspector]
    static List<ChunkData> chunkDatas;
    static Queue<ChunkData> processQueue;
    Octree chunkTree;

    //DEBUG
    public bool debugMode;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int chunkCount;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly, InfoBox("Memory amount displayed in MB")]
    public int totalMemoryAllocated;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int memoryUsed;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int memoryWasted;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int vertCount;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int indexCount;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int freeBufferCount;
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
    public bool drawPlayerBounds;
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
    public bool drawChunkBounds;
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
    public ChunkDebugView drawChunkVariables;
    [BoxGroup("DEBUG"), HideIf("@!debugMode")]
    public bool debugLogs;
    [Serializable]
    public class ChunkDebugView{
        public bool position;
        public bool dirty;
        public bool dispose;
        public bool depth;
        public bool genTime;
        public bool vertexCount;
        public bool indexCount;
        public bool maxVertexCount;
        public bool draw{
            get{
                return position || dirty || dispose || depth || genTime || vertexCount || indexCount || maxVertexCount;
            }
        }
    }
    public void Start(){
        playerBounds = new BoundingBox(player.transform.position, new float3(chunkResolution * (int)math.pow(2, 2)));
        memoryManager = new MemoryManager();
        processQueue = new Queue<ChunkData>();
        memoryManager.AllocateDensityMaps(GetDensityMapLength(), (chunkResolution + 1)*(chunkResolution + 1)*(chunkResolution + 1));
        memoryManager.AllocateMeshData();
        chunkDatas = new List<ChunkData>();
        staticChunkPrefab = chunkPrefab;
        staticNoiseData = noiseData;
        chunkTree = new Octree();
        chunkTree.BuildTree();
        int elemCount = MemoryManager.maxBufferCount * MemoryManager.maxVertexCount;
        totalMemoryAllocated = elemCount * sizeof(float)*3 + elemCount * sizeof(uint);
        totalMemoryAllocated = totalMemoryAllocated / 1000000;
        //GenerateWorld();
    }
    void Update(){
        if(debugMode){
            memoryUsed = vertCount * sizeof(float) * 3 + indexCount * sizeof(uint);
            memoryUsed = memoryUsed / 1000000;
            memoryWasted = totalMemoryAllocated - memoryUsed;
        }
        vertCount = 0;
        indexCount = 0;
        for(int i = 0; i < chunkDatas.Count; i++){
            if(debugMode){
                vertCount += chunkDatas[i].vertCount;
                indexCount += chunkDatas[i].indexCount;
            }
            var chunk = chunkDatas[i];
            if(chunk.dirty){
                if(chunk.meshJobHandle.IsCompleted){
                    chunk.meshJobHandle.Complete();
                    chunk.ApplyMesh();
                }
                continue;
            }
            if(chunk.dispose){
                DisposeChunk(chunk);
            }
        }
        processQueue = new Queue<ChunkData>(processQueue.Where(x => x.dispose != true));
        if(processQueue.Count > 0){
            if(memoryManager.DensityMapAvailable && memoryManager.VertexBufferAvailable){
                var toBeProcessed = processQueue.Dequeue();
                if(!toBeProcessed.dispose)
                    GenerateMesh(toBeProcessed);
            }
        }
        if(debugMode){
            chunkCount = chunkDatas.Count;
            freeBufferCount = memoryManager.GetFreeBufferCount();
        }
        if(math.distance(playerBounds.center, player.transform.position) > 10f){
            if(debugLogs) Debug.Log("Updated octree");
            playerBounds.center = player.transform.position;
            chunkTree.BuildTree();
        }
    }
    [Button]
    public void RegenChunks(){
        foreach(var chunk in chunkDatas){
            GenerateMesh(chunk);
        }
    }
    public static void DisposeChunk(ChunkData chunk){
        chunkDatas.Remove(chunk);
        var mesh = chunk.worldObject.GetComponent<MeshFilter>().sharedMesh;
        //mesh.SetVertexBufferData(new Vector3[0],0,0,0,0,MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
        //mesh.SetIndexBufferData(new int[0],0,0,0,MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
        Destroy(chunk.worldObject);
        if(chunk.indices.IsCreated)
            memoryManager.ReturnIndexBuffer(chunk.indices);
        if(chunk.vertices.IsCreated)
            memoryManager.ReturnVertexBuffer(chunk.vertices);
        if(chunk.densityMap != default) memoryManager.ReturnDensityMap(chunk.densityMap);
    }
    public static ChunkData GenerateChunk(Vector3 pos, int depth){
        GameObject newChunk = Instantiate(staticChunkPrefab);
        newChunk.name = $"Chunk {pos.x}, {pos.y}, {pos.z}";
        newChunk.transform.position = pos;

        ChunkData newChunkData = new ChunkData(pos, newChunk, depth);
        
        //newChunkData.meshData = meshDataArray[idx];
        //chunkDatas[idx] = newChunkData;
        if(memoryManager.DensityMapAvailable && memoryManager.VertexBufferAvailable)
            GenerateMesh(newChunkData);
        else{
            processQueue.Enqueue(newChunkData);
        }
        chunkDatas.Add(newChunkData);
        return newChunkData;
    }
    static void GenerateMesh(ChunkData chunkData)
    {
        chunkData.genTime = Time.realtimeSinceStartup;
        chunkData.vertices = memoryManager.GetVertexBuffer();
        chunkData.indices = memoryManager.GetIndexBuffer();
        chunkData.dirty = true;
        chunkData.vertexCounter = new Counter(Allocator.Persistent);
        chunkData.indexCounter = new Counter(Allocator.Persistent);
        chunkData.densityMap = memoryManager.GetDensityMap();
        chunkData.vertexIndexBuffer = memoryManager.GetVertexIndexBuffer();

        var noiseJob = new NoiseJob()
        {
            ampl = staticNoiseData.ampl,
            freq = staticNoiseData.freq,
            oct = staticNoiseData.oct,
            offset = chunkData.pos,
            seed = staticNoiseData.offset,
            surfaceLevel = staticNoiseData.surfaceLevel,
            noiseMap = chunkData.densityMap,
            size = chunkResolution + 2,
            depthMultiplier = chunkData.depthMultiplier
            //pos = WorldSetup.positions
        };
        var noiseHandle = noiseJob.Schedule((chunkResolution + 2) * (chunkResolution + 2) * (chunkResolution + 2), 64);

        
        
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
        var noiseMapSize = chunkResolution + 2;
        return noiseMapSize * noiseMapSize * noiseMapSize;
    }
    public void OnDisable(){
        Dispose();
    }

    public void Dispose(){
        memoryManager.Dispose();
    }

    public static int XyzToIndex(int x, int y, int z, int width, int height)
    {
        return z * width * height + y * width + x;
    }
    public static int3 IndexToXyz(int index, int width, int height)
    {
        int3 position = new int3(
            index % width,
            index / width % height,
            index / (width * height));
        return position;
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
                float3 offset = chunkDatas[i].pos + chunkResolution * chunkDatas[i].depthMultiplier / 2;
                if(drawChunkVariables.depth){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].depth.ToString());
                }
                if(drawChunkVariables.dirty){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].dirty.ToString());
                }
                if(drawChunkVariables.dispose){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].dispose.ToString());
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
#endif
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
}
