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

public class TerraxelWorld : MonoBehaviour
{
    public enum WorldState { DENSITY_UPDATE, MESH_UPDATE, IDLE }
    [ShowInInspector]
    public static WorldState worldState = WorldState.IDLE;
    public static Camera renderCamera;
    public GameObject player;
    public static BoundingBox playerBounds;
    public static int seed = 1;
    public NoiseProperties noiseData;
    public Transform poolParent;
    public Transform activeParent;
    
    public GameObject simpleChunkPrefab;
    public GameObject chunkPrefab;
    int baseChunkSize = ChunkManager.chunkResolution * Octree.depthMultipliers[ChunkManager.lodLevels - 2];
    public int3 worldOffset = Int16.MaxValue;
    
    public int3 playerOffset = Int16.MaxValue;
    public static DensityManager DensityManager {get; private set;}
    public static ChunkManager ChunkManager {get; private set;}
    public static bool worldUpdatePending {get; private set;}
    public bool frustumCulling = true;

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
        renderCamera = Camera.main;
        MemoryManager.Init();
        playerBounds = new BoundingBox(player.transform.position, new float3(ChunkManager.chunkResolution));
        DensityManager = new DensityManager();
        DensityManager.Init(noiseData);
        ChunkManager = new ChunkManager();
        ChunkManager.Init(poolParent, activeParent, simpleChunkPrefab, chunkPrefab);
    }
    void Update(){
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
            debugLabels[4].text = $"Total chunk generation time: {totalGenTime}";
            debugLabels[5].text = $"Vertex count: {vertCount}";
            debugLabels[6].text = $"Index count: {indexCount}";
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
        ChunkManager.RenderChunks(planes, frustumCulling);
        if(worldState == WorldState.DENSITY_UPDATE){
            if(DensityManager.Update()){
                worldState = WorldState.IDLE;
            }
        }
        else if(worldState == WorldState.MESH_UPDATE){
            if(ChunkManager.Update()){
                worldState = WorldState.IDLE;
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
            if(!DensityManager.IsReady) worldState = WorldState.DENSITY_UPDATE;
            else if(!ChunkManager.IsReady) worldState = WorldState.MESH_UPDATE;
            else if(ChunkManager.shouldUpdateTree){
                ChunkManager.shouldUpdateTree = false;
                ChunkManager.chunkTree.UpdateTreeRecursive();
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
    public void OnDisable(){
        JobRunner.CompleteAll();
        MemoryManager.Dispose();
        DensityManager.Dispose();
    }
    #if UNITY_EDITOR
    private void OnDrawGizmos()
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
                if(drawChunkVariables.maxVertexCount){
                    offset.y += 4f;
                    Handles.Label(offset, MemoryManager.maxVertexCount.ToString());
                }
                if(drawChunkVariables.dirMask){
                    offset.y += 4f;
                    Handles.Label(offset, Utils.DirectionMaskToString(chunkDatas[i].dirMask));
                }
                if(drawChunkBounds){
                    chunkDatas[i].region.DebugDraw();
                }
            }
        }
    }
#endif
}