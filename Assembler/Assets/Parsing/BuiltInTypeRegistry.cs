using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class BuiltInTypeRegistry
	{
		public readonly static IReadOnlyDictionary<string, Type> Default = new Dictionary<string, Type>
		{
			["float"] = typeof(float),
			["int"] = typeof(int),
			["string"] = typeof(string),
			["bool"] = typeof(bool),
			["vector"] = typeof(Vector3),
			["colour"] = typeof(Color),
			["vector list"] = typeof(IList<Vector3>),
			["int list"] = typeof(IList<int>),
			["float list"] = typeof(IList<float>),
			["bool list"] = typeof(IList<bool>),
			["string list"] = typeof(IList<string>),
			["colour list"] = typeof(IList<Color>)
		};
	}
}
