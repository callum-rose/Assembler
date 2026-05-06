// using Assembler.Definitions;
// using Assembler.Generators.Attributes;
//
// namespace Operations
// {
// 	[GenerateEnumFromMembers(typeof(Func<VectorDef, VectorDef, VectorDef>))]
// 	public static class VectorVectorVector
// 	{
// 		public static readonly Func<VectorDef, VectorDef, VectorDef> Add = (a, b) => new VectorDef(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
// 		public static readonly Func<VectorDef, VectorDef, VectorDef> Subtract = (a, b) =>
// 			new VectorDef(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
// 		public static readonly Func<VectorDef, VectorDef, VectorDef> Dot = (a, b) => new VectorDef(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
//
// 		public static readonly Func<VectorDef, VectorDef, VectorDef> Cross = (a, b) => new VectorDef(
// 			a.Y * b.Z - a.Z * b.Y,
// 			a.Z * b.X - a.X * b.Z,
// 			a.X * b.Y - a.Y * b.X
// 		);
//
// 		public static readonly Func<VectorDef, VectorDef, VectorDef> Max =
// 			(a, b) => new VectorDef(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
//
// 		public static readonly Func<VectorDef, VectorDef, VectorDef> Min =
// 			(a, b) => new VectorDef(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
// 	}
// }