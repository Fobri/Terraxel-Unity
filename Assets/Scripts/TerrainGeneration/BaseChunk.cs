using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Terraxel;
using UnityEngine.Rendering;
using System;
using Terraxel.DataStructures;
using System.Collections.Generic;

public abstract class BaseChunk : Octree
{
    public const MeshUpdateFlags MESH_UPDATE_FLAGS = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds;
    protected bool active = true;
    protected static Mesh grassMesh;
    protected static Mesh treeMesh;
    protected static Mesh leafMesh;
    protected Unity.Mathematics.Random rng;
    static Transform cam;
    //public Matrix4x4[] _grassPositions;
    protected NativeReference<float3x2> renderBoundsData;
    public Bounds renderBounds;
    public ChunkState chunkState = ChunkState.INVALID;
    public OnMeshReadyAction onMeshReady = OnMeshReadyAction.ALERT_PARENT;
    public DisposeState disposeStatus = DisposeState.NOTHING;
    public float genTime;
    public int vertCount;
    public int idxCount;
    public bool hasMesh;
    public byte dirMask;
    public InstancedRenderer grassRenderer;
    public InstancedRenderer treeRenderer;
    public InstancedRenderer leafRenderer;
    public abstract bool CanBeCreated{get;}
    protected MaterialPropertyBlock propertyBlock;
    
    //public NativeList<Matrix4x4> grassPositions;
    protected SubMeshDescriptor desc = new SubMeshDescriptor();
    public BaseChunk(BoundingBox bounds, int depth)
    : base(bounds, depth){
        desc.topology = MeshTopology.Triangles;
        chunkState = ChunkState.INVALID;
        disposeStatus = DisposeState.NOTHING;
        grassRenderer = new InstancedRenderer((Resources.Load("Materials/GrassMaterial", typeof(Material)) as Material), grassMesh, ShadowCastingMode.Off);
        treeRenderer = new InstancedRenderer((Resources.Load("Materials/Bark_Pine", typeof(Material)) as Material), treeMesh, ShadowCastingMode.On);
        leafRenderer = new InstancedRenderer((Resources.Load("Materials/Leaves_Pine", typeof(Material)) as Material), leafMesh, ShadowCastingMode.On);
        propertyBlock = new MaterialPropertyBlock();
        rng = new Unity.Mathematics.Random((uint)TerraxelWorld.seed);
    }
    public BaseChunk() : base(){

    }
    static BaseChunk(){
        grassMesh = Resources.Load("Meshes/GrassMesh", typeof(Mesh)) as Mesh;
        treeMesh = Resources.Load("Meshes/PineTrunk", typeof(Mesh)) as Mesh;
        leafMesh = Resources.Load("Meshes/PineLeaves", typeof(Mesh)) as Mesh;
        cam = Camera.main.transform;
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
    protected void RenderInstances(){
        if(!TerraxelWorld.renderGrass) return;
        grassRenderer.Render();
        treeRenderer.Render();
        leafRenderer.Render();
    }
    internal override void OnJobsReady()
    {
        if(renderBoundsData.IsCreated){
            renderBounds = new Bounds((renderBoundsData.Value.c1 - renderBoundsData.Value.c0) * 0.5f + WorldPosition, renderBoundsData.Value.c1 - renderBoundsData.Value.c0);
            renderBoundsData.Dispose();
        }else{
            renderBounds = new Bounds(region.center, region.bounds);
        }
        ApplyMesh();
        PushInstanceData();
    }
    protected void PushInstanceData(){
        grassRenderer.PushData();
        leafRenderer.PushData(treeRenderer.data);
        treeRenderer.PushData();
    }
    public void ScheduleMeshUpdate(){
        propertyBlock.SetVector("_WorldPos", new float4(WorldPosition, 1));
        vertCount = 0;
        idxCount = 0;
        chunkState = ChunkState.DIRTY;
        genTime = Time.realtimeSinceStartup;
        grassRenderer.Dispose();
        treeRenderer.Dispose();
        grassRenderer.AllocateData();
        treeRenderer.AllocateData();
        //leafRenderer.AllocateData();
        renderBoundsData = new NativeReference<float3x2>(Allocator.TempJob);
        OnScheduleMeshUpdate();
        
    }
    protected abstract void OnScheduleMeshUpdate();
    public abstract void RenderChunk();
    public abstract void ApplyMesh();
    protected abstract void OnFreeBuffers();
    public void FreeBuffers(){
        grassRenderer?.Dispose();
        treeRenderer?.Dispose();
        leafRenderer?.Dispose();
        OnFreeBuffers();
    }
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
    public override string ToString()
    {
        return "Chunk " + WorldPosition.ToString();
    }
}
