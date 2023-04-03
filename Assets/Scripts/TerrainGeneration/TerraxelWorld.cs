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
using System.Linq;
using UnityEditor;
using TMPro;
using Unity.Profiling;

[DisallowMultipleComponent]
public class TerraxelWorld : MonoBehaviour
{
    public enum WorldState { DENSITY_UPDATE, MESH_UPDATE, IDLE }
    [HideInInspector]
    public TerraxelWorldSettings worldSettings;
    static TerraxelWorldSettings m_worldSettings;
    [HideInInspector]
    public GameObject player;
    
    Transform poolParent;
    Transform activeParent;
    GameObject debugCanvas;
    int3 worldOffset = Int16.MaxValue;
    int3 playerOffset = Int16.MaxValue;

    ComputeShader noiseShader;
    GameObject chunkPrefab;
    int baseChunkSize = ChunkManager.chunkResolution * Octree.depthMultipliers[ChunkManager.lodLevels - 2];
    
    //STATIC VARS
    public static DensityManager DensityManager {get; private set;}
    public static ChunkManager ChunkManager {get; private set;}
    public static bool worldUpdatePending {get; private set;}
    public static bool frustumCulling{
        get{
            #if !UNITY_EDITOR
            return true;
            #endif
            #if UNITY_EDITOR
            return m_worldSettings.frustumCulling;
            #endif
        }
    }
    public static bool renderGrass{
        get{
            return m_worldSettings.renderGrass;
        }
    }
    public static BoundingBox playerBounds;
    public static WorldState worldState = WorldState.IDLE;
    public static Camera renderCamera;
    public static int seed;

    //DEBUG
    [SerializeField, HideInInspector]
    private bool debugMode;
    private int chunkCount;
    private int freeBufferCount;
    private int freeDensityMaps;
    private int memoryUsed;
    private int vertCount;
    private int indexCount;
    private float totalGenTime;
#if UNITY_EDITOR
    [SerializeField]
    private bool drawPlayerBounds;
    [SerializeField]
    private bool drawChunkBounds;
    [SerializeField]
    private bool drawDensityMaps;
    [SerializeField]
    private ChunkDebugView drawChunkVariables;
    [Serializable]
    public class ChunkDebugView{
        public bool position;
        public bool chunkState;
        public bool depth;
        public bool genTime;
        public bool vertexCount;
        public bool indexCount;
        public bool dirMask;
        public bool type;
        public bool draw{
            get{
                return position || chunkState || depth || genTime || vertexCount || indexCount || dirMask || type;
            }
        }
    }
#endif
float totalChunkGenTime;
float totalDensityGenTime;
TextMeshProUGUI[] debugLabels;
    public void Start(){
        m_worldSettings = worldSettings;
        seed = worldSettings.seed == 0 ? UnityEngine.Random.Range(Int16.MinValue, Int16.MaxValue) : worldSettings.seed;
        debugCanvas = Resources.Load<GameObject>("Prefabs/TerraxelDebug");
        var canv = Instantiate(debugCanvas);
        debugLabels = new TextMeshProUGUI[canv.transform.childCount];
        for(int i = 0; i < debugLabels.Length; i++){
            debugLabels[i] = canv.transform.GetChild(i).GetComponent<TextMeshProUGUI>();
        }
        poolParent = new GameObject("Pooled Chunks").transform;
        poolParent.SetParent(transform);
        activeParent = new GameObject("Active Chunks").transform;
        activeParent.SetParent(transform);
        chunkPrefab = Resources.Load<GameObject>("Prefabs/Chunk");
        noiseShader = Resources.Load<ComputeShader>("Generated/TerraxelGenerated");
        renderCamera = Camera.main;
        MemoryManager.Init();
        m_worldSettings.generator.Init();
        playerBounds = new BoundingBox(player.transform.position, new float3(ChunkManager.chunkResolution));
        DensityManager = new DensityManager();
        DensityManager.Init(noiseShader);
        ChunkManager = new ChunkManager();
        ChunkManager.Init(poolParent, activeParent, chunkPrefab);
        player.SetActive(false);
        if(worldSettings.placePlayerOnSurface){
            var startHeight = TerraxelGenerated.GenerateDensity(new float2(player.transform.position.x, player.transform.position.z), seed) + 0.1f;
            player.transform.position = player.transform.position * new float3(1,0,1) + new float3(0,startHeight, 0);
        }
    }
    public void Update(){
        JobRunner.Update();
        if(debugMode){
            var chunkDatas = ChunkManager.GetDebugArray();
            memoryUsed = vertCount * sizeof(float) * 9 + vertCount * sizeof(int) + 
                        indexCount * sizeof(ushort) + 
                        (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution) * sizeof(sbyte) * Octree.depthMultipliers[2] * Octree.depthMultipliers[2] * Octree.depthMultipliers[2];
            memoryUsed = memoryUsed / 1000000;
            freeDensityMaps = MemoryManager.GetFreeDensityMapCount();
            chunkCount = chunkDatas.Count;
            freeBufferCount = MemoryManager.GetFreeMeshDataCount();
            //debugLabels[0].text = $"Memory used: {memoryUsed} Mb";
            debugLabels[1].text = $"Currently processing {MemoryManager.maxConcurrentOperations - MemoryManager.GetFreeVertexIndexBufferCount()}/{MemoryManager.maxConcurrentOperations}";
            debugLabels[2].text = $"Chunk count: {chunkCount}";
            debugLabels[3].text = $"Free vertex buffers: {freeBufferCount}";
            debugLabels[4].text = $"Last chunk generation time: {totalChunkGenTime}";
            debugLabels[5].text = $"Last density generation time: {totalDensityGenTime}";
            debugLabels[6].text = $"Vertex count: {vertCount}";
            debugLabels[7].text = $"Index count: {indexCount}";
            vertCount = 0;
            indexCount = 0;
            totalGenTime = 0f;
            if(debugMode){
                for(int i = 0; i < chunkDatas.Count; i++){
                        vertCount += chunkDatas[i].vertCount;
                        indexCount += chunkDatas[i].idxCount;
                        totalGenTime += chunkDatas[i].genTime;
                }
            }
        }
        /*for(int i = 0; i < ChunkManager.GetDebugArray().Count; i++){
            ChunkManager.GetDebugArray()[i].RenderChunk();
        }*/
        var planes = GeometryUtility.CalculateFrustumPlanes(renderCamera);
        ChunkManager.RenderChunks(planes);
        if(worldState == WorldState.DENSITY_UPDATE){
            if(DensityManager.Update()){
                worldState = WorldState.IDLE;
                totalDensityGenTime = Time.realtimeSinceStartup - totalDensityGenTime;
            }
        }
        else if(worldState == WorldState.MESH_UPDATE){
            if(ChunkManager.Update()){
                worldState = WorldState.IDLE;
                totalChunkGenTime = Time.realtimeSinceStartup - totalChunkGenTime;
            }
        }
        int3 lodChunkPos = (int3)(math.round(player.transform.position / baseChunkSize)) * baseChunkSize;
        worldUpdatePending = !lodChunkPos.Equals(worldOffset);
        if(worldState == WorldState.IDLE){
            int3 playerChunkPos = (int3)(math.round(player.transform.position / ChunkManager.chunkResolution)) * ChunkManager.chunkResolution;
            if(!playerChunkPos.Equals(playerOffset)){
                //activeParent.parent.position = (float3)closestOctetToPlayer;
                playerOffset = playerChunkPos;
                playerBounds.center = playerOffset;
                ChunkManager.shouldUpdateTree = true;
                DensityManager.LoadDensityData(playerOffset, 2);
            }
            else if(worldUpdatePending){
                worldOffset = lodChunkPos;
                int3 startPos = worldOffset - baseChunkSize * 2;
                List<int3> newPositions = new List<int3>();
                for(int x = 0; x < 4; x++){
                        for(int y = 0; y < 4; y++){
                            for(int z = 0; z < 4; z++){
                                var pos = new int3(x,y,z) * baseChunkSize + startPos;
                                newPositions.Add(pos);
                        }
                    }
                }
                ChunkManager.chunkTree.region.center = worldOffset;
                ChunkManager.chunkTree.RepositionOctets(newPositions, baseChunkSize);
            }
            if(!DensityManager.IsReady) { worldState = WorldState.DENSITY_UPDATE; totalDensityGenTime = Time.realtimeSinceStartup;}
            else if(!ChunkManager.IsReady) { worldState = WorldState.MESH_UPDATE; totalChunkGenTime = Time.realtimeSinceStartup;}
            else if(ChunkManager.shouldUpdateTree){
                ChunkManager.shouldUpdateTree = false;
                ChunkManager.chunkTree.UpdateTreeRecursive();
            }else if(!player.activeSelf) {
                player.SetActive(true);
            }
        }
    }
    public static void QueueModification(int3 pos, int value, int radius){
        for(int x = -radius; x < radius; x++){
            for(int y = -radius; y < radius; y++){
                for(int z = -radius; z < radius; z++){
                    float _value = math.clamp(math.clamp(radius - math.distance(new float3(x,y,z) + pos, pos), 0f, 127) * value, sbyte.MinValue, sbyte.MaxValue);
                    DensityManager.QueueModification(pos + new int3(x,y,z), Convert.ToSByte(_value));
                }
            }
        }
        ChunkManager.RegenerateChunkMesh(pos, radius);
    }
    public static void Test(){
        Debug.Log(-1 >> 24);
        //0b1000_0000_0000_0000_0000_0000_0000_0000
    }
    public static BiomeData GetBiomeData(int biomeIndex){
        return m_worldSettings.generator.GetBiomeData(biomeIndex);
    }
    public void OnDisable(){
        JobRunner.CompleteAll();
        DensityManager.Dispose();
        MemoryManager.Dispose();
        worldOffset = Int16.MaxValue;
        playerOffset = Int16.MaxValue;
    }
    #if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        if(!debugMode) return;
        
        if(drawPlayerBounds){
            Gizmos.DrawWireCube(playerBounds.center, playerBounds.bounds);
        }
        if(drawDensityMaps){
            var maps = DensityManager.GetDebugArray();
            foreach(var bound in maps){
                Gizmos.DrawWireCube(bound.center, bound.bounds);
            }
        }
        if(drawChunkVariables.draw ||drawChunkBounds){
            GUI.color = Color.green;
            var chunkDatas = ChunkManager.GetDebugArray();
            for(int i = 0; i < chunkDatas.Count; i++){
                float3 offset = chunkDatas[i].WorldPosition + ChunkManager.chunkResolution * chunkDatas[i].depthMultiplier / 2;
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
                    Handles.Label(offset, chunkDatas[i].idxCount.ToString());
                }
                if(drawChunkVariables.dirMask){
                    offset.y += 4f;
                    Handles.Label(offset, Utils.DirectionMaskToString(chunkDatas[i].dirMask));
                }
                if(drawChunkBounds){
                    Gizmos.DrawWireCube(chunkDatas[i].renderBounds.center, chunkDatas[i].renderBounds.size);
                }
                if(drawChunkVariables.type){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].GetType().Name);
                }
                if(drawChunkVariables.position){
                    offset.y += 4f;
                    Handles.Label(offset, chunkDatas[i].ToString());
                }
            }
        }
    }
#endif
}
