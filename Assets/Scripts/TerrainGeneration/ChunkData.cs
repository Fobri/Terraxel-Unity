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
        public void UpdateMesh(){
            //Check front
            dirMask = 0;
            NeighbourDensities[] neighbourDensities = new NeighbourDensities[6];
            NeighbourDensities densities;
            if(CheckNeighbour(new int3(1, 0, 0), out densities)){
                dirMask |= 0b_0000_0001;
                neighbourDensities[0] = densities;
            }
            if(CheckNeighbour(new int3(-1, 0, 0), out densities)){
                dirMask |= 0b_0000_0010;
                neighbourDensities[1] = densities;
            }
            if(CheckNeighbour(new int3(0, 1, 0), out densities)){
                dirMask |= 0b_0000_0100;
                neighbourDensities[2] = densities;
            }
            if(CheckNeighbour(new int3(0, -1, 0), out densities)){
                dirMask |= 0b_0000_1000;
                neighbourDensities[3] = densities;
            }
            if(CheckNeighbour(new int3(0, 0, 1), out densities)){
                dirMask |= 0b_0001_0000;
                neighbourDensities[4] = densities;
            }
            if(CheckNeighbour(new int3(0, 0, -1), out densities)){
                dirMask |= 0b_0010_0000;
                neighbourDensities[5] = densities;
            }
            var marchingJob = new MarchingJob()
            {
                densities = meshData.densityBuffer,
                isolevel = 0f,
                chunkSize = ChunkManager.chunkResolution + 1,
                vertices = meshData.vertexBuffer,
                vertexCounter = vertexCounter,
                depthMultiplier = depthMultiplier,
                vertexIndices = vertexIndexBuffer,
                front = neighbourDensities[0],
                back = neighbourDensities[1],
                up = neighbourDensities[2],
                down = neighbourDensities[3],
                right = neighbourDensities[4],
                left = neighbourDensities[5]
                
            };
            var marchingHandle = marchingJob.Schedule((ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1), 32);

            var vertexSharingJob = new VertexSharingJob()
            {
                triangles = meshData.indexBuffer,
                chunkSize = ChunkManager.chunkResolution + 1,
                counter = indexCounter,
                vertexIndices = vertexIndexBuffer
            };
            jobHandle = vertexSharingJob.Schedule((ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1) * (ChunkManager.chunkResolution + 1), 32, marchingHandle);
        }
        bool CheckNeighbour(int3 relativeOffset, out NeighbourDensities densities){
            float3 pos = ChunkManager.chunkResolution * depthMultiplier * relativeOffset;
            var tree = ChunkManager.chunkTree as Octree;
            var queryResult = tree.Query(new BoundingBox(this.region.center + pos, this.region.bounds));
            if(queryResult != null){
                if(queryResult.HasSubChunks){
                    NeighbourDensities _densities = new NeighbourDensities();
                    var quadrants = Utils.ChunkRelativePositionToQuadrantLocations[relativeOffset];
                    for(int i = 0; i < 4; i++){
                        _densities[i] = queryResult.GetByLocation(quadrants[i]).meshData.densityBuffer;
                    }
                    densities = _densities;
                    return true;
                }
                densities = default(NeighbourDensities);
                return false;
            }
            densities = default(NeighbourDensities);
            return false;
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