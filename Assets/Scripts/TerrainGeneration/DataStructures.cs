using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace DataStructures
{
    
    public enum QuadrantLocations { NE_TOP, NW_TOP, SW_TOP, SE_TOP, NE_BOT, NW_BOT, SW_BOT, SE_BOT}
    public struct NeighbourDensities{
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<float> first;
        
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<float> second;
        
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<float> third;
        
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<float> fourth;

        public NativeArray<float> this[int index]{
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
    }
    public struct MeshData : System.IDisposable{
        
        public NativeArray<VertexData> vertexBuffer;
        public NativeArray<ushort> indexBuffer;
        public NativeArray<float> densityBuffer;

        public MeshData(NativeArray<VertexData> vertexBuffer, NativeArray<ushort> indexBuffer, NativeArray<float> densityBuffer){
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
    public struct ushort4{
        public ushort x;
        public ushort y;
        public ushort z;
        public ushort w;
        public ushort4(ushort x, ushort y, ushort z, ushort w){
            this.x = x; 
            this.y = y;
            this.z = z;
            this.w = w;
        }
        public ushort4(int value){
            this.x = (ushort)value;
            this.y = (ushort)value;
            this.z = (ushort)value; 
            this.w = (ushort)value;
        }
    }
    public struct VoxelCornerElement{
        public float density;
        public float3 normal;
        public VoxelCornerElement(float density, float3 normal){
            this.density = density;
            this.normal = normal;
        }
    }
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
        {new int3(0, 1, 0), new QuadrantLocations[]{QuadrantLocations.NW_TOP, QuadrantLocations.NE_TOP, QuadrantLocations.SW_TOP, QuadrantLocations.SE_TOP}},
        {new int3(0, -1, 0), new QuadrantLocations[]{QuadrantLocations.NW_BOT, QuadrantLocations.NE_BOT, QuadrantLocations.SW_BOT, QuadrantLocations.SE_BOT}},
        {new int3(0, 0, 1), new QuadrantLocations[]{QuadrantLocations.SE_TOP, QuadrantLocations.NE_TOP, QuadrantLocations.SE_BOT, QuadrantLocations.NE_BOT}},
        {new int3(0, 0, -1), new QuadrantLocations[]{QuadrantLocations.NW_TOP, QuadrantLocations.SW_TOP, QuadrantLocations.NW_BOT, QuadrantLocations.SW_BOT}}};

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
    }
}