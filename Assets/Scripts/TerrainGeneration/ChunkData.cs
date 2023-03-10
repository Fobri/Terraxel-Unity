using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;
using System;
using WorldGeneration.DataStructures;
using System.Collections.Generic;

public class ChunkData : Octree{
    private const MeshUpdateFlags MESH_UPDATE_FLAGS = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds;
        public enum ChunkState { DIRTY, READY, INVALID, ROOT, QUEUED }
        public enum OnMeshReadyAction { ALERT_PARENT, DISPOSE_CHILDREN }
        public enum DisposeState { NOTHING, POOL, FREE_MESH }
        bool colliderBaking = false;
        public TempBuffer vertexIndexBuffer;
        public MeshData meshData;
        private NativeArray<int2> meshStarts;
        public GameObject worldObject;
        Mesh[] transitionMeshes = new Mesh[6];
        MaterialPropertyBlock propertyBlock;
        Mesh chunkMesh;
        bool active = true;
        static Material chunkMaterial;
        static Mesh grassMesh;
        static Material grassMaterial;
        public Matrix4x4[] grassPositions;
        public SimpleMeshData simpleMesh;
        //public Counter vertexCounter;
        //public Counter indexCounter;
        public ChunkState chunkState = ChunkState.INVALID;
        public OnMeshReadyAction onMeshReady = OnMeshReadyAction.ALERT_PARENT;
        public DisposeState disposeStatus = DisposeState.NOTHING;
        public float genTime;
        public int vertCount;
        public int idxCount;
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
            chunkMesh = new Mesh();
            for(int i = 0; i < transitionMeshes.Length; i++){
                transitionMeshes[i] = new Mesh();
            }
            propertyBlock = new MaterialPropertyBlock();
        }
        static ChunkData(){
            chunkMaterial = Resources.Load("TerrainMaterial", typeof(Material)) as Material;
            grassMesh = Resources.Load("GrassMesh", typeof(Mesh)) as Mesh;
            grassMaterial = Resources.Load("GrassMaterial", typeof(Material)) as Material;
        }
        //ROOT chunk
        public ChunkData() : base() {
        }
        public void SetActive(bool active){
            this.active = active;
        }
        void InitWorldObject(){
            worldObject = TerraxelWorld.ChunkManager.GetChunkObject();
            worldObject.name = $"Chunk {WorldPosition.x}, {WorldPosition.y}, {WorldPosition.z}";
            worldObject.transform.position = WorldPosition;
        }
        public void RenderChunk(){
            if(!active) return;
            Graphics.DrawMesh(chunkMesh, WorldPosition, Quaternion.identity, chunkMaterial, 0, null, 0, propertyBlock, true, true, true);
            for(int i = 0; i < transitionMeshes.Length; i++){
                if(transitionMeshes[i] == null || (dirMask & transitionMeshIndexMap[i]) == 0) continue;
                Graphics.DrawMesh(transitionMeshes[i], WorldPosition, Quaternion.identity, chunkMaterial, 0, null, 0, propertyBlock, true, true, true);
            }
            if(grassPositions != null){
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, grassPositions, meshData.grassPositions.Length, null, ShadowCastingMode.On, false, 0, null, LightProbeUsage.Off, null);
            }
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
            else
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
            //if(worldObject == null) return;
            /*worldObject.GetComponent<MeshRenderer>().material.SetInt("_DirectionMask", dirMask);
            for(int i = 0; i < 6; i++){
                worldObject.transform.GetChild(i).GetComponent<MeshRenderer>().material.SetInt("_DirectionMask", dirMask);
            }*/
            propertyBlock?.SetInt("_DirectionMask", dirMask);
            /*worldObject.transform.GetChild(0).gameObject.SetActive((dirMask & 0b_0000_0001) != 0);
            worldObject.transform.GetChild(1).gameObject.SetActive((dirMask & 0b_0000_0010) != 0);
            worldObject.transform.GetChild(4).gameObject.SetActive((dirMask & 0b_0000_0100) != 0);
            worldObject.transform.GetChild(5).gameObject.SetActive((dirMask & 0b_0000_1000) != 0);
            worldObject.transform.GetChild(2).gameObject.SetActive((dirMask & 0b_0001_0000) != 0);
            worldObject.transform.GetChild(3).gameObject.SetActive((dirMask & 0b_0010_0000) != 0);*/
        }
        public void UpdateMesh(){
            colliderBaking = false;
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
                base.ScheduleParallelForJob(noiseJob, 1089);
                var meshJob = new Mesh2DJob(){
                    heightMap = simpleMesh.heightMap,
                    chunkSize = ChunkManager.chunkResolution + 1,
                    vertices = simpleMesh.buffer,
                    chunkPos = new int2(pos.x, pos.z),
                    surfaceLevel = TerraxelWorld.DensityManager.GetNoiseProperties().surfaceLevel
                };
                base.ScheduleParallelForJob(meshJob, 1089, true);
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
            var grassJob = new GrassJob{
                offset = new float2(_pos.x, _pos.z),
                depthMultiplier = depthMultiplier,
                size = ChunkManager.chunkResolution + 1,
                noiseProperties = TerraxelWorld.DensityManager.GetNoiseProperties(),
                positions = meshData.grassPositions,
                rng = new Unity.Mathematics.Random(1)
            };
            base.ScheduleJobFor(grassJob, 1023, false);
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
            grassPositions = meshData.grassPositions.ToArray();
            if(simpleMesh != null){
                simpleMesh.worldObject.SetActive(true);
                simpleMesh.worldObject.transform.localScale = new float3(depthMultiplier, 1, depthMultiplier);
                var mesh = simpleMesh.worldObject.GetComponent<MeshFilter>().sharedMesh;
                mesh.SetVertexBufferData(simpleMesh.buffer, 0, 0, 1089,0, MESH_UPDATE_FLAGS);
                mesh.SetIndexBufferData(SimpleMeshData.indices, 0, 0, 6144);
                desc.indexCount = 6144;
                mesh.SetSubMesh(0, desc, MESH_UPDATE_FLAGS);
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
                if(depth < 3){
                    if(worldObject == null)
                        InitWorldObject();
                    var colliderJob = new ChunkColliderBakeJob(){
                        meshId = chunkMesh.GetInstanceID()
                    };
                    colliderBaking = true;
                    ScheduleJob(colliderJob, false);
                }
            }else if(depth > 1){
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