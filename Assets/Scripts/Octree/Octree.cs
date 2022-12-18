using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class Octree
{
    public Octree(BoundingBox size, int depth)
    {
        children = new Octree[8];
        region = size;
        //objects = objs;
        parent = null;
        this.depth = depth;
        var multiplier = ChunkManager.chunkResolution * math.pow(2, depth) / 2;
        chunkData = ChunkManager.GenerateChunk(new Vector3(size.center.x - multiplier,size.center.y - multiplier,size.center.z - multiplier), depth);
        chunkData.node = this;
    }

    public Octree()
    {
        children = new Octree[8];
        //objects = new List<BoundingBox>();
        region = new BoundingBox(Vector3.zero, new float3(ChunkManager.chunkResolution * math.pow(2, depth)));
        parent = null;
        chunkData = ChunkManager.GenerateChunk(new float3(-ChunkManager.chunkResolution * math.pow(2, depth)/2), ChunkManager.lodLevels);
        chunkData.node = this;
    }

    private Octree CreateNode(BoundingBox region)
    {
        Octree ret = new Octree(region, depth - 1);
        ret.parent = this;
        return ret;
    }

    public static List<BoundingBox> TestCollision(Octree tree, BoundingBox b)
    {
        List<BoundingBox> collisions = new List<BoundingBox>();

        if (tree == null)
            return new List<BoundingBox>();

        if (tree.region.IsColliding(b))
        {
            //Loop through current trees objects
            /*foreach(BoundingBox obj in tree.objects)
            {
                if (obj.IsColliding(b))
                {
                    collisions.Add(obj);
                }
            }*/

            if (tree.children != null)
            {
                //Loop through suboctants
                for (int i = 0; i < 8; i++)
                {
                    if (tree.children[i] != null)
                    {
                        collisions.AddRange(TestCollision(tree.children[i], b));
                    }
                }
            }
        }

        return collisions;
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
    public void RemoveChunk(){
        chunkData.FreeChunk();
        //chunkData = null;
    }
    public bool RemoveChunksRecursive(){
        bool hadChildren = false;
        for (int i = 0; i < 8; i++){
            if(children[i] != null){
                children[i].RemoveChunksRecursive();
                children[i].RemoveChunk();
                hadChildren = true;
                children[i] = null;
            }
        }
        return hadChildren;
    }
    /*public void OnMeshReady(){
        if(parent != null)
            parent.SubChunkDone();
    }
    public void SubChunkDone(){
        if(subChunksInGeneration == 0) return;
        subChunksInGeneration--;
        if(subChunksInGeneration == 0){
            RemoveChunk();
        }
    }*/
    public void BuildTree()
    {
        //if (objects.Count <= 1)
            //return;

        //if (region.bounds.x <= 1.0f && region.bounds.y <= 1.0 && region.bounds.z <= 1.0)
        if(!region.IsColliding(ChunkManager.playerBounds) && !region.Contains(ChunkManager.playerBounds)){
            if(RemoveChunksRecursive()){
                var multiplier = ChunkManager.chunkResolution * math.pow(2, depth) / 2;
                chunkData = ChunkManager.GenerateChunk(new Vector3(region.center.x - multiplier,region.center.y - multiplier,region.center.z - multiplier), depth);
            }
            return;
        }
           // return;
        if(depth == 0) return;
        
        
        //Create the tree suboctants
        

        //List<BoundingBox>[] octLists = new List<BoundingBox>[8];
        //Debug.Log(ChunkManager.playerBounds.bounds + " " + ChunkManager.playerBounds.center);
        subChunksInGeneration = 8;
        octants = CreateOctants(region);
        for (int i = 0; i < 8; i++)
        {
            //Debug.Log(octants[i].bounds + " " + octants[i].center);
            //if(octants[i].IsColliding(ChunkManager.playerBounds) || octants[i].Contains(ChunkManager.playerBounds)){
            if(children[i] == null){
                
                children[i] = CreateNode(octants[i]);
            }
            children[i].BuildTree();
            //}
        }
        RemoveChunk();
        /*if(chunkData == null){
            var multiplier = ChunkManager.chunkResolution * math.pow(2, depth) / 2;
            chunkData = ChunkManager.GenerateChunk(new Vector3(region.center.x - multiplier,region.center.y - multiplier,region.center.z - multiplier), depth);
        }*/
        //List<BoundingBox> delist = new List<BoundingBox>();

        /*foreach (BoundingBox obj in objects)
        {
            for (int a = 0; a < 8; a++)
            {
                if (octants[a].Contains(obj))
                {
                    octLists[a].Add(obj);
                    delist.Add(obj);
                    break;
                }
            }
        }*/

        //delist every moved object from this node.
        //foreach (BoundingBox obj in delist)
            //objects.Remove(obj);

    }
    int subChunksInGeneration = 0;
    public BoundingBox region; //Region encapsulating the entire octant
    public BoundingBox[] octants; //This objects 8 suboctants
    //public List<BoundingBox> objects; //Bounding box objects in this region

    public Octree[] children; //Child octrees.

    public Octree parent { get; private set; }

    public int depth = ChunkManager.lodLevels;
    public DataStructures.ChunkData chunkData;
}
