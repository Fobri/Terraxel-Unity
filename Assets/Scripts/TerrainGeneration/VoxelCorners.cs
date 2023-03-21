using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Terraxel.DataStructures
{
    public struct VoxelCorners
    {
        /// <summary>
        /// The first corner
        /// </summary>
        public sbyte Corner1;

        /// <summary>
        /// The second corner
        /// </summary>
        public sbyte Corner2;

        /// <summary>
        /// The third corner
        /// </summary>
        public sbyte Corner3;

        /// <summary>
        /// The fourth corner
        /// </summary>
        public sbyte Corner4;

        /// <summary>
        /// The fifth corner
        /// </summary>
        public sbyte Corner5;

        /// <summary>
        /// The sixth corner
        /// </summary>
        public sbyte Corner6;

        /// <summary>
        /// The seventh corner
        /// </summary>
        public sbyte Corner7;

        /// <summary>
        /// The eighth corner
        /// </summary>
        public sbyte Corner8;

        /// <summary>
        /// The indexer for the voxel corners
        /// </summary>
        /// <param name="index">The corner's index</param>
        /// <exception cref="System.IndexOutOfRangeException">Thrown when index is more than 7.</exception>
        public sbyte this[int index]
        {
            get
            {
                unsafe{
                    fixed(sbyte* ptr = &Corner1){
                        return ptr[index];
                    }
                }
                /*switch (index)
                {
                    case 0: return Corner1;
                    case 1: return Corner2;
                    case 2: return Corner3;
                    case 3: return Corner4;
                    case 4: return Corner5;
                    case 5: return Corner6;
                    case 6: return Corner7;
                    case 7: return Corner8;
                    default: throw new System.IndexOutOfRangeException();
                }*/
            }
            set
            {
                unsafe{
                    fixed(sbyte* ptr = &Corner1){
                        ptr[index] = value;
                    }
                }
                /*switch (index)
                {
                    case 0:
                        Corner1 = value;
                        break;
                    case 1:
                        Corner2 = value;
                        break;
                    case 2:
                        Corner3 = value;
                        break;
                    case 3:
                        Corner4 = value;
                        break;
                    case 4:
                        Corner5 = value;
                        break;
                    case 5:
                        Corner6 = value;
                        break;
                    case 6:
                        Corner7 = value;
                        break;
                    case 7:
                        Corner8 = value;
                        break;
                    default: throw new System.IndexOutOfRangeException();
                }*/
            }
        }
    }
}