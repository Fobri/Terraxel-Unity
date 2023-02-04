using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;
using WorldGeneration.DataStructures;

public class ChunkData : Octree{
        public enum ChunkState { DIRTY, READY, INVALID, ROOT, QUEUED }
        public enum OnMeshReady { ALERT_PARENT, DISPOSE_CHILDREN }
        public enum DisposeState { NOTHING, POOL, FREE_MESH }
        public TempBuffer vertexIndexBuffer;
        public MeshData meshData;
        public GameObject worldObject;
        public Counter vertexCounter;
        public Counter indexCounter;
        public ChunkState chunkState = ChunkState.INVALID;
        public OnMeshReady onMeshReady = OnMeshReady.ALERT_PARENT;
        public DisposeState disposeStatus = DisposeState.NOTHING;
        public float genTime;
        public int vertCount;
        public int indexCount;
        public bool hasMesh;
        public byte dirMask;
        SubMeshDescriptor desc = new SubMeshDescriptor();
        static VertexAttributeDescriptor[] layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
                };
        public ChunkData(GameObject worldObject, BoundingBox bounds, int depth) 
        : base(bounds, depth){
            this.worldObject = worldObject;
            desc.topology = MeshTopology.Triangles;
            chunkState = ChunkState.INVALID;
            disposeStatus = DisposeState.NOTHING;
        }
        //ROOT chunk
        public ChunkData() : base() {
        }
        void InitWorldObject(){
            worldObject = ChunkManager.GetChunkObject();
            worldObject.name = $"Chunk {WorldPosition.x}, {WorldPosition.y}, {WorldPosition.z}";
            worldObject.transform.position = WorldPosition;
        }
        internal override void OnJobsReady(){
            ApplyMesh();
        }
        public void UpdateMesh(){
            //Check front
            dirMask = 0;
            if(depth > 0){
                if(CheckNeighbour(new int3(1, 0, 0))){
                    dirMask |= 0b_0000_0001;
                }
                if(CheckNeighbour(new int3(-1, 0, 0))){
                    dirMask |= 0b_0000_0010;
                }
                if(CheckNeighbour(new int3(0, 1, 0))){
                    dirMask |= 0b_0000_0100;
                }
                if(CheckNeighbour(new int3(0, -1, 0))){
                    dirMask |= 0b_0000_1000;
                }
                if(CheckNeighbour(new int3(0, 0, -1))){
                    dirMask |= 0b_0001_0000;
                }
                if(CheckNeighbour(new int3(0, 0, 1))){
                    dirMask |= 0b_0010_0000;
                }
            }
            
            genTime = Time.realtimeSinceStartup;
            var marchingJob = new MeshJob()
            {
                densities = ChunkManager.densityManager.GetJobDensityData(),
                chunkPos = (int3)WorldPosition,
                isolevel = 0f,
                chunkSize = ChunkManager.chunkResolution,
                vertices = meshData.vertexBuffer,
                vertexCounter = vertexCounter,
                indexCounter = indexCounter,
                depthMultiplier = depthMultiplier,
                negativeDepthMultiplier = negativeDepthMultiplier,
                vertexIndices = vertexIndexBuffer,
                triangles = meshData.indexBuffer,
                neighbourDirectionMask = dirMask,
                cache = new DensityCacheInstance(new int3(int.MaxValue))
            };
            base.ScheduleJob(marchingJob, (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution));

            /*var vertexSharingJob = new VertexSharingJob()
            {
                triangles = meshData.indexBuffer,
                chunkSize = ChunkManager.chunkResolution + 1,
                counter = indexCounter,
                vertexIndices = vertexIndexBuffer,
                neighbourDirectionMask = dirMask
            };
            jobHandle = vertexSharingJob.Schedule((ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1), 32, marchingHandle);
        */}
        bool CheckNeighbour(int3 relativeOffset){
            float3 pos = ChunkManager.chunkResolution * depthMultiplier * relativeOffset;
            var tree = ChunkManager.chunkTree as Octree;
            var queryResult = tree.Query(new BoundingBox(this.region.center + pos, this.region.bounds - 4));
            if(queryResult != null){
                if(queryResult.HasSubChunks){
                    return true;
                }else if(queryResult.depth == this.depth){
                    return false;
                }
                return false;
            }
            return false;
        }
        public void ApplyMesh(){
            chunkState = ChunkState.READY;
            
            var vertexCount = vertexCounter.Count;
            var indexCount = indexCounter.Count * 3;
            if (vertexCount > 0)
            {
                if(worldObject == null)
                    InitWorldObject();
                var mesh = worldObject.GetComponent<MeshFilter>().sharedMesh;
                mesh.bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
                //Set vertices and indices
                mesh.SetVertexBufferParams(vertexCount, layout);
                mesh.SetVertexBufferData(meshData.vertexBuffer, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(meshData.indexBuffer, 0, 0, indexCount,  MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

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
            ChunkManager.DisposeChunk(this);
        }
        public void PoolChunk(){
            disposeStatus = DisposeState.POOL;
            ChunkManager.DisposeChunk(this);
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