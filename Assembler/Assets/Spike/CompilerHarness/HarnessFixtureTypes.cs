namespace Spike.CompilerHarness
{
	// Verbatim ports of Assets/Tests/Compiler/CompilerTestTypes.cs. They can't be reused directly:
	// Tests.Compiler is `includePlatforms: ["Editor"]` with a UNITY_INCLUDE_TESTS define constraint, so
	// it cannot enter a player build. Keep these byte-for-byte equivalent to the originals — a divergence
	// here would change what the ported cases actually measure.

	public class CoercionTarget
	{
		public static float Shared;

		public float Value { get; set; }
	}

	public class TestVector3
	{
		public double x;
		public double y;
		public double z;

		public TestVector3(double x, double y, double z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
	}

	public class TestTransform
	{
		public TestVector3 position = new(0, 0, 0);
	}
}
