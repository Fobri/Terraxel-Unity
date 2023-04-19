using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Terraxel.DataStructures;

public abstract class Octree : JobRunner
{
    public BoundingBox region; //Region encapsulating the entire octant
    public BoundingBox[] octants; //This objects 8 suboctants
    //public List<BoundingBox> objects; //Bounding box objects in this region

    public Octree[] children; //Child octrees.

    public Octree parent { get; set; }

    public int depth = TerraxelConstants.lodLevels;
    
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
        var ret = TerraxelWorld.ChunkManager.GenerateChunk(region.center - multiplier, depth - 1, region);
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
    public void QueryColliding(BoundingBox region, List<Octree> result){
        for(int i = 0; i < 8; i++){
            if(children[i].region.IsColliding(region)){
                if(!children[i].HasSubChunks){
                    if(!result.Contains(children[i]))
                        result.Add(children[i]);
                }else{
                    children[i].QueryColliding(region, result);
                }
            }
        }
    }
    public void RepositionOctets(List<int3> newOctetPositions, int size){
        if(!HasSubChunks) return;
        Dictionary<int3, Octree> instanceByPosition = new Dictionary<int3, Octree>();
        Queue<Octree> freeNodes = new Queue<Octree>();
        for(int i = 0; i < 8; i++){
            for(int s = 0; s < 8; s++){
                var pos = (int3)(children[i].children[s].region.center - size / 2);
                if(!newOctetPositions.Contains(pos)){
                    children[i].children[s].PruneChunksRecursiveNow();
                    TerraxelWorld.ChunkManager.FreeChunkBuffers(children[i].children[s] as BaseChunk);
                    freeNodes.Enqueue(children[i].children[s]);
                }else{
                    newOctetPositions.Remove(pos);
                    instanceByPosition.Add(pos, children[i].children[s]);
                }
            }
        }
        for(int i = 0; i < newOctetPositions.Count; i++){
            var octet = freeNodes.Dequeue();
            octet.region.center = newOctetPositions[i] + size / 2;
            instanceByPosition.Add(newOctetPositions[i], octet);
            TerraxelWorld.ChunkManager.RegenerateChunk(octet as BaseChunk);
            (octet as BaseChunk).UpdateTreeRecursive();
        }
        if(newOctetPositions.Count > 0){
            RecalculateChildren(instanceByPosition, size);
        }
    }
    private void RecalculateChildren(Dictionary<int3, Octree> instanceByPosition, int size){
        var octs = CreateOctants(region);
        for(int i = 0; i < 8; i++){
            children[i].region = octs[i];
            (children[i] as BaseChunk).RecalculateBounds();
            var octants = CreateOctants(children[i].region);
            for(int s = 0; s < 8; s++){
                children[i].children[s] = instanceByPosition[(int3)octants[s].center - size / 2];
                (children[i].children[s] as BaseChunk).onMeshReady = OnMeshReadyAction.DISPOSE_CHILDREN;
            }
        }
    }
    public bool HasSubChunks{
        get{
            return children[0] != null & children[1] != null & children[2] != null & children[3] != null
                 & children[4] != null & children[5] != null & children[6] != null & children[7] != null;
        }
    }
    public void PruneChunksRecursive(){
        for (int i = 0; i < 8; i++){
            if(children[i] != null){
                children[i].PruneChunksRecursive();
                children[i].thisChunk.PoolChunk();
                children[i] = null;
            }
        }
    }
    public void PruneChunksRecursiveNow(){
        for (int i = 0; i < 8; i++){
            if(children[i] != null){
                children[i].PruneChunksRecursiveNow();
                TerraxelWorld.ChunkManager.PoolChunk(children[i].thisChunk);
                children[i] = null;
            }
        }
    }
    public void NotifyParentMeshReady(){
        parent?.CheckSubMeshesReady();
    }
    public void CheckSubMeshesReady(){
        for (int i = 0; i < 8; i++){
            if(children[i] == null) return;
            if(children[i].thisChunk.chunkState != ChunkState.READY) return;
        }
        for (int i = 0; i < 8; i++){
            children[i].thisChunk.SetActive(true);
        }
        thisChunk.FreeChunkMesh();
        thisChunk.RefreshRenderState(true);
        parent?.CheckSubMeshesReady();
    }
    const float dstModifier = 45.2548f;//55.42562f;
    public static readonly float[] maxDistances = { 0, 60,150f, 340, 780, 1500, 3000, 1800, 2500 };
    public void UpdateTreeRecursive()
    {
        /*var maxCoord = TerraxelWorld.playerBounds.center + ChunkManager.chunkResolution * depthMultiplier * 0.8f;
        var minCoord = TerraxelWorld.playerBounds.center - ChunkManager.chunkResolution * depthMultiplier * 0.8f;(math.any(region.center < minCoord) || math.any(region.center > maxCoord))*/
        float dst = math.distance(TerraxelWorld.playerBounds.center, region.center);
        float dst2D = math.distance(new float2(TerraxelWorld.playerBounds.center.x, TerraxelWorld.playerBounds.center.z), new float2(region.center.x, region.center.z));
        if(dst > maxDistances[depth] && depth < TerraxelConstants.lodLevels - 1){
            if(HasSubChunks){
                if(!thisChunk.hasMesh){
                    /*if(depth > ChunkManager.simpleChunkTreshold && dst2D > maxDistances[ChunkManager.simpleChunkTreshold+1]){
                        if(this is Chunk3D){
                            Regenerate3DTo2D();
                            return;
                        }
                    }else if(this is Chunk2D){
                        Regenerate2DTo3D();
                        return;
                    }*/
                    thisChunk.onMeshReady = OnMeshReadyAction.DISPOSE_CHILDREN;
                    TerraxelWorld.ChunkManager.RegenerateChunk(thisChunk);
                }else if(thisChunk.onMeshReady != OnMeshReadyAction.DISPOSE_CHILDREN){
                    thisChunk.onMeshReady = OnMeshReadyAction.ALERT_PARENT;
                    PruneChunksRecursive();
                    thisChunk.RefreshRenderState();
                }
            }
            return;
        }
        if(depth == 0){
            //if(!chunkData.meshData.IsCreated) TerraxelWorld.ChunkManager.RegenerateChunk(chunkData);
            return;
        }
        /*if(thisChunk.chunkState != ChunkState.ROOT && this is Chunk3D && depth > ChunkManager.simpleChunkTreshold && dst2D > maxDistances[ChunkManager.simpleChunkTreshold+1]){
            Regenerate3DTo2D();
            return;
        }*/
        
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
    public void RenderChunksRecursive(Plane[] frustumPlanes){
        if(thisChunk.chunkState != ChunkState.ROOT) thisChunk.RenderChunk();
        if(!HasSubChunks) return;
        for(int s = 0; s < 8; s++){
            if(!TerraxelWorld.frustumCulling || GeometryUtility.TestPlanesAABB(frustumPlanes, children[s].thisChunk.renderBounds)){
                children[s].RenderChunksRecursive(frustumPlanes);
                //break;
            }
            /*for(int i = 0; i < 6; i++){
                var dst = math.dot(frustumPlanes[i].normal, children[s].region.center) + frustumPlanes[i].distance;
                if(math.abs(dst) < children[s].region.cullRadius){
                    children[s].RenderChunksRecursive(frustumPlanes);
                    break;
                }
            }
            /*var corners = children[s].region.GetCorners();
            for(int i = 0; i < 8; i++){
                var viewPortPos = TerraxelWorld.renderCamera.WorldToViewportPoint(corners[i]);
                if(viewPortPos.z >= 0 && (viewPortPos.x > 0 && viewPortPos.x < 1 && viewPortPos.y > 0 && viewPortPos.y < 1) || children[s].region.Contains(playerPos)){
                    children[s].RenderChunksRecursive(playerPos);
                    break;
                }
            }*/
        }
    }
    private BaseChunk thisChunk{
        get{
        return this as BaseChunk;
        }
    }
}
