using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelToolkit
{
	[BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
	public struct CountQuadsJob : IJob
	{
		[ReadOnly] private NativeArray<int> Count;
		[ReadOnly] private NativeArray<Quad> Quads;
		[ReadOnly] private NativeArray<int> Info;

		public void Execute()
		{
			var area = 0;

			for (var countIndex = 0; countIndex < 2; countIndex++)
			{
				var count = Count[countIndex];
				for (var index = 0; index < count; index++)
				{
					var quad = Quads[index];
					var start = new int3(quad.StartX, quad.StartY, quad.StartZ);
					var end = new int3(quad.EndX, quad.EndY, quad.EndZ);
					var difference = end - start;
					area += difference.x * difference.y * difference.z;
				}
			}

			Info[0] = area;
		}
	}
}