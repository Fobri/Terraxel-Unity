using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;
using WorldGeneration.DataStructures;
using System.Collections.Generic;

public class Chunk3D : BaseChunk{
        private bool colliderBaking = false;
        private TempBuffer vertexIndexBuffer;
        private MeshData meshData;
        static Material chunkMaterial;
        private NativeArray<int2> meshStarts;
        public GameObject worldObject;
        private Mesh chunkMesh;
        private Mesh[] transitionMeshes = new Mesh[6];
        public override bool CanBeCreated{
            get{
                return meshData.IsCreated || (!meshData.IsCreated && MemoryManager.GetFreeMeshDataCount() > 0);
            }
        }
        static VertexAttributeDescriptor[] layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.SInt32, 1)
                };
        public Chunk3D(BoundingBox bounds, int depth) 
        : base(bounds, depth){
            meshStarts = MemoryManager.GetMeshCounterArray();
            chunkMesh = new Mesh();
            for(int i = 0; i < transitionMeshes.Length; i++){
                transitionMeshes[i] = new Mesh();
            }
        }
        static Chunk3D(){
            chunkMaterial = Resources.Load("TerrainMaterial", typeof(Material)) as Material;
        }
        //ROOT chunk
        public Chunk3D() : base() {
        }
        void InitWorldObject(){
            worldObject = TerraxelWorld.ChunkManager.GetChunkObject();
            worldObject.name = $"Chunk {WorldPosition.x}, {WorldPosition.y}, {WorldPosition.z}";
            worldObject.transform.position = WorldPosition;
        }
        public override void RenderChunk(){
            if(!active || chunkState != ChunkState.READY) return;
            Graphics.DrawMesh(chunkMesh, WorldPosition, Quaternion.identity, chunkMaterial, 0, null, 0, propertyBlock, true, true, true);
            for(int i = 0; i < transitionMeshes.Length; i++){
                if(transitionMeshes[i] == null || (dirMask & transitionMeshIndexMap[i]) == 0) continue;
                Graphics.DrawMesh(transitionMeshes[i], WorldPosition, Quaternion.identity, chunkMaterial, 0, null, 0, propertyBlock, true, true, true);
            }
            base.RenderGrass();
        }
        static readonly int[] transitionMeshIndexMap = new int[] {
            0b_0000_0001, 0b_0000_0010, 0b_0001_0000, 0b_0010_0000, 0b_0000_0100, 0b_0000_1000
        };
        public void ClearMesh(){
            if(chunkMesh == null) return;
            chunkMesh.Clear();
            for(int i = 0; i < 6; i++){
                transitionMeshes[i].Clear();
            }
        }
        internal override void OnJobsReady(){
            if(colliderBaking){
                if(worldObject == null) return;
                worldObject.GetComponent<MeshCollider>().sharedMesh = null;
                worldObject.GetComponent<MeshCollider>().sharedMesh = chunkMesh;
                colliderBaking = false;
            }
            else{
                ApplyMesh();
                base.PushGrassData();
            }
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
        public override void RefreshRenderState(bool refreshNeighbours = false){
            UpdateDirectionMask(refreshNeighbours);
            propertyBlock?.SetInt("_DirectionMask", dirMask);
        }
        protected override void OnScheduleMeshUpdate(){
            if(vertexIndexBuffer.vertexIndices == default)
                vertexIndexBuffer = MemoryManager.GetVertexIndexBuffer();
            else
                vertexIndexBuffer.ClearBuffers();
            if(!meshData.IsCreated)
                meshData = MemoryManager.GetMeshData();
            else
                meshData.ClearBuffers();
            colliderBaking = false;
            MemoryManager.ClearArray(meshStarts, 7);
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
                cache = cache,
                grassData = base.grassData,
                rng = base.rng
            };
            base.ScheduleJobFor(marchingJob, (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution), false);
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
            base.ScheduleJobFor(transitionJob, 6 * (ChunkManager.chunkResolution) * (ChunkManager.chunkResolution), true);
            var _pos = (int3)WorldPosition;
        }
        bool CheckNeighbour(int3 relativeOffset, bool refreshNeighbours = false){
            float3 pos = ChunkManager.chunkResolution * depthMultiplier * relativeOffset;
            var tree = TerraxelWorld.ChunkManager.chunkTree as Octree;
            var queryResult = tree.Query(new BoundingBox(this.region.center + pos, this.region.bounds - 4));
            if(queryResult != null){
                if(refreshNeighbours)
                    (queryResult as Chunk3D)?.RefreshRenderState();
                if(queryResult.HasSubChunks){
                    for(int i = 0; i < 8; i++){
                        if((queryResult.children[i] as BaseChunk).chunkState != ChunkState.READY) return false;
                    }
                    return true;
                }else if(queryResult.depth == this.depth){
                    return false;
                }
                return false;
            }
            return false;
        }
        public override void ApplyMesh(){
            //_grassPositions = grassPositions.ToArray();
            int2 totalCount = new int2(meshData.vertexBuffer.Length, meshData.indexBuffer.Length);
            meshStarts[6] = totalCount;
            
            vertCount = 0;
            idxCount = 0;
            
            var vertexCount = meshStarts[0].x;
            var indexCount = meshStarts[0].y;
            if (vertexCount > 0)
            {
                if(onMeshReady == OnMeshReadyAction.ALERT_PARENT)
                    SetActive(false);
                //var mesh = worldObject.GetComponent<MeshFilter>().sharedMesh;
                var bounds = new Bounds(new float3(ChunkManager.chunkResolution * depthMultiplier / 2), region.bounds);
                chunkMesh.bounds = bounds;
                //Set vertices and indices
                chunkMesh.SetVertexBufferParams(vertexCount, layout);
                chunkMesh.SetVertexBufferData(meshData.vertexBuffer.AsArray(), 0, 0, vertexCount, 0, MESH_UPDATE_FLAGS);
                chunkMesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                chunkMesh.SetIndexBufferData(meshData.indexBuffer.AsArray(), 0, 0, indexCount,  MESH_UPDATE_FLAGS);

                desc.indexCount = indexCount;
                chunkMesh.SetSubMesh(0, desc, MESH_UPDATE_FLAGS);
                
                this.vertCount = vertexCount;
                this.idxCount = indexCount;
                for(int i = 0; i < 6; i++){
                    var vertexStart = meshStarts[i].x;
                    var indexStart = meshStarts[i].y;
                    var vertexEnd = meshStarts[i+1].x;
                    var indexEnd = meshStarts[i+1].y;
                    //var transitionMesh = worldObject.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh;
                    transitionMeshes[i].bounds = bounds;
                    //Set vertices and indices
                    transitionMeshes[i].SetVertexBufferParams(vertexEnd - vertexStart, layout);
                    transitionMeshes[i].SetVertexBufferData(meshData.vertexBuffer.AsArray(), vertexStart, 0, vertexEnd - vertexStart, 0, MESH_UPDATE_FLAGS);
                    transitionMeshes[i].SetIndexBufferParams(indexEnd - indexStart, IndexFormat.UInt16);
                    transitionMeshes[i].SetIndexBufferData(meshData.indexBuffer.AsArray(), indexStart, 0, indexEnd - indexStart,  MESH_UPDATE_FLAGS);

                    desc.indexCount = indexEnd - indexStart;
                    transitionMeshes[i].SetSubMesh(0, desc, MESH_UPDATE_FLAGS);
                    this.vertCount += vertexEnd - vertexStart;
                    this.idxCount += indexEnd - indexStart;
                }
                if(worldObject == null)
                    InitWorldObject();
                var colliderJob = new ChunkColliderBakeJob(){
                    meshId = chunkMesh.GetInstanceID()
                };
                colliderBaking = true;
                ScheduleJob(colliderJob, false);
            }else if(depth > 1){
                FreeChunkMesh();
            }
            //indexCounter.Dispose();
            //vertexCounter.Dispose();
            MemoryManager.ReturnVertexIndexBuffer(vertexIndexBuffer);
            vertexIndexBuffer = default;
            OnMeshReady();
        }
        public override void OnMeshReady(){
            genTime = Time.realtimeSinceStartup - genTime;
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
        protected override void OnFreeBuffers(){
            ClearMesh();
            hasMesh = false;
            worldObject = null;
            if(meshData.IsCreated){
                MemoryManager.ReturnMeshData(meshData);
                meshData = default;
            }
        }

        public static Chunk3D CreateCopy(BaseChunk source){
            Chunk3D result = TerraxelWorld.ChunkManager.GetNewChunk3D(source.region, source.depth);
            result.children = source.children;
            result.onMeshReady = source.onMeshReady;
            result.hasMesh = source.hasMesh;
            result.parent = source.parent;
            return result;
        }
    }