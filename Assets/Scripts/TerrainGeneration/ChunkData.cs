using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;
using DataStructures;

public class ChunkData : Octree{
        public enum ChunkState { DIRTY, READY, INVALID, ROOT, QUEUED }
        public enum GenerationState { DENSITY, MESH }
        public enum OnMeshReady { ALERT_PARENT, DISPOSE_CHILDREN }
        public enum DisposeState { NOTHING, POOL, FREE_MESH }
        public NativeArray<ushort4> vertexIndexBuffer;
        public MeshData meshData;
        public JobHandle jobHandle;
        public GameObject worldObject;
        public Counter vertexCounter;
        public Counter indexCounter;
        public ChunkState chunkState = ChunkState.INVALID;
        public OnMeshReady onMeshReady = OnMeshReady.ALERT_PARENT;
        public DisposeState disposeStatus = DisposeState.NOTHING;
        public GenerationState generationState;
        public float genTime;
        public int vertCount;
        public int indexCount;
        public bool hasMesh;
        SubMeshDescriptor desc = new SubMeshDescriptor();
        static VertexAttributeDescriptor[] layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
                };
        public ChunkData(GameObject worldObject, BoundingBox bounds, int depth) 
        : base(bounds, depth){
            this.worldObject = worldObject;
            jobHandle = default;
            desc.topology = MeshTopology.Triangles;
            chunkState = ChunkState.INVALID;
            disposeStatus = DisposeState.NOTHING;
            generationState = GenerationState.DENSITY;
        }
        //ROOT chunk
        public ChunkData() : base() {
        }
        void InitWorldObject(){
            worldObject = ChunkManager.GetChunkObject();
            worldObject.name = $"Chunk {WorldPosition.x}, {WorldPosition.y}, {WorldPosition.z}";
            worldObject.transform.position = WorldPosition;
        }
        public void ApplyMesh(){
            chunkState = ChunkState.READY;
            
            var vertexCount = vertexCounter.Count;
            var indexCount = indexCounter.Count * 3;
            if (vertexCount > 0)
            {
                InitWorldObject();
                var mesh = worldObject.GetComponent<MeshFilter>().sharedMesh;
                mesh.bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
                //Set vertices and indices
                mesh.SetVertexBufferParams(vertexCount, layout);
                mesh.SetVertexBufferData(meshData.vertexBuffer, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(meshData.indexBuffer, 0, 0, indexCount, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                desc.indexCount = indexCount;
                mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                //mesh.RecalculateNormals();
                this.vertCount = vertexCount;
                this.indexCount = indexCount;
            }else{
                vertCount = 0;
                indexCount = 0;
                FreeChunkMesh();
            }
            indexCounter.Dispose();
            vertexCounter.Dispose();
            genTime = Time.realtimeSinceStartup - genTime;
            ChunkManager.memoryManager.ReturnVertexIndexBuffer(vertexIndexBuffer);
            vertexIndexBuffer = default;
            if(onMeshReady == OnMeshReady.ALERT_PARENT){
                base.NotifyParentMeshReady();
            }else if(onMeshReady == OnMeshReady.DISPOSE_CHILDREN){
                onMeshReady = OnMeshReady.ALERT_PARENT;
                PruneChunksRecursive();
            }
        }
        public void FreeChunkMesh(){
            disposeStatus = DisposeState.FREE_MESH;
        }
        public void PoolChunk(){
            disposeStatus = DisposeState.POOL;
        }
        /*public bool HasMesh(){
            return worldObject != null;
        }*/
        public float3 WorldPosition{
            get{
                return (float3)region.center - new float3(ChunkManager.chunkResolution * depthMultiplier / 2);
            }
        }
    }