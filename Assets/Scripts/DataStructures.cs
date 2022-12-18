using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;
using UnityEngine.Rendering;

namespace DataStructures
{
    public class ChunkData{
        
        public NativeArray<float> densityMap;
        public NativeArray<Vector3> vertices;
        public NativeArray<int> indices;
        public JobHandle meshJobHandle;
        public float3 pos;
        public GameObject worldObject;
        public Counter counter;
        public bool dirty;
        public bool dispose;
        public int depth;
        public Octree node;
        public float genTime;
        SubMeshDescriptor desc = new SubMeshDescriptor();
        public ChunkData(float3 pos, GameObject worldObject, int depth){
            this.pos = pos;
            this.worldObject = worldObject;
            meshJobHandle = default;
            desc.topology = MeshTopology.Triangles;
            worldObject.GetComponent<MeshFilter>().sharedMesh = new Mesh();
            this.depth = depth;
        }
        public void ApplyMesh(){
            dirty = false;
            var mesh = worldObject.GetComponent<MeshFilter>().sharedMesh;
            var layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                };
            var vertexCount = counter.Count * 3;
            if (vertexCount > 0)
            {
                
                var size = ChunkManager.chunkResolution * math.pow(2, depth) / 2;
                mesh.bounds = new Bounds(new Vector3(size, size, size), new Vector3(ChunkManager.chunkResolution * math.pow(2, depth), ChunkManager.chunkResolution * math.pow(2, depth), ChunkManager.chunkResolution * math.pow(2, depth)));
                //Set vertices and indices
                mesh.SetVertexBufferParams(vertexCount, layout);
                mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
                mesh.SetIndexBufferData(indices, 0, 0, vertexCount, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                desc.indexCount = vertexCount;
                mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

                mesh.RecalculateNormals();
                //filter.sharedMesh = myMesh;
                counter.Dispose();
            }else{
                FreeChunk();
            }
            genTime = Time.realtimeSinceStartup - genTime;
            ChunkManager.memoryManager.ReturnDensityMap(densityMap);
            densityMap = default;
        }
        public void FreeChunk(){
            dispose = true;
        }
        public int depthMultiplier{
            get{
                return (int)math.pow(2, depth);
            }
        }
    }
}