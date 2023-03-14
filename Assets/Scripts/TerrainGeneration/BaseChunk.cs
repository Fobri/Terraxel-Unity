using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;
using WorldGeneration.DataStructures;
using System.Collections.Generic;

public abstract class BaseChunk : Octree
{
    public const MeshUpdateFlags MESH_UPDATE_FLAGS = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds;
    protected bool active = true;
    protected static Mesh grassMesh;
    protected static Material grassMaterial;
    public Matrix4x4[] _grassPositions;
    
    public ChunkState chunkState = ChunkState.INVALID;
    public OnMeshReadyAction onMeshReady = OnMeshReadyAction.ALERT_PARENT;
    public DisposeState disposeStatus = DisposeState.NOTHING;
    public float genTime;
    public int vertCount;
    public int idxCount;
    public bool hasMesh;
    public byte dirMask;
    public abstract bool CanBeCreated{get;}
    
    public NativeList<Matrix4x4> grassPositions;
    protected SubMeshDescriptor desc = new SubMeshDescriptor();
    protected MaterialPropertyBlock propertyBlock;
    public BaseChunk(BoundingBox bounds, int depth)
    : base(bounds, depth){
        desc.topology = MeshTopology.Triangles;
        chunkState = ChunkState.INVALID;
        disposeStatus = DisposeState.NOTHING;
        propertyBlock = new MaterialPropertyBlock();
    }
    public BaseChunk() : base(){

    }
    static BaseChunk(){
        grassMesh = Resources.Load("GrassMesh", typeof(Mesh)) as Mesh;
        grassMaterial = Resources.Load("GrassMaterial", typeof(Material)) as Material;
    }
    public void SetActive(bool active){
        this.active = active;
    }
    public void FreeChunkMesh(){
        disposeStatus = DisposeState.FREE_MESH;
        TerraxelWorld.ChunkManager.DisposeChunk(this);
    }
    public void PoolChunk(){
        disposeStatus = DisposeState.POOL;
        TerraxelWorld.ChunkManager.DisposeChunk(this);
    }
    protected void RenderGrass(){
        if(_grassPositions != null){
            Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, _grassPositions, grassPositions.Length, null, ShadowCastingMode.On, false, 0, null, LightProbeUsage.Off, null);
        }
    }
    internal override void OnJobsReady()
    {
        ApplyMesh();
    }
    public void ScheduleMeshUpdate(){
        propertyBlock.SetVector("_WorldPos", new float4(WorldPosition, 1));
        vertCount = 0;
        idxCount = 0;
        chunkState = ChunkState.DIRTY;
        genTime = Time.realtimeSinceStartup;
        OnScheduleMeshUpdate();
    }
    protected abstract void OnScheduleMeshUpdate();
    public abstract void RenderChunk();
    public abstract void ApplyMesh();
    public abstract void FreeBuffers();
    public virtual void OnMeshReady(){
        genTime = Time.realtimeSinceStartup - genTime;
        chunkState = ChunkState.READY;
        if(onMeshReady == OnMeshReadyAction.ALERT_PARENT){
            base.NotifyParentMeshReady();
        }else if(onMeshReady == OnMeshReadyAction.DISPOSE_CHILDREN){
            onMeshReady = OnMeshReadyAction.ALERT_PARENT;
            PruneChunksRecursive();
        }
    }
    public virtual void RefreshRenderState(bool refreshNeighbours = false){}
    
    public float3 WorldPosition{
        get{
            return (float3)region.center - new float3(ChunkManager.chunkResolution * depthMultiplier / 2);
        }
    }
}
