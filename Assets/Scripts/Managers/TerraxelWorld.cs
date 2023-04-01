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

[CreateAssetMenu(fileName = "World Settings", menuName = "Terraxel/World Settings", order = 0), System.Serializable]
public class TerraxelWorld : ScriptableObject, IDisposable
{
    public enum WorldState { DENSITY_UPDATE, MESH_UPDATE, IDLE }
    public bool placePlayerOnSurface = true;
    public GameObject debugCanvas;
    public int3 worldOffset = Int16.MaxValue;
    public int3 playerOffset = Int16.MaxValue;
    public int m_seed;
    
    ComputeShader noiseShader;
    GameObject chunkPrefab;
    GameObject player;
    int baseChunkSize = ChunkManager.chunkResolution * Octree.depthMultipliers[ChunkManager.lodLevels - 2];
    
    //STATIC VARS
    public static bool renderGrass = true;
    public static DensityManager DensityManager {get; private set;}
    public static ChunkManager ChunkManager {get; private set;}
    public static bool worldUpdatePending {get; private set;}
    public static bool frustumCulling = true;
    public static BoundingBox playerBounds;
    [ShowInInspector]
    public static WorldState worldState = WorldState.IDLE;
    public static Camera renderCamera;
    public static int seed;

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
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly, InfoBox("Temporary buffers available. Should be @MemoryManager.densityMapCount")]
#endif
    public int freeDensityMaps;
#if ODIN_INSPECTOR    
    [BoxGroup("DEBUG"), HideIf("@!debugMode"), ReadOnly]
#endif
    public int memoryUsed;
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
    public void Init(Transform poolParent, Transform activeParent, GameObject player){
#if !UNITY_EDITOR
        frustumCulling = true;
#endif
        if(m_seed == 0){
            seed = UnityEngine.Random.Range(Int16.MinValue, Int16.MaxValue);
        }else seed = m_seed;
        if(debugMode){
            debugCanvas = Resources.Load<GameObject>("Prefabs/TerraxelDebug");
            var canv = Instantiate(debugCanvas);
            debugLabels = new TextMeshProUGUI[canv.transform.childCount];
            for(int i = 0; i < debugLabels.Length; i++){
                debugLabels[i] = canv.transform.GetChild(i).GetComponent<TextMeshProUGUI>();
            }
        }
        chunkPrefab = Resources.Load<GameObject>("Prefabs/Chunk");
        noiseShader = Resources.Load<ComputeShader>("Generated/TerraxelGenerated");
        renderCamera = Camera.main;
        MemoryManager.Init();
        playerBounds = new BoundingBox(player.transform.position, new float3(ChunkManager.chunkResolution));
        DensityManager = new DensityManager();
        DensityManager.Init(noiseShader);
        ChunkManager = new ChunkManager();
        ChunkManager.Init(poolParent, activeParent, chunkPrefab);
        this.player = player;
        player.SetActive(false);
        if(placePlayerOnSurface){
            var startHeight = TerraxelGenerated.GenerateDensity(new float2(player.transform.position.x, player.transform.position.z), seed) + 0.1f;
            player.transform.position = player.transform.position * new float3(1,0,1) + new float3(0,startHeight, 0);
        }
    }
    public void Run(){
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
            debugLabels[2].text = $"Chunk count: {chunkCount}/{MemoryManager.maxBufferCount}";
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
    public void Dispose(){
        JobRunner.CompleteAll();
        DensityManager.Dispose();
        MemoryManager.Dispose();
        worldOffset = Int16.MaxValue;
        playerOffset = Int16.MaxValue;
    }
    #if UNITY_EDITOR
    public void DebugDraw()
    {
        if(!debugMode) return;
        
        if(drawPlayerBounds){
            Gizmos.DrawWireCube(playerBounds.center, playerBounds.bounds);
        }
        if(drawChunkBounds){
            Gizmos.DrawWireCube((float3)worldOffset, new float3(ChunkManager.chunkResolution * math.pow(2, ChunkManager.lodLevels)));
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