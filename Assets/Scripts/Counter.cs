using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace WorldGeneration
{
    public unsafe struct Counter : IDisposable
    {
        /// <summary>
        /// The allocator for the counter
        /// </summary>
        private Allocator allocator;

        /// <summary>
        /// The pointer to the value
        /// </summary>
        [NativeDisableUnsafePtrRestriction] private int* _counter;

        /// <summary>
        /// The counter's value
        /// </summary>
        public int Count
        {
            get => *_counter;
            set => (*_counter) = value;
        }

        /// <summary>
        /// The constructor
        /// </summary>
        /// <param name="allocator">What type of allocator to use</param>
        public Counter(Allocator allocator)
        {
            this.allocator = allocator;
            _counter = (int*)UnsafeUtility.Malloc(sizeof(int), 4, allocator);
            Count = 0;
        }

        /// <summary>
        /// Increments the count by 1
        /// </summary>
        /// <returns>The original count</returns>
        public int Increment()
        {
            return Interlocked.Increment(ref *_counter) - 1;
        }

        /// <summary>
        /// Disposes the counter
        /// </summary>
        public void Dispose()
        {
            UnsafeUtility.Free(_counter, allocator);
        }
    }
    public static class Utils
    {
        public static unsafe void CopyToFast<T>(this NativeSlice<T> source, T[] target) where T : struct
        {
            if (target == null)
            {
                throw new NullReferenceException(nameof(target) + " is null");
            }

            int nativeArrayLength = source.Length;
            if (target.Length < nativeArrayLength)
            {
                throw new IndexOutOfRangeException(nameof(target) + " is shorter than " + nameof(source));
            }

            int byteLength = source.Length * Marshal.SizeOf(default(T));
            void* managedBuffer = UnsafeUtility.AddressOf(ref target[0]);
            void* nativeBuffer = source.GetUnsafePtr();
            Buffer.MemoryCopy(nativeBuffer, managedBuffer, byteLength, byteLength);
        }
        public static int3 FloorToMultipleOfX(this float3 n, int x)
        {
            return (int3)(math.floor(n / x) * x);
        }
        public static int3 Mod(this int3 n, int x)
        {
            return (n % x + x) % x;
        }
    }
}
