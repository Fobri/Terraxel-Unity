using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

using System.Runtime.CompilerServices;

namespace DataStructures
{
    
    public enum QuadrantLocations { NE_TOP, NW_TOP, SW_TOP, SE_TOP, NE_BOT, NW_BOT, SW_BOT, SE_BOT}
    public struct NeighbourDensities{
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<sbyte> first;
        
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<sbyte> second;
        
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<sbyte> third;
        
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<sbyte> fourth;

        public NativeArray<sbyte> this[int index]{
            get{
                switch(index){
                    case 0: return first;
                    case 1: return second;
                    case 2: return third;
                    case 3: return fourth;
                    
                    default: throw new System.IndexOutOfRangeException();
                }
            }set{
                switch(index){
                    case 0: first = value; break;
                    case 1: second = value; break;
                    case 2: third = value; break;
                    case 3: fourth = value; break;
                    
                    default: throw new System.IndexOutOfRangeException();
                }
            }
        }
    }
    public struct VertexData{
        public float3 vertex;
        public float3 normal;
        public VertexData(float3 vertex, float3 normal){
            this.vertex = vertex;
            this.normal = normal;
        }
    }
    public struct MeshData : System.IDisposable{
        
        public NativeArray<VertexData> vertexBuffer;
        public NativeArray<ushort> indexBuffer;
        public NativeArray<sbyte> densityBuffer;

        public MeshData(NativeArray<VertexData> vertexBuffer, NativeArray<ushort> indexBuffer, NativeArray<sbyte> densityBuffer){
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
            this.densityBuffer = densityBuffer;
        }
        public bool IsCreated{
            get{
                return vertexBuffer.IsCreated;
            }
        }
        public void Dispose(){
            densityBuffer.Dispose();
            indexBuffer.Dispose();
            vertexBuffer.Dispose();
        }
    }
    public struct ushort3{
        ushort x;
        ushort y;
        ushort z;
        public ushort3(ushort value){
            x = value;
            y = value;
            z = value;
        }
        public ushort this[int index]{
            get{
                switch(index){
                    case 1: return x;
                    case 2: return y;
                    case 3: return z;
                    
                    default: throw new System.IndexOutOfRangeException();
                }
            }set{
                switch(index){
                    case 1: x = value; break;
                    case 2: y = value; break;
                    case 3: z = value; break;
                    
                    default: throw new System.IndexOutOfRangeException();
                }
            }
        }
    }
    public struct VoxelCornerElement{
        public sbyte density;
        public VoxelCornerElement(sbyte density){
            this.density = density;
        }
    }
    public struct RegularCell
    {
        private byte geometryCounts;

        // High nibble is vertex count, low nibble is triangle count.
        private byte[] vertexIndex;
        // Groups of 3 indexes giving the triangulation.

        public RegularCell(byte gc, byte[] vi)
        {
            geometryCounts = gc;
            vertexIndex = vi;
        }

        public long GetVertexCount()
        {
            return (geometryCounts >> 4);
        }

        public long GetTriangleCount()
        {
            return (geometryCounts & 0x0F);
        }

        public byte[] Indizes()
        {
            return vertexIndex;
        }
    };
    public class Utils{
        public static int XyzToIndex(int x, int y, int z, int size)
        {
            return z * size * size + y * size + x;
        }
        public static int XyzToIndex(int3 index, int size)
        {
            return XyzToIndex(index.x, index.y, index.z, size);
        }
        public static int3 IndexToXyz(int index, int size)
        {
            int3 position = new int3(
                index % size,
                index / size % size,
                index / (size * size));
            return position;
        }
        public static readonly Dictionary<int3, QuadrantLocations[]> ChunkRelativePositionToQuadrantLocations = new Dictionary<int3, QuadrantLocations[]>(){
        {new int3(1, 0, 0), new QuadrantLocations[]{QuadrantLocations.SW_TOP, QuadrantLocations.SE_TOP, QuadrantLocations.SW_BOT, QuadrantLocations.SE_BOT}},
        {new int3(-1, 0, 0), new QuadrantLocations[]{QuadrantLocations.NW_TOP, QuadrantLocations.NE_TOP, QuadrantLocations.NW_BOT, QuadrantLocations.NE_BOT}},
        {new int3(0, -1, 0), new QuadrantLocations[]{QuadrantLocations.NW_TOP, QuadrantLocations.NE_TOP, QuadrantLocations.SW_TOP, QuadrantLocations.SE_TOP}},
        {new int3(0, 1, 0), new QuadrantLocations[]{QuadrantLocations.NW_BOT, QuadrantLocations.NE_BOT, QuadrantLocations.SW_BOT, QuadrantLocations.SE_BOT}},
        {new int3(0, 0, 1), new QuadrantLocations[]{QuadrantLocations.SE_TOP, QuadrantLocations.NE_TOP, QuadrantLocations.SE_BOT, QuadrantLocations.NE_BOT}},
        {new int3(0, 0, -1), new QuadrantLocations[]{QuadrantLocations.SW_TOP, QuadrantLocations.NW_TOP, QuadrantLocations.SW_BOT, QuadrantLocations.NW_BOT}}};

        public static string DirectionMaskToString(byte dirMask){
            string value = "";
            if((dirMask & 0b_0000_0001) == 0b_0000_0001) value += "Front, ";
            if((dirMask & 0b_0000_0010) == 0b_0000_0010) value += "Back, ";
            if((dirMask & 0b_0000_0100) == 0b_0000_0100) value += "Up, ";
            if((dirMask & 0b_0000_1000) == 0b_0000_1000) value += "Down, ";
            if((dirMask & 0b_0001_0000) == 0b_0001_0000) value += "Right, ";
            if((dirMask & 0b_0010_0000) == 0b_0010_0000) value += "Left, ";
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x3 DecodeNeighborMask(byte dirMask){
            return new bool2x3(new bool2((dirMask & 0b_0000_0001) == 0b_0000_0001, (dirMask & 0b_0000_0010) == 0b_0000_0010), 
                                                    new bool2((dirMask & 0b_0000_0100) == 0b_0000_0100, (dirMask & 0b_0000_1000) == 0b_0000_1000),
                                                    new bool2((dirMask & 0b_0010_0000) == 0b_0010_0000, (dirMask & 0b_0001_0000) == 0b_0001_0000));
            
        }
    }
}