using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using WorldGeneration.DataStructures;

public abstract class Octree : JobRunner
{
    public BoundingBox region; //Region encapsulating the entire octant
    public BoundingBox[] octants; //This objects 8 suboctants
    //public List<BoundingBox> objects; //Bounding box objects in this region

    public Octree[] children; //Child octrees.

    public Octree parent { get; private set; }

    public int depth = ChunkManager.lodLevels;
    
    public int depthMultiplier{
        get{
            return depthMultipliers[depth];
        }
    }
    public float negativeDepthMultiplier{
        get{
            return negativeDepthMultipliers[depth];
        }
    }
    public static readonly int[] depthMultipliers = {
        1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024
    };
    public static readonly float[] negativeDepthMultipliers = {
        1, 0.5f, 0.25f, 0.125f, 0.0625f, 0.03125f, 0.015625f
    };
    public Octree(BoundingBox size, int depth)
    {
        children = new Octree[8];
        region = size;
        //objects = objs;
        parent = null;
        this.depth = depth;
        //var multiplier = ChunkManager.chunkResolution * math.pow(2, depth) / 2;
        //chunkData = ChunkManager.GenerateChunk(new Vector3(size.center.x - multiplier,size.center.y - multiplier,size.center.z - multiplier), depth);
        //chunkData.node = this;
    }

    public Octree()
    {
        children = new Octree[8];
        //objects = new List<BoundingBox>();
        region = new BoundingBox(Vector3.zero, new float3(ChunkManager.chunkResolution * depthMultiplier));
        parent = null;
        //chunkData = ChunkManager.GenerateChunk(new float3(-ChunkManager.chunkResolution * math.pow(2, depth)/2), ChunkManager.lodLevels);
        //chunkData.node = this;
    }

    private Octree CreateNode(BoundingBox region)
    {
        var multiplier = ChunkManager.chunkResolution * depthMultipliers[depth - 1] / 2;
        ChunkData ret = ChunkManager.GenerateChunk(new Vector3(region.center.x - multiplier,region.center.y - multiplier,region.center.z - multiplier), depth - 1, region);
        if(ret != null) 
            ret.parent = this;
        return ret;
    }

    public static BoundingBox[] CreateOctants(BoundingBox box)
    {
        BoundingBox[] octants = new BoundingBox[8];

        octants = new BoundingBox[8];

        Vector3 bounds = box.bounds;
        Vector3 halfBounds = bounds / 2;
        Vector3 center = box.center;
        Vector3 fourthBounds = bounds / 4;

        //North east top octant
        octants[0] = new BoundingBox(center + fourthBounds, halfBounds);

        //North west top octant
        octants[1] = new BoundingBox(new Vector3(center.x - fourthBounds.x, center.y + fourthBounds.y, center.z + fourthBounds.z), halfBounds);

        //South west top octant
        octants[2] = new BoundingBox(new Vector3(center.x - fourthBounds.x, center.y + fourthBounds.y, center.z - fourthBounds.z), halfBounds);

        //South east top octant
        octants[3] = new BoundingBox(new Vector3(center.x + fourthBounds.x, center.y + fourthBounds.y, center.z - fourthBounds.z), halfBounds);

        //North east bottom octant
        octants[4] = new BoundingBox(new Vector3(center.x + fourthBounds.x, center.y - fourthBounds.y, center.z + fourthBounds.z), halfBounds);

        //North west bottom octant
        octants[5] = new BoundingBox(new Vector3(center.x - fourthBounds.x, center.y - fourthBounds.y, center.z + fourthBounds.z), halfBounds);

        //South west bottom octant
        octants[6] = new BoundingBox(new Vector3(center.x - fourthBounds.x, center.y - fourthBounds.y, center.z - fourthBounds.z), halfBounds);

        //South east bottom octant
        octants[7] = new BoundingBox(new Vector3(center.x + fourthBounds.x, center.y - fourthBounds.y, center.z - fourthBounds.z), halfBounds);

        return octants;
    }
    public Octree Query(BoundingBox region){
        for(int i = 0; i < 8; i++){
            if(children[i] == null) return this;
            if(children[i].region.Contains(region)){
                return children[i].Query(region);
            }
        }
        //Root chunk
        if(this.parent == null) return null;
        return this;
    }
    public bool HasSubChunks{
        get{
            return children[0] != null && children[1] != null && children[2] != null && children[3] != null
                 && children[4] != null && children[5] != null && children[6] != null && children[7] != null;
        }
    }
    public bool PruneChunksRecursive(){
        
        bool hadChildren = false;
        for (int i = 0; i < 8; i++){
            if(children[i] != null){
                children[i].PruneChunksRecursive();
                (children[i] as ChunkData).PoolChunk();
                hadChildren = true;
                children[i] = null;
            }
        }
        return hadChildren;
    }
    public void NotifyParentMeshReady(){
        parent?.CheckSubMeshesReady();
    }
    public void CheckSubMeshesReady(){
        for (int i = 0; i < 8; i++){
            if(children[i] == null) return;
            if((children[i] as ChunkData).chunkState != ChunkData.ChunkState.READY) return;
        }
        thisAsChunkData().FreeChunkMesh();
        parent?.CheckSubMeshesReady();
    }
    public void UpdateTreeRecursive()
    {
        float dst = math.distance(ChunkManager.playerBounds.center, region.center);
        if(dst > ChunkManager.chunkResolution * depthMultiplier * 2f){
            if(HasSubChunks){
                var chunk = this as ChunkData;
                if(!chunk.hasMesh){
                    chunk.onMeshReady = ChunkData.OnMeshReady.DISPOSE_CHILDREN;
                    ChunkManager.RegenerateChunk(chunk);
                }else if(chunk.onMeshReady != ChunkData.OnMeshReady.DISPOSE_CHILDREN){
                    chunk.onMeshReady = ChunkData.OnMeshReady.ALERT_PARENT;
                    PruneChunksRecursive();
                }
            }
            return;
        }
        if(depth == 0){
            return;
        }
        
        octants = CreateOctants(region);
        for (int i = 0; i < 8; i++)
        {
            if(children[i] == null){
                
                children[i] = CreateNode(octants[i]);
                if(children[i] == null) return;
            }
            children[i].UpdateTreeRecursive();
        }

    }
    private ChunkData thisAsChunkData(){
        return this as ChunkData;
    }
}
