using Unity.Mathematics;

namespace DataStructures
{
    public struct VertexData{
        public float3 vertex;
        public float3 normal;
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
    }
}