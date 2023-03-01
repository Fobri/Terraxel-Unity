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
        public enum OnMeshReadyAction { ALERT_PARENT, DISPOSE_CHILDREN }
        public enum DisposeState { NOTHING, POOL, FREE_MESH }
        public TempBuffer vertexIndexBuffer;
        public MeshData meshData;
        private NativeArray<int2> meshStarts;
        public GameObject worldObject;
        public SimpleMeshData simpleMesh;
        //public Counter vertexCounter;
        //public Counter indexCounter;
        public ChunkState chunkState = ChunkState.INVALID;
        public OnMeshReadyAction onMeshReady = OnMeshReadyAction.ALERT_PARENT;
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
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.SInt32, 1)
                };
        public ChunkData(GameObject worldObject, BoundingBox bounds, int depth) 
        : base(bounds, depth){
            this.worldObject = worldObject;
            desc.topology = MeshTopology.Triangles;
            chunkState = ChunkState.INVALID;
            disposeStatus = DisposeState.NOTHING;
            meshStarts = MemoryManager.GetMeshCounterArray();
        }
        //ROOT chunk
        public ChunkData() : base() {
        }
        void InitWorldObject(){
            worldObject = TerraxelWorld.ChunkManager.GetChunkObject();
            worldObject.name = $"Chunk {WorldPosition.x}, {WorldPosition.y}, {WorldPosition.z}";
            worldObject.transform.position = WorldPosition;
            if(onMeshReady == OnMeshReadyAction.ALERT_PARENT)
                worldObject.SetActive(false);
        }
        internal override void OnJobsReady(){
            ApplyMesh();
        }
        public void UpdateDirectionMask(bool refreshNeighbours = false){
            dirMask = 0;
            if(CheckNeighbour(new int3(1, 0, 0), refreshNeighbours)){
                dirMask |= 0b_0000_0001;
            }
            if(CheckNeighbour(new int3(-1, 0, 0), refreshNeighbours)){
                dirMask |= 0b_0000_0010;
            }
            if(CheckNeighbour(new int3(0, 1, 0), refreshNeighbours)){
                dirMask |= 0b_0000_0100;
            }
            if(CheckNeighbour(new int3(0, -1, 0), refreshNeighbours)){
                dirMask |= 0b_0000_1000;
            }
            if(CheckNeighbour(new int3(0, 0, 1), refreshNeighbours)){
                dirMask |= 0b_0001_0000;
            }
            if(CheckNeighbour(new int3(0, 0, -1), refreshNeighbours)){
                dirMask |= 0b_0010_0000;
            }
            if(depth == 0) dirMask = 0;
        }
        public void RefreshRenderState(bool refreshNeighbours = false){
            UpdateDirectionMask(refreshNeighbours);
            if(worldObject == null) return;
            worldObject.GetComponent<MeshRenderer>().material.SetInt("_DirectionMask", dirMask);
            for(int i = 0; i < 6; i++){
                worldObject.transform.GetChild(i).GetComponent<MeshRenderer>().material.SetInt("_DirectionMask", dirMask);
            }
            worldObject.transform.GetChild(0).gameObject.SetActive((dirMask & 0b_0000_0001) != 0);
            worldObject.transform.GetChild(1).gameObject.SetActive((dirMask & 0b_0000_0010) != 0);
            worldObject.transform.GetChild(4).gameObject.SetActive((dirMask & 0b_0000_0100) != 0);
            worldObject.transform.GetChild(5).gameObject.SetActive((dirMask & 0b_0000_1000) != 0);
            worldObject.transform.GetChild(2).gameObject.SetActive((dirMask & 0b_0001_0000) != 0);
            worldObject.transform.GetChild(3).gameObject.SetActive((dirMask & 0b_0010_0000) != 0);
        }
        public void UpdateMesh(){
            MemoryManager.ClearArray(meshStarts, 7);
            genTime = Time.realtimeSinceStartup;
            if(simpleMesh != null){
                var pos = (int3)WorldPosition;
                var noiseJob = new NoiseJob2D{
                    offset = new float2(pos.x, pos.z),
                    depthMultiplier = depthMultiplier,
                    size = ChunkManager.chunkResolution + 1,
                    heightMap = simpleMesh.heightMap,
                    noiseProperties = TerraxelWorld.DensityManager.GetNoiseProperties()
                };
                base.ScheduleParallelJob(noiseJob, 1089);
                var meshJob = new Mesh2DJob(){
                    heightMap = simpleMesh.heightMap,
                    chunkSize = ChunkManager.chunkResolution + 1,
                    vertices = simpleMesh.buffer,
                    chunkPos = new int2(pos.x, pos.z),
                    surfaceLevel = TerraxelWorld.DensityManager.GetNoiseProperties().surfaceLevel
                };
                base.ScheduleParallelJob(meshJob, 1089, true);
                return;
            }
            var densityData = TerraxelWorld.DensityManager.GetJobDensityData();
            var cache = new DensityCacheInstance(new int3(int.MaxValue));
            var marchingJob = new MeshJob()
            {
                densities = densityData,
                chunkPos = (int3)WorldPosition,
                chunkSize = ChunkManager.chunkResolution,
                vertices = meshData.vertexBuffer,
                depthMultiplier = depthMultiplier,
                negativeDepthMultiplier = negativeDepthMultiplier,
                vertexIndices = vertexIndexBuffer,
                triangles = meshData.indexBuffer,
                cache = cache
            };
            base.ScheduleJob(marchingJob, (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution), false);
            var transitionJob = new TransitionMeshJob()
            {
                densities = densityData,
                chunkPos = (int3)WorldPosition,
                chunkSize = ChunkManager.chunkResolution,
                vertices = meshData.vertexBuffer,
                depthMultiplier = depthMultiplier,
                vertexIndices = vertexIndexBuffer,
                triangles = meshData.indexBuffer,
                cache = cache,
                indexTracker = -1,
                meshStarts = meshStarts,
                negativeDepthMultiplier = negativeDepthMultiplier
            };
            base.ScheduleJob(transitionJob, 6 * (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution), true);
        }
        bool CheckNeighbour(int3 relativeOffset, bool refreshNeighbours = false){
            float3 pos = ChunkManager.chunkResolution * depthMultiplier * relativeOffset;
            var tree = TerraxelWorld.ChunkManager.chunkTree as Octree;
            var queryResult = tree.Query(new BoundingBox(this.region.center + pos, this.region.bounds - 4));
            if(queryResult != null){
                if(refreshNeighbours)
                    (queryResult as ChunkData).RefreshRenderState();
                if(queryResult.HasSubChunks){
                    for(int i = 0; i < 8; i++){
                        if((queryResult.children[i] as ChunkData).chunkState != ChunkState.READY) return false;
                    }
                    return true;
                }else if(queryResult.depth == this.depth){
                    return false;
                }
                return false;
            }
            return false;
        }
        public void ApplyMesh(){
            if(simpleMesh != null){
                simpleMesh.worldObject.SetActive(true);
                simpleMesh.worldObject.transform.localScale = new float3(depthMultiplier, 1, depthMultiplier);
                var mesh = simpleMesh.worldObject.GetComponent<MeshFilter>().sharedMesh;
                mesh.SetVertexBufferData(simpleMesh.buffer, 0, 0, 1089,0,MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferData(SimpleMeshData.indices, 0, 0, 6144);
                desc.indexCount = 6144;
                mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                var bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
                mesh.bounds = bounds;
                mesh.RecalculateNormals();
                simpleMesh.worldObject.transform.position = WorldPosition;
                genTime = Time.realtimeSinceStartup - genTime;
                OnMeshReady();
                return;
            }
            int2 totalCount = new int2(meshData.vertexBuffer.Length, meshData.indexBuffer.Length);
            meshStarts[6] = totalCount;
            
            var vertexCount = meshStarts[0].x;
            var indexCount = meshStarts[0].y;
            if (vertexCount > 0)
            {
                if(worldObject == null)
                    InitWorldObject();
                var mesh = worldObject.GetComponent<MeshFilter>().sharedMesh;
                var bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
                mesh.bounds = bounds;
                //Set vertices and indices
                mesh.SetVertexBufferParams(vertexCount, layout);
                mesh.SetVertexBufferData(meshData.vertexBuffer.AsArray(), 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(meshData.indexBuffer.AsArray(), 0, 0, indexCount,  MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                desc.indexCount = indexCount;
                mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                
                this.vertCount = vertexCount;
                this.indexCount = indexCount;
                for(int i = 0; i < 6; i++){
                    var vertexStart = meshStarts[i].x;
                    var indexStart = meshStarts[i].y;
                    var vertexEnd = meshStarts[i+1].x;
                    var indexEnd = meshStarts[i+1].y;
                    var transitionMesh = worldObject.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh;
                    transitionMesh.bounds = bounds;
                    //Set vertices and indices
                    transitionMesh.SetVertexBufferParams(vertexEnd - vertexStart, layout);
                    transitionMesh.SetVertexBufferData(meshData.vertexBuffer.AsArray(), vertexStart, 0, vertexEnd - vertexStart, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                    transitionMesh.SetIndexBufferParams(indexEnd - indexStart, IndexFormat.UInt16);
                    transitionMesh.SetIndexBufferData(meshData.indexBuffer.AsArray(), indexStart, 0, indexEnd - indexStart,  MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                    desc.indexCount = indexEnd - indexStart;
                    transitionMesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                    this.vertCount += vertexEnd - vertexStart;
                    this.indexCount += indexEnd - indexStart;
                }
            }else{
                vertCount = 0;
                indexCount = 0;
                FreeChunkMesh();
            }
            //indexCounter.Dispose();
            //vertexCounter.Dispose();
            genTime = Time.realtimeSinceStartup - genTime;
            MemoryManager.ReturnVertexIndexBuffer(vertexIndexBuffer);
            vertexIndexBuffer = default;
            OnMeshReady();
        }
        public void OnMeshReady(){
            chunkState = ChunkState.READY;
            if(onMeshReady == OnMeshReadyAction.ALERT_PARENT){
                RefreshRenderState(false);
                base.NotifyParentMeshReady();
            }else if(onMeshReady == OnMeshReadyAction.DISPOSE_CHILDREN){
                onMeshReady = OnMeshReadyAction.ALERT_PARENT;
                PruneChunksRecursive();
                RefreshRenderState(true);
            }
        }
        public void FreeChunkMesh(){
            disposeStatus = DisposeState.FREE_MESH;
            TerraxelWorld.ChunkManager.DisposeChunk(this);
        }
        public void PoolChunk(){
            disposeStatus = DisposeState.POOL;
            TerraxelWorld.ChunkManager.DisposeChunk(this);
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