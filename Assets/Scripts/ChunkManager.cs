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
public class ChunkManager : MonoBehaviour, IDisposable
{
    public GameObject player;
    public static BoundingBox playerBounds;
    public const int chunkResolution = 32;
    public const int lodLevels = 5;
    public WorldGeneration.NoiseData noiseData;
    static WorldGeneration.NoiseData staticNoiseData;
    public GameObject chunkPrefab;
    static GameObject staticChunkPrefab;
    public static MemoryManager memoryManager;
    static List<ChunkData> chunkDatas;
    static Queue<ChunkData> processQueue;
    Octree chunkTree;

    //DEBUG
    public bool debugMode;
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
    public int chunkCount;
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
        public bool draw{
            get{
                return position || dirty || dispose || depth || genTime;
            }
        }
    }
    public void Start(){
        playerBounds = new BoundingBox(player.transform.position, new float3(chunkResolution * (int)math.pow(2, 2)));
        memoryManager = new MemoryManager();
        processQueue = new Queue<ChunkData>();
        memoryManager.AllocateDensityMaps(GetDensityMapLength());
        memoryManager.AllocateMeshData(chunkResolution * chunkResolution * chunkResolution * 3 * 5, chunkResolution * chunkResolution * chunkResolution * 3 * 5);
        chunkDatas = new List<ChunkData>();
        staticChunkPrefab = chunkPrefab;
        staticNoiseData = noiseData;
        chunkTree = new Octree();
        chunkTree.BuildTree();
        //GenerateWorld();
    }
    void Update(){
        for(int i = 0; i < chunkDatas.Count; i++){
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
        chunkData.counter = new Counter(Allocator.Persistent);
        var arraySize = chunkResolution * chunkResolution * chunkResolution;
        chunkData.densityMap = memoryManager.GetDensityMap();

        var noiseJob = new NoiseJob()
        {
            ampl = staticNoiseData.ampl,
            freq = staticNoiseData.freq,
            oct = staticNoiseData.oct,
            offset = chunkData.pos,
            seed = staticNoiseData.offset,
            surfaceLevel = staticNoiseData.surfaceLevel,
            noiseMap = chunkData.densityMap,
            size = chunkResolution + 1,
            depthMultiplier = chunkData.depthMultiplier
            //pos = WorldSetup.positions
        };
        var noiseHandle = noiseJob.Schedule((chunkResolution + 1) * (chunkResolution + 1) * (chunkResolution + 1), 64);

        
        
        var marchingJob = new MarchingJob()
        {
            densities = chunkData.densityMap,
            isolevel = 0f,
            chunkSize = chunkResolution,
            vertices = chunkData.vertices,
            triangles = chunkData.indices,
            counter = chunkData.counter,
            depthMultiplier = chunkData.depthMultiplier
        };
        var marchinJob = marchingJob.Schedule(arraySize, 32, noiseHandle);
        chunkData.meshJobHandle = marchinJob;
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

    private void OnDrawGizmos()
    {
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
                float3 offset = chunkDatas[i].pos + 0.1f;
                if(drawChunkVariables.depth){
                    offset += 1f;
                    Handles.Label(chunkDatas[i].pos, chunkDatas[i].depth.ToString());
                }
                if(drawChunkVariables.dirty){
                    offset += 1f;
                    Handles.Label(chunkDatas[i].pos, chunkDatas[i].dirty.ToString());
                }
                if(drawChunkVariables.dispose){
                    offset += 1f;
                    Handles.Label(chunkDatas[i].pos, chunkDatas[i].dispose.ToString());
                }
                if(drawChunkVariables.genTime){
                    offset += 1f;
                    Handles.Label(chunkDatas[i].pos, chunkDatas[i].genTime.ToString());
                }
                if(drawChunkVariables.position){
                    offset += 1f;
                    Handles.Label(chunkDatas[i].pos, chunkDatas[i].pos.ToString());
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
}
