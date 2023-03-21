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
    protected Material grassMaterial;
    protected Unity.Mathematics.Random rng;
    static Transform cam;
    //public Matrix4x4[] _grassPositions;
    protected NativeList<InstanceData> grassData;
    protected NativeReference<float3x2> renderBoundsData;
    public Bounds renderBounds;
    private ComputeBuffer grassBuffer;
    RenderParams rp;
    
    public ChunkState chunkState = ChunkState.INVALID;
    public OnMeshReadyAction onMeshReady = OnMeshReadyAction.ALERT_PARENT;
    public DisposeState disposeStatus = DisposeState.NOTHING;
    public float genTime;
    public int vertCount;
    public int idxCount;
    public bool hasMesh;
    public byte dirMask;
    public abstract bool CanBeCreated{get;}
    
    //public NativeList<Matrix4x4> grassPositions;
    protected SubMeshDescriptor desc = new SubMeshDescriptor();
    protected MaterialPropertyBlock propertyBlock;
    public BaseChunk(BoundingBox bounds, int depth)
    : base(bounds, depth){
        desc.topology = MeshTopology.Triangles;
        chunkState = ChunkState.INVALID;
        disposeStatus = DisposeState.NOTHING;
        propertyBlock = new MaterialPropertyBlock();
        grassMaterial = (Resources.Load("Materials/GrassMaterial", typeof(Material)) as Material);
        rp = new RenderParams(grassMaterial);
        rp.matProps = propertyBlock;
        rp.shadowCastingMode = ShadowCastingMode.On;
        rp.receiveShadows = true;
        rng = new Unity.Mathematics.Random((uint)TerraxelWorld.seed);
    }
    public BaseChunk() : base(){

    }
    static BaseChunk(){
        grassMesh = Resources.Load("Meshes/GrassMesh", typeof(Mesh)) as Mesh;
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
    protected void RenderGrass(){
        if(!TerraxelWorld.renderGrass || chunkState != ChunkState.READY) return;
        if(grassData.IsCreated){
            if(grassData.Length == 0) return;
            Graphics.RenderMeshPrimitives(rp, grassMesh, 0, grassData.Length);
        }
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
        PushGrassData();
    }
    protected void PushGrassData(){
        if(!grassData.IsCreated || grassData.Length == 0) return;
        grassBuffer = new ComputeBuffer(grassData.Length, sizeof(float) * 16);
        grassBuffer.SetData(grassData.AsArray());
        //grassMaterial.SetBuffer("positionBuffer", grassBuffer);
        rp.worldBounds = renderBounds;
        rp.matProps.SetBuffer("Matrices", grassBuffer);
    }
    public void ScheduleMeshUpdate(){
        //propertyBlock.SetVector("_WorldPos", new float4(WorldPosition, 1));
        vertCount = 0;
        idxCount = 0;
        chunkState = ChunkState.DIRTY;
        genTime = Time.realtimeSinceStartup;
        grassData = MemoryManager.GetGrassData();
        renderBoundsData = new NativeReference<float3x2>(Allocator.TempJob);
        OnScheduleMeshUpdate();
        
    }
    protected abstract void OnScheduleMeshUpdate();
    public abstract void RenderChunk();
    public abstract void ApplyMesh();
    protected abstract void OnFreeBuffers();
    public void FreeBuffers(){
        grassBuffer?.Release();
        grassBuffer = null;
        rp.matProps?.Clear();
        if(grassData.IsCreated){
            MemoryManager.ReturnGrassData(grassData);
            grassData = default;
        }
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
