using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelToolkit
{
    public static class RollingHashCalculator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CalculateHash(NativeArray<int> data, int2 startPosition, int2 endPosition, int2 size)
        {
            var hash = 0L;
            for (var y = startPosition.y; y <= endPosition.y; y++)
            for (var x = startPosition.x; x <= endPosition.x; x++)
                hash += data[math.dot(new int2(1, size.x), new int2(x, y))];

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MoveHorizontally(ref long hash, NativeArray<int> data, int2 position, int2 endPosition, int2 size, int direction)
        {
            var index = direction > 0 ? position.x : endPosition.x;
            var newIndex = (direction > 0 ? endPosition.x : position.x) + direction;
            for (var y = position.y; y <= endPosition.y; y++)
            {
                var currentDataIndex = math.dot(new int2(1, size.x), new int2(index, y));
                var oldColumnValue = data[currentDataIndex];
                var newDataIndex = math.dot(new int2(1, size.x), new int2(newIndex, y));
                var newColumnValue = data[newDataIndex];
                hash = hash - oldColumnValue + newColumnValue;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MoveVertically(ref long hash, NativeArray<int> data, int2 position, int2 endPosition, int2 size)
        {
            var firstIndex = position.y;
            var newIndex = endPosition.y + 1;
            for (var x = position.x; x <= endPosition.x; x++)
            {
                var oldValue = data[math.dot(new int2(1, size.x), new int2(x, firstIndex))];
                var newValue = data[math.dot(new int2(1, size.x), new int2(x, newIndex))];
                hash = hash - oldValue + newValue;
            }
        }
    }
}