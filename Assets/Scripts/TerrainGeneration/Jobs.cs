using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Runtime.CompilerServices;
using System;
using Unity.Collections.LowLevel.Unsafe;
using DataStructures;

namespace WorldGeneration
{
    [Serializable]
    public class NoiseData
    {
        public float surfaceLevel;
        public float freq;
        public float ampl;
        public int oct;
        public float offset;
    }
    [BurstCompile]
    public struct VertexSharingJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<ushort> triangles;
        [ReadOnly] public int chunkSize;
        [WriteOnly] public Counter counter;
        [ReadOnly] public NativeArray<ushort4> vertexIndices;

        public void Execute(int index){

            int3 voxelLocalPosition = Utils.IndexToXyz(index, chunkSize);
            if(voxelLocalPosition.x == 0 || voxelLocalPosition.y == 0 || voxelLocalPosition.z == 0) return;

            int cubeIndex = (int)vertexIndices[index].w;
            if (cubeIndex == 0 || cubeIndex == 255)
            {
                return;
            }

            //VertexList vertexList = GenerateVertexList(densities, corners, edgeIndex, isolevel);

            // Index at the beginning of the row
            int rowIndex = 15 * cubeIndex;

            for (int i = 0; Tables.TriangleTable[rowIndex + i] != -1 && i < 15; i += 3)
            {
                int triangleIndex = counter.Increment() * 3;
                triangles[triangleIndex + 0] = GetVertexIndex(Tables.TriangleTable[rowIndex + i + 0], voxelLocalPosition);
                triangles[triangleIndex + 1] = GetVertexIndex(Tables.TriangleTable[rowIndex + i + 1], voxelLocalPosition);
                triangles[triangleIndex + 2] = GetVertexIndex(Tables.TriangleTable[rowIndex + i + 2], voxelLocalPosition);
            }
        }
        private ushort GetVertexIndex(int index, int3 voxelLocalPosition){
            if(index == 5) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].x;
            if(index == 6) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].y;
            if(index == 10) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].z;
            if(index == 9) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z - 1, chunkSize)].z;
            if(index == 8) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y, voxelLocalPosition.z - 1, chunkSize)].z;
            if(index == 11) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].z;
            if(index == 1) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y - 1, voxelLocalPosition.z, chunkSize)].x;
            if(index == 3) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y - 1, voxelLocalPosition.z, chunkSize)].x;
            if(index == 7) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].x;
            if(index == 2) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y - 1, voxelLocalPosition.z, chunkSize)].y;
            if(index == 0) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y - 1, voxelLocalPosition.z - 1, chunkSize)].y;
            if(index == 4) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z - 1, chunkSize)].y;
            return ushort.MaxValue;
        }
    }
    //Marching cubes job from https://github.com/Eldemarkki/Marching-Cubes-Terrain
    [BurstCompile]
    public struct MarchingJob : IJobFor
    {
        /// <summary>
        /// The densities to generate the mesh off of
        /// </summary>
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float> densities;
        
        [ReadOnly] public NeighbourDensities front;
        [ReadOnly] public NeighbourDensities back;
        [ReadOnly] public NeighbourDensities left;
        [ReadOnly] public NeighbourDensities right;
        [ReadOnly] public NeighbourDensities up;
        [ReadOnly] public NeighbourDensities down;
        [ReadOnly] public byte neighbourDirectionMask;

        /// <summary>
        /// The density level where a surface will be created. Densities below this will be inside the surface (solid),
        /// and densities above this will be outside the surface (air)
        /// </summary>
        [ReadOnly] public float isolevel;

        /// <summary>
        /// The chunk's size. This represents the width, height and depth in Unity units.
        /// </summary>
        [ReadOnly] public int chunkSize;

        /// <summary>
        /// The counter to keep track of the triangle index
        /// </summary>
        [WriteOnly] public Counter vertexCounter;
        
        [WriteOnly] public Counter indexCounter;


        /// <summary>
        /// The generated vertices
        /// </summary>
        //[NativeDisableParallelForRestriction, WriteOnly] public NativeArray<Vector3> vertices;

        /// <summary>
        /// The generated vertices
        /// </summary>
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<VertexData> vertices;
        
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<ushort> triangles;

        /// <summary>
        /// The generated triangles
        /// </summary>
        //[NativeDisableParallelForRestriction, WriteOnly] public NativeArray<int> triangles;
        [ReadOnly] public int depthMultiplier;
        public NativeArray<ushort4> vertexIndices;

        /// <summary>
        /// The execute method required by the Unity Job System's IJobParallelFor
        /// </summary>
        /// <param name="index">The iteration index</param>
        public void Execute(int index)
        {
            // Voxel's position inside the chunk. Goes from (0, 0, 0) to (chunkSize-1, chunkSize-1, chunkSize-1)
            int3 voxelLocalPosition = Utils.IndexToXyz(index, chunkSize) - 1;

            VoxelCorners<VoxelCornerElement> densities = GetDensities(voxelLocalPosition);

            int cubeIndex = CalculateCubeIndex(densities, isolevel);
            ushort4 indices = new ushort4(ushort.MaxValue);
            //indices.w = (ushort)cubeIndex;
            if (cubeIndex == 0 || cubeIndex == 255)
            {
                return;
            }

            // Index at the beginning of the row
            int rowIndex = 15 * cubeIndex;
            
            for (int i = 0; Tables.TriangleTable[rowIndex + i] != -1 && i < 15; i += 3)
            {
                for(int v = 0; v < 3; v++){
                    var edgeIdx = Tables.TriangleTable[rowIndex + i + v];
                    if(edgeIdx == 5){
                        int vertexIndex = vertexCounter.Increment();
                        vertices[vertexIndex] = GetVertex(edgeIdx, densities, voxelLocalPosition, isolevel / 255f);
                        indices.x = (ushort)vertexIndex;
                    }
                    if(edgeIdx == 6){
                        int vertexIndex = vertexCounter.Increment();
                        vertices[vertexIndex] = GetVertex(edgeIdx, densities, voxelLocalPosition, isolevel / 255f);
                        indices.y = (ushort)vertexIndex;
                    }
                    if(edgeIdx == 10){
                        int vertexIndex = vertexCounter.Increment();
                        vertices[vertexIndex] = GetVertex(edgeIdx, densities, voxelLocalPosition, isolevel / 255f);
                        indices.z = (ushort)vertexIndex;
                    }
                }

                var pos = voxelLocalPosition + 1;
                if(pos.x == 0 || pos.y == 0 || pos.z == 0) continue;

                int triangleIndex = indexCounter.Increment() * 3;
                triangles[triangleIndex + 0] = GetVertexIndex(Tables.TriangleTable[rowIndex + i + 0], pos, indices);
                triangles[triangleIndex + 1] = GetVertexIndex(Tables.TriangleTable[rowIndex + i + 1], pos, indices);
                triangles[triangleIndex + 2] = GetVertexIndex(Tables.TriangleTable[rowIndex + i + 2], pos, indices);
            }
            vertexIndices[index] = indices;
        }
        private ushort GetVertexIndex(int index, int3 voxelLocalPosition, ushort4 ownCellvalues){
            if(index == 5) return ownCellvalues.x;
            if(index == 6) return ownCellvalues.y;
            if(index == 10) return ownCellvalues.z;
            if(index == 9) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z - 1, chunkSize)].z;
            if(index == 8) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y, voxelLocalPosition.z - 1, chunkSize)].z;
            if(index == 11) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].z;
            if(index == 1) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y - 1, voxelLocalPosition.z, chunkSize)].x;
            if(index == 3) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y - 1, voxelLocalPosition.z, chunkSize)].x;
            if(index == 7) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x - 1, voxelLocalPosition.y, voxelLocalPosition.z, chunkSize)].x;
            if(index == 2) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y - 1, voxelLocalPosition.z, chunkSize)].y;
            if(index == 0) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y - 1, voxelLocalPosition.z - 1, chunkSize)].y;
            if(index == 4) return vertexIndices[Utils.XyzToIndex(voxelLocalPosition.x, voxelLocalPosition.y, voxelLocalPosition.z - 1, chunkSize)].y;
            return ushort.MaxValue;
        }
        /// <summary>
        /// Gets the densities for the voxel at a position
        /// </summary>
        /// <param name="localPosition">Voxel's local position</param>
        /// <returns>The densities of that voxel</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VoxelCorners<VoxelCornerElement> GetDensities(int3 localPosition)
        {
            VoxelCorners<VoxelCornerElement> densities = new VoxelCorners<VoxelCornerElement>();
            for (int i = 0; i < 8; i++)
            {
                int3 voxelCorner = localPosition + Tables.CubeCorners[i];
                //int densityIndex = voxelCorner.x * (chunkSize + 1) * (chunkSize + 1) + voxelCorner.y * (chunkSize + 1) + voxelCorner.z;
                VoxelCornerElement element = new VoxelCornerElement();
                element.density = SampleDensity(voxelCorner);
                densities[i] = element;
            }

            return densities;
        }

        /// <summary>
        /// Gets the corners for the voxel at a position
        /// </summary>
        /// <param name="position">The voxel's position</param>
        /// <returns>The voxel's corners</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VoxelCorners<int3> GetCorners(int3 position)
        {
            VoxelCorners<int3> corners = new VoxelCorners<int3>();
            for (int i = 0; i < 8; i++)
            {
                corners[i] = position + Tables.CubeCorners[i];
            }

            return corners;
        }

        /// <summary>
        /// Interpolates the vertex's position 
        /// </summary>
        /// <param name="p1">The first corner's position</param>
        /// <param name="p2">The second corner's position</param>
        /// <param name="v1">The first corner's density</param>
        /// <param name="v2">The second corner's density</param>
        /// <param name="isolevel">The density level where a surface will be created. Densities below this will be inside the surface (solid),
        /// and densities above this will be outside the surface (air)</param>
        /// <returns>The interpolated vertex's position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VertexData VertexInterpolate(float3 p1, float3 p2, VoxelCornerElement v1, VoxelCornerElement v2, float isolevel)
        {
            var vert = new VertexData();
            vert.vertex = (p1 + (isolevel - v1.density) * (p2 - p1) / (v2.density - v1.density)) * depthMultiplier;
            //vert.normal = math.normalize((isolevel - v1.density) * (v2.normal - v1.normal) / (v2.density - v1.density));
            var normal1 = GetVertexNormal((int3)p1);
            var normal2 = GetVertexNormal((int3)p2);
            vert.normal = (normal1 + (isolevel - v1.density) * (normal2 - normal1) / (v2.density - v1.density));
            return vert;
        }

        /// <summary>
        /// Generates the vertex list for a single voxel
        /// </summary>
        /// <param name="voxelDensities">The voxel's densities</param>
        /// <param name="voxelCorners">The voxel's corners</param>
        /// <param name="edgeIndex">The edge index</param>
        /// <param name="isolevel">The density level where a surface will be created. Densities below this will be inside the surface (solid),
        /// and densities above this will be outside the surface (air)</param>
        /// <returns>The generated vertex list for the voxel</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe VertexData GetVertex(int index, VoxelCorners<VoxelCornerElement> voxelDensities, int3 voxelLocalPosition, float isolevel) {
            int edgeStartIndex = Tables.EdgeIndexTable[2 * index + 0];
            int edgeEndIndex = Tables.EdgeIndexTable[2 * index + 1];
            int3 corner1 = voxelLocalPosition + Tables.CubeCorners[edgeStartIndex];
            int3 corner2 = voxelLocalPosition + Tables.CubeCorners[edgeEndIndex];
            VoxelCornerElement density1 = voxelDensities[edgeStartIndex];
            VoxelCornerElement density2 = voxelDensities[edgeEndIndex];
            return VertexInterpolate(corner1, corner2, density1, density2, isolevel);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetVertexNormal(int3 voxelLocalPosition){
            if(voxelLocalPosition.x < 0 || voxelLocalPosition.y < 0 || voxelLocalPosition.z < 0) return new float3(0);
            float nx = (SampleDensity(voxelLocalPosition + new int3(1, 0, 0)) - SampleDensity(voxelLocalPosition - new int3(1, 0, 0)));
            float ny = (SampleDensity(voxelLocalPosition + new int3(0, 1, 0)) - SampleDensity(voxelLocalPosition - new int3(0, 1, 0)));
            float nz = (SampleDensity(voxelLocalPosition + new int3(0, 0, 1)) - SampleDensity(voxelLocalPosition - new int3(0, 0, 1)));
            return math.normalize((new float3(nx,ny,nz)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleDensity(int3 pos){
            return densities[Utils.XyzToIndex(pos + 1, chunkSize + 2)];
        }
        /// <summary>
        /// Calculates the cube index of a single voxel
        /// </summary>
        /// <param name="voxelDensities">The voxel's densities</param>
        /// <param name="isolevel">The density level where a surface will be created. Densities below this will be inside the surface (solid),
        /// and densities above this will be outside the surface (air)</param>
        /// <returns>The calculated cube index</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte CalculateCubeIndex(VoxelCorners<VoxelCornerElement> voxelDensities, float isolevel) {
            float4 voxelDensitiesPart1 = new float4(voxelDensities.Corner1.density, voxelDensities.Corner2.density, voxelDensities.Corner3.density, voxelDensities.Corner4.density);
            float4 voxelDensitiesPart2 = new float4(voxelDensities.Corner5.density, voxelDensities.Corner6.density, voxelDensities.Corner7.density, voxelDensities.Corner8.density);
            int4 p1 = math.select(0, new int4(1, 2, 4, 8), voxelDensitiesPart1 < isolevel);
            int4 p2 = math.select(0, new int4(16, 32, 64, 128), voxelDensitiesPart2 < isolevel);
            return (byte)(math.csum(p1) | math.csum(p2));
        }
    }
    //Chunk noisemap update job, in a ball shape. Could implement more complex logic for this as well.
    [BurstCompile]
    public struct ChunkExplodeJob : IJobParallelFor
    {
        //Chunk's noisemap to edit.
        [NativeDisableContainerSafetyRestriction, WriteOnly] public NativeArray<float> noiseMap;
        //Chunk size + 1 to account for borders
        [ReadOnly] public int size;
        //New density value
        [ReadOnly] public float newDensity;
        //Where the "explosion" happens, in local coordinates relative to chunk
        [ReadOnly] public int3 explosionOrigin;
        //How big of an explosion should happen
        [ReadOnly] public float explosionRange;

        public void Execute(int index)
        {
            var pos = new float3(index / (size * size), index / size % size, index % size);
            if(math.distance(pos, explosionOrigin) < explosionRange)
            {
                noiseMap[index] = newDensity;
            }

        }
    }
    //A job to bake chunk colliders
    public struct ChunkColliderBakeJob : IJob
    {
        [ReadOnly] public int meshId;
        public void Execute()
        {
            Physics.BakeMesh(meshId, false);
        }
    }
    //Calculate noise in jobs
    [BurstCompile]
    public struct NoiseJob : IJobParallelFor
    {
        [ReadOnly] public float surfaceLevel;
        [ReadOnly] public float3 offset;
        [ReadOnly] public float3 seed;
        [ReadOnly] public float ampl;
        [ReadOnly] public float freq;
        [ReadOnly] public int oct;
        [ReadOnly] public int depthMultiplier;
        
        [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction, WriteOnly]
        public NativeArray<float> noiseMap;
        [ReadOnly] public int size;



        public void Execute(int index)
        {
            noiseMap[index] = FinalNoise(Utils.IndexToXyz(index, size) * depthMultiplier);
        }
        float FinalNoise(float3 pos)
        {
            //pos -= depthMultiplier;
            float value = SurfaceNoise2D(pos.x, pos.z);
            value -= pos.y + offset.y - surfaceLevel;
            value += PerlinNoise3D(pos.x, pos.y, pos.z) * math.clamp(value, 0f, -1f);
            value = -value;
            return value;
        }
        float PerlinNoise3D(float x, float y, float z)
        {
            float total = 0;
            var ampl = this.ampl;
            var freq = this.freq;
            for (int i = 0; i < oct; i++)
            {
                total += noise.snoise(math.float3((x + offset.x + seed.x) * freq, (y + offset.y + seed.y) * freq, (z + offset.z + seed.z) * freq) * ampl);

                ampl *= 2;
                freq *= 0.5f;
            }
            //total -= total % 2.5f;
            return total;
        }
        float PerlinNoise3DSnake(float x, float y, float z)
        {
            float total = 0;
            var ampl = this.ampl;
            var freq = this.freq + 0.03f;
            for (int i = 0; i < oct; i++)
            {
                total += noise.snoise(math.float3((x + offset.x + seed.x) * freq, (y + offset.y + seed.y) * freq, (z + offset.z + seed.z) * freq) * ampl);

                ampl *= 2;
                freq *= 0.5f;
            }
            total -= total % 2.5f;
            return total;
        }
        float SurfaceNoise2D(float x, float z)
        {
            float total = 0;
            var ampl = this.ampl;
            var freq = this.freq;
            for (int i = 0; i < oct; i++)
            {
                total += noise.snoise(math.float2((x + offset.x + seed.x) * freq, (z + offset.z + seed.z) * freq)) * ampl;

                ampl *= 2;
                freq *= 0.5f;
            }
            //total = total % 5f;
            return total;
        }
    }

    //Tables used for marching cubes. Taken from https://github.com/Eldemarkki/Marching-Cubes-Terrain
    internal class Tables
    {
        /// <summary>
        /// Lookup table for how the edges should be connected
        /// </summary>
        public static readonly int[] EdgeIndexTable =
        {
            0, 1,
            1, 2,
            2, 3,
            3, 0,
            4, 5,
            5, 6,
            6, 7,
            7, 4,
            0, 4,
            1, 5,
            2, 6,
            3, 7
        };

        /// <summary>
        /// The corners of a voxel
        /// </summary>
        public static readonly int3[] CubeCorners =
        {
            new int3(0, 0, 0),
            new int3(1, 0, 0),
            new int3(1, 0, 1),
            new int3(0, 0, 1),
            new int3(0, 1, 0),
            new int3(1, 1, 0),
            new int3(1, 1, 1),
            new int3(0, 1, 1)
        };

        /// <summary>
        /// The edge table for the marching cubes. Used to determine which edges are intersected by the isosurface
        /// </summary>
        public static readonly int[] EdgeTable =
        {
            0x0, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
            0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
            0x190, 0x99, 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c,
            0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
            0x230, 0x339, 0x33, 0x13a, 0x636, 0x73f, 0x435, 0x53c,
            0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
            0x3a0, 0x2a9, 0x1a3, 0xaa, 0x7a6, 0x6af, 0x5a5, 0x4ac,
            0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
            0x460, 0x569, 0x663, 0x76a, 0x66, 0x16f, 0x265, 0x36c,
            0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
            0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0xff, 0x3f5, 0x2fc,
            0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
            0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x55, 0x15c,
            0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
            0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0xcc,
            0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
            0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc,
            0xcc, 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
            0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c,
            0x15c, 0x55, 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
            0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc,
            0x2fc, 0x3f5, 0xff, 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
            0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c,
            0x36c, 0x265, 0x16f, 0x66, 0x76a, 0x663, 0x569, 0x460,
            0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac,
            0x4ac, 0x5a5, 0x6af, 0x7a6, 0xaa, 0x1a3, 0x2a9, 0x3a0,
            0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c,
            0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x33, 0x339, 0x230,
            0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c,
            0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x99, 0x190,
            0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c,
            0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x0
        };

        /// <summary>
        /// The triangle table for the marching cubes algorithm. Used to determine how the faces should be connected.
        /// </summary>
        public static readonly int[] TriangleTable =
        {
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1,
            3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1,
            3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1,
            3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1,
            9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1,
            1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1,
            9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1,
            2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1,
            8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1,
            9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1,
            4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1,
            3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1,
            1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1,
            4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1,
            4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1,
            9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1,
            1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1,
            5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1,
            2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1,
            9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1,
            0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1,
            2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1,
            10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1,
            4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1,
            5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1,
            5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1,
            9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1,
            0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1,
            1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1,
            10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1,
            8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1,
            2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1,
            7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1,
            9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1,
            2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1,
            11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1,
            9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1,
            5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0,
            11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0,
            11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1,
            1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1,
            9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1,
            5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1,
            2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1,
            0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1,
            5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1,
            6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1,
            0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1,
            3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1,
            6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1,
            5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1,
            1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1,
            10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1,
            6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1,
            1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1,
            8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1,
            7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9,
            3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1,
            5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1,
            0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1,
            9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6,
            8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1,
            5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11,
            0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7,
            6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1,
            10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1,
            10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1,
            8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1,
            1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1,
            3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1,
            0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1,
            10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1,
            0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1,
            3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1,
            6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1,
            9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1,
            8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1,
            3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1,
            6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1,
            0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1,
            10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1,
            10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1,
            1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1,
            2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9,
            7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1,
            7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1,
            2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7,
            1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11,
            11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1,
            8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6,
            0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1,
            7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1,
            10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1,
            2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1,
            6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1,
            7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1,
            2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1,
            1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1,
            10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1,
            10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1,
            0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1,
            7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1,
            6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1,
            8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1,
            9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1,
            6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1,
            1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1,
            4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1,
            10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3,
            8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1,
            0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1,
            1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1,
            8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1,
            10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1,
            4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3,
            10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1,
            5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1,
            11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1,
            9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1,
            6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1,
            7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1,
            3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6,
            7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1,
            9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1,
            3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1,
            6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8,
            9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1,
            1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4,
            4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10,
            7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1,
            6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1,
            3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1,
            0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1,
            6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1,
            1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1,
            0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10,
            11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5,
            6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1,
            5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1,
            9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1,
            1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8,
            1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6,
            10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1,
            0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1,
            5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1,
            10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1,
            11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1,
            0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1,
            9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1,
            7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2,
            2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1,
            8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1,
            9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1,
            9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2,
            1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1,
            9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1,
            9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1,
            5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1,
            0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1,
            10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4,
            2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1,
            0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11,
            0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5,
            9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1,
            5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1,
            3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9,
            5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1,
            8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1,
            0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1,
            9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1,
            0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1,
            1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1,
            3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4,
            4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1,
            9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3,
            11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1,
            11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1,
            2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1,
            9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7,
            3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10,
            1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1,
            4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1,
            4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1,
            0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1,
            3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1,
            3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1,
            0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1,
            9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1,
            1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
        };
    }
}
