using System.Runtime.InteropServices;

namespace VoxelToolkit
{
	[StructLayout(LayoutKind.Explicit)]
	public struct Color32
	{
		[FieldOffset(0)]
		public int RGBA;

		[FieldOffset(0)] 
		public byte R;
		[FieldOffset(1)] 
		public byte G;
		[FieldOffset(2)] 
		public byte B;
		[FieldOffset(3)] 
		public byte A;

		public Color32(byte r, byte g, byte b, byte a)
		{
			this.RGBA = default;
			this.R = r;
			this.G = g;
			this.B = b;
			this.A = a;
		}
	}
}