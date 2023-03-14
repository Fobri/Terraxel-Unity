using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;
using WorldGeneration.DataStructures;
using System.Collections.Generic;
public class Chunk2D : BaseChunk
{
    
    private Mesh chunkMesh;
    public SimpleMeshData meshData;
    static Material chunkMaterial;
    const int vertexCount = 1098;
    const int indexCount = 6144;
    Matrix4x4 localMatrix;
    VertexAttributeDescriptor[] layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
            };
    public override bool CanBeCreated{
        get{
            return IsReady;
        }
    }
    public Chunk2D(BoundingBox bounds, int depth)
    : base(bounds, depth){
        chunkMesh = new Mesh();
        desc.topology = MeshTopology.Triangles;
        chunkState = ChunkState.INVALID;
        disposeStatus = DisposeState.NOTHING;
    }
    static Chunk2D(){
            chunkMaterial = Resources.Load("TerrainSimple", typeof(Material)) as Material;
    }

    protected override void OnScheduleMeshUpdate()
    {
        if(WorldPosition.y != 0){
            OnMeshReady();
            return;
        }
        meshData = MemoryManager.GetSimpleMeshData();
        var pos = (int3)WorldPosition;
        var noiseJob = new NoiseJob2D{
            offset = new float2(pos.x, pos.z),
            depthMultiplier = depthMultiplier,
            size = ChunkManager.chunkResolution + 1,
            heightMap = meshData.heightMap,
            noiseProperties = TerraxelWorld.DensityManager.GetNoiseProperties()
        };
        base.ScheduleParallelForJob(noiseJob, 1089);
        var meshJob = new Mesh2DJob(){
            heightMap = meshData.heightMap,
            chunkSize = ChunkManager.chunkResolution + 1,
            vertices = meshData.buffer,
            chunkPos = new int2(pos.x, pos.z),
            surfaceLevel = 0
        };
        base.ScheduleParallelForJob(meshJob, 1089, true);
    }
    public override void ApplyMesh()
    {
        localMatrix = Matrix4x4.TRS(WorldPosition, Quaternion.identity, new float3(depthMultiplier, 1, depthMultiplier));
        //propertyBlock.SetInt("_DepthMultiplier", depthMultiplier);
        chunkMesh.SetVertexBufferParams(vertexCount, layout);
        chunkMesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
        chunkMesh.SetVertexBufferData(meshData.buffer, 0, 0, 1089,0, MESH_UPDATE_FLAGS);
        chunkMesh.SetIndexBufferData(SimpleMeshData.indices, 0, 0, 6144);
        desc.indexCount = 6144;
        chunkMesh.SetSubMesh(0, desc, MESH_UPDATE_FLAGS);
        var bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
        chunkMesh.bounds = bounds;
        chunkMesh.RecalculateNormals();
        OnMeshReady();
    }
    public override void RenderChunk()
    {
        if(!active) return;
        Graphics.DrawMesh(chunkMesh, localMatrix, chunkMaterial, 0, null, 0, propertyBlock, true, true, true);
        base.RenderGrass();
    }
    public override void FreeBuffers()
    {
        chunkMesh.Clear();
        if(meshData != null && meshData.IsCreated)
            MemoryManager.ReturnSimpleMeshData(meshData);
        meshData = null;
    }
}
