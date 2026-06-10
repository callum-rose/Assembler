using System;
using System.Collections.Generic;
using Assembler.Libraries;
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
			["vector list"] = typeof(List<Vector3>),
			["int list"] = typeof(List<int>),
			["float list"] = typeof(List<float>),
			["bool list"] = typeof(List<bool>),
			["string list"] = typeof(List<string>),
			["colour list"] = typeof(List<Color>),
			["record"] = typeof(Record),
			["record list"] = typeof(List<Record>)
		};
	}
}
