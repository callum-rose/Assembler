using System.Runtime.CompilerServices;
using Unity.Collections;

namespace VoxelToolkit
{
    public struct BooleanArray
    {
        private NativeArray<byte> values;

        private readonly int length;

        public BooleanArray(int count, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            var entriesCount = (count / 8) + 1;
            length = count;

            values = new NativeArray<byte>(entriesCount, allocator, options);
        }
        
        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var elementIndex = index / 8;
                var shift = index - elementIndex * 8;

                return (values[elementIndex] & (1 << shift)) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                var elementIndex = index / 8;
                var shift = index - elementIndex * 8;

                var current = values[elementIndex];
                var valueToBeSet = value ? 1 : 0;
                valueToBeSet <<= shift;
                var mask = ~(1 << shift);

                values[elementIndex] = (byte)((current & mask) | valueToBeSet);
            }
        }

        public int Length => length;
        
        public void Dispose()
        {
            if (values.IsCreated)
                values.Dispose();
        }
    }
}