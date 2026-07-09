namespace Tests.Compiler
{
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
