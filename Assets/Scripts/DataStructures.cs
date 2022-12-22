using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;

namespace DataStructures
{
    public enum ChunkState { DIRTY, READY, INVALID, ROOT }
    public enum OnMeshReady { ALERT_PARENT, DISPOSE_CHILDREN }
    public enum DisposeStatus { NOTHING, POOL, FREE_MESH }
    public class ChunkData : Octree{
        
        public NativeArray<float> densityMap;
        public NativeArray<ushort4> vertexIndexBuffer;
        public NativeArray<Vector3> vertices;
        public NativeArray<ushort> indices;
        public JobHandle meshJobHandle;
        public GameObject worldObject;
        public Counter vertexCounter;
        public Counter indexCounter;
        public ChunkState chunkState = ChunkState.INVALID;
        public OnMeshReady onMeshReady = OnMeshReady.ALERT_PARENT;
        public DisposeStatus disposeStatus = DisposeStatus.NOTHING;
#if UNITY_EDITOR
        public float genTime;
#endif
        public int vertCount;
        public int indexCount;
        SubMeshDescriptor desc = new SubMeshDescriptor();
        public ChunkData(GameObject worldObject, BoundingBox bounds, int depth) 
        : base(bounds, depth){
            this.worldObject = worldObject;
            meshJobHandle = default;
            desc.topology = MeshTopology.Triangles;
            chunkState = ChunkState.INVALID;
            disposeStatus = DisposeStatus.NOTHING;
        }
        //ROOT chunk
        public ChunkData() : base() {
        }
        public void ApplyMesh(){
            chunkState = ChunkState.READY;
            var mesh = worldObject.GetComponent<MeshFilter>().sharedMesh;
            var layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                };
            var vertexCount = vertexCounter.Count;
            var indexCount = indexCounter.Count * 3;
            if (vertexCount > 0)
            {
                
                //var size = ChunkManager.chunkResolution * math.pow(2, depth) / 2;
                mesh.bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
                //Set vertices and indices
                mesh.SetVertexBufferParams(vertexCount, layout);
                mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(indices, 0, 0, indexCount, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                desc.indexCount = indexCount;
                mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                mesh.RecalculateNormals();
                //filter.sharedMesh = myMesh;
                this.vertCount = vertexCount;
                this.indexCount = indexCount;
            }else{
                vertCount = 0;
                indexCount = 0;
                FreeChunkMesh();
            }
            indexCounter.Dispose();
            vertexCounter.Dispose();
#if UNITY_EDITOR
            genTime = Time.realtimeSinceStartup - genTime;
#endif
            ChunkManager.memoryManager.ReturnDensityMap(densityMap);
            ChunkManager.memoryManager.ReturnVertexIndexBuffer(vertexIndexBuffer);
            densityMap = default;
            vertexIndexBuffer = default;
            if(onMeshReady == OnMeshReady.ALERT_PARENT){
                base.NotifyParentMeshReady();
            }else if(onMeshReady == OnMeshReady.DISPOSE_CHILDREN){
                onMeshReady = OnMeshReady.ALERT_PARENT;
                PruneChunksRecursive();
            }
        }
        public void FreeChunkMesh(){
            disposeStatus = DisposeStatus.FREE_MESH;
        }
        public void PoolChunk(){
            disposeStatus = DisposeStatus.POOL;
        }
        public bool HasMesh(){
            return worldObject != null;
        }
        public float3 WorldPosition{
            get{
                return (float3)region.center - new float3(ChunkManager.chunkResolution * depthMultiplier / 2);
            }
        }
    }
}