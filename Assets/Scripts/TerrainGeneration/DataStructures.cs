using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

namespace DataStructures
{

    
    public struct DensityResultData{
        
        [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction, WriteOnly]
        public NativeArray<sbyte> densityMap;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isEmpty;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeReference<bool> isFull;
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

        public MeshData(NativeArray<VertexData> vertexBuffer, NativeArray<ushort> indexBuffer){
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
        }
        public bool IsCreated{
            get{
                return vertexBuffer.IsCreated;
            }
        }
        public void Dispose(){
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