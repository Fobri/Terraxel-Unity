
# Terraxel

Terraxel is an infinite procedurally generated terrain generator for Unity.

It generates meshes near the player using marching cubes and transvoxel algorithms, and chunks that are further away are simple 2d chunks.

It is NOT in any way feature complete, and is meant to demonstrate the possibilities of such algorithms in Unity.

If you are looking for the transvoxel implementation it can be found in Jobs.cs, method TransitionMeshJob.






 [Demo video](https://youtu.be/sSxwXc8xHwg)
Textures and 3D models shown on the video are not included.
## Features
- Burst compiled Transvoxel implementation using Unity Jobs
- Chunk and LOD management using an octree
- Collider baking in Jobs
- Simple graph editor for terrain shape that compiles into a compute shader
- GPU noise generation
- Very simple instanced renderer to demonstrate grass rendering

## Used in the project

- [NodeGraphProcessor](https://github.com/alelievr/NodeGraphProcessor)
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite)


## Installation

The version of Unity I used with the project is 2022.3, but earlier versions should work fine too.

Download the source, open it in Unity and open the sample scene. Features are shown on the video, that should be enough to get started.

You can cut the terrain with C and fill it with F.
    
## Acknowledgements

 - [Lengyel, Eric. “Voxel-Based Terrain for Real-Time Virtual Simulations”. PhD diss., University of California at Davis, 2010.](http://transvoxel.org/)


## License

[MIT](https://choosealicense.com/licenses/mit/)

