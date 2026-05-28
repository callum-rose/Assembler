using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelToolkit
{
    public struct FindFitJob : IJob
    {
        [ReadOnly] public TextureHandle Target;
        [ReadOnly] public TextureHandle Candidate;

        [NativeDisableContainerSafetyRestriction] public NativeSlice<float3x3> Transformation;
        [NativeDisableContainerSafetyRestriction] public NativeSlice<bool> Result;
        
        public void Execute()
        {
            Result[0] = false;

            var iterator = new TextureHandle.TransformationIterator();
            iterator.Reset(ref Candidate);

            do
            {
                var candidateSize = Candidate.Size;
                if (math.any(candidateSize > Target.Size))
                    continue;

                var hashWindow = new RollingHashWindow(Target.Data, Target.Size,
                    new(0, 0),
                    candidateSize - 1);

                var maxIndex = Target.Size - candidateSize;

                for (var yTarget = 0; yTarget <= maxIndex.y; yTarget++)
                {
                    var isOdd = yTarget % 2 != 0;
                    var moveDirection = isOdd ? -1 : 1;
                    var xStart = isOdd ? maxIndex.x : 0;
                    var xEnd = (isOdd ? -1 : maxIndex.x + 1);

                    for (var xTarget = xStart; xTarget != xEnd; xTarget += moveDirection)
                    {
                        var currentHash = hashWindow.Hash;
                        hashWindow.MoveHorizontally(moveDirection);

                        if (currentHash != Candidate.Hash)
                            continue;

                        do
                        {
                            var found = true;
                            for (var x = 0; x < candidateSize.x; x++)
                            {
                                for (var y = 0; y < candidateSize.y; y++)
                                {
                                    var targetColor = Target[new int2(xTarget, yTarget) + new int2(x, y)];
                                    var potentialColor = Candidate[new int2(x, y)];
                                    if (potentialColor == targetColor)
                                        continue;

                                    found = false;
                                    goto cycleEnd;
                                }
                            }

                            cycleEnd:
                            if (!found)
                                continue;

                            var transformation = Candidate.CurrentTransformation;

                            var uvShift = new float3x3(
                                Candidate.Size.x / (float)Target.Size.x, 0.0f, xTarget / (float)Target.Size.x,
                                0.0f, Candidate.Size.y / (float)Target.Size.y, yTarget / (float)Target.Size.y,
                                0.0f, 0.0f, 1.0f);

                            var uvTransformation = math.mul(uvShift, transformation.UVTransformationMatrix);

                            Transformation[0] = uvTransformation;
                            Result[0] = true;

                            return;
                        } while (iterator.MoveNextSubTransformation(ref Candidate));
                    }

                    hashWindow.MoveVertically();
                }
            } while (iterator.MoveNext(ref Candidate));
        }
    }
}