using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelToolkit
{
    public struct RollingHashWindow
    {
        private NativeArray<int> texture;
        private int2 windowStart;
        private int2 windowEnd;
        private int2 textureSize;
        private long hash;

        public long Hash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => hash;
        }

        public RollingHashWindow(NativeArray<int> texture, int2 textureSize, int2 windowStart, int2 windowEnd)
        {
            this.texture = texture;
            this.windowStart = windowStart;
            this.windowEnd = windowEnd;
            this.textureSize = textureSize;
            hash = RollingHashCalculator.CalculateHash(texture, windowStart, windowEnd, textureSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveHorizontally(int direction)
        {
            var newStart = windowStart + new int2(direction, 0);
            var newEnd = windowEnd + new int2(direction, 0);

            var location = new int4(newStart, newEnd);
            if (math.any(location >= new int4(textureSize, textureSize)) || 
                math.any(location < 0))
                return;
            
            RollingHashCalculator.MoveHorizontally(ref hash, texture, windowStart, windowEnd, textureSize, direction);
            windowStart = newStart;
            windowEnd = newEnd;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveVertically()
        {
            var newStart = windowStart + new int2(0, 1);
            var newEnd = windowEnd + new int2(0, 1);

            var location = new int4(newStart, newEnd);
            if (math.any(location >= new int4(textureSize, textureSize)) || 
                math.any(location < 0))
                return;
            
            RollingHashCalculator.MoveVertically(ref hash, texture, windowStart, windowEnd, textureSize);
            windowStart = newStart;
            windowEnd = newEnd;
        }
    }
}