using System;
using System.Collections.Generic;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	// Covers the runtime-spawn counterpart of #404 for lists: a list-typed value (e.g. a vector-list
	// patrol route read via `!var` in a spawner's Parameters) is resolved to a concrete List<T> before
	// template instantiation. AdaptRuntimeParameter must convert it to a TypedListValue that
	// CreateValueSource can rewrap as a typed constant list, rather than throwing
	// "Cannot adapt runtime parameter".
	public class RuntimeListParameterTests
	{
		private static readonly ValueConversion Runtime = new() { AllowRuntimeTypes = true };

		private static TransformContext EmptyContext() =>
			new(new List<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				new Dictionary<string, Type>(),
				new Dictionary<Type, System.Reflection.MethodInfo>(),
				new InlineExpressionAccumulator(),
				RecordSchemaRegistry.Empty);

		[Test]
		public void ToAssemblerValue_RuntimeVectorList_ConvertsToTypedListValue()
		{
			var route = new List<Vector3> { new(1, 2, 3), new(4, 5, 6) };

			var converted = AssemblerValueConverter.ToAssemblerValue(route, Runtime);

			Assert.IsInstanceOf<TypedListValue>(converted);
			var typed = (TypedListValue)converted;
			Assert.AreEqual(typeof(Vector3), typed.ElementType);
			Assert.AreEqual(2, typed.Items.Count);
			Assert.AreEqual(new Vector3(1, 2, 3), ((Vector3Value)typed.Items[0]).Value);
			Assert.AreEqual(new Vector3(4, 5, 6), ((Vector3Value)typed.Items[1]).Value);
		}

		[Test]
		public void ToAssemblerValue_RuntimeColorList_ConvertsToTypedListValue()
		{
			var palette = new List<Color> { Color.red, Color.blue };

			var converted = AssemblerValueConverter.ToAssemblerValue(palette, Runtime);

			Assert.IsInstanceOf<TypedListValue>(converted);
			var typed = (TypedListValue)converted;
			Assert.AreEqual(typeof(Color), typed.ElementType);
			Assert.AreEqual(Color.red, ((ColorValue)typed.Items[0]).Value);
		}

		[Test]
		public void ToAssemblerValue_RuntimePrimitiveLists_ConvertToTypedListValues()
		{
			Assert.AreEqual(typeof(int),
				((TypedListValue)AssemblerValueConverter.ToAssemblerValue(new List<int> { 1, 2 }, Runtime)).ElementType);
			Assert.AreEqual(typeof(float),
				((TypedListValue)AssemblerValueConverter.ToAssemblerValue(new List<float> { 1f }, Runtime)).ElementType);
			Assert.AreEqual(typeof(bool),
				((TypedListValue)AssemblerValueConverter.ToAssemblerValue(new List<bool> { true }, Runtime)).ElementType);
			Assert.AreEqual(typeof(string),
				((TypedListValue)AssemblerValueConverter.ToAssemblerValue(new List<string> { "a" }, Runtime)).ElementType);
		}

		[Test]
		public void CreateValueSource_RuntimeVectorList_RewrapsAsTypedConstantList()
		{
			var route = new List<Vector3> { new(1, 2, 3), new(4, 5, 6) };
			var converted = AssemblerValueConverter.ToAssemblerValue(route, Runtime);

			var source = ValueSourceFactory.CreateValueSource<List<Vector3>>(EmptyContext(), converted);

			Assert.IsInstanceOf<ConstantSource<List<Vector3>>>(source);
			CollectionAssert.AreEqual(route, ((ConstantSource<List<Vector3>>)source).Value);
		}
	}
}
