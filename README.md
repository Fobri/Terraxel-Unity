# Unity-Marching-Cubes-Octree
This is an Unity implementation of the marching cubes algorithm with vertex sharing and LOD with a recursive octree

TODO:

Update marching cubes algorithm to transvoxel https://transvoxel.org/

Infinite world

Proper memory allocator for vertex and index data

Add colliders to 0 depth chunks

Terrain editing



Problems

When moving really fast some high detail chunks might remain until parent chunk gets pooled

Slight stutter when a lot of chunks get loaded/unloaded

If the highest LOD level has no surface, the chunk doesnt divide further possibly missing detail (Like small islands etc)

