using System;
using System.Collections.Generic;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	// Covers #404: an !asset passed as a spawner Parameter is resolved to a loaded UnityEngine.Object
	// before template instantiation. AdaptRuntimeParameter must carry it through (as a RuntimeObjectValue)
	// and CreateValueSource must rewrap it as a typed ConstantSource, rather than throwing
	// "Cannot adapt runtime parameter".
	public class RuntimeAssetParameterTests
	{
		private static TransformContext EmptyContext() =>
			new(new List<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				new Dictionary<string, Type>(),
				new Dictionary<Type, System.Reflection.MethodInfo>(),
				new InlineExpressionAccumulator(),
				RecordSchemaRegistry.Empty);

		[Test]
		public void ToAssemblerValue_RuntimeUnityObject_CarriedThroughAsRuntimeObjectValue()
		{
			var mesh = new Mesh();

			var converted = AssemblerValueConverter.ToAssemblerValue(
				mesh, new ValueConversion { AllowRuntimeTypes = true });

			Assert.IsInstanceOf<RuntimeObjectValue>(converted);
			Assert.AreSame(mesh, ((RuntimeObjectValue)converted).Value);
		}

		[Test]
		public void CreateValueSource_RuntimeObjectValue_RewrapsAsTypedConstant()
		{
			var mesh = new Mesh();
			var value = new RuntimeObjectValue(mesh);

			var source = ValueSourceFactory.CreateValueSource<Mesh>(EmptyContext(), value);

			Assert.IsInstanceOf<ConstantSource<Mesh>>(source);
			Assert.AreSame(mesh, ((ConstantSource<Mesh>)source).Value);
		}

		[Test]
		public void CreateValueSource_RuntimeObjectValue_WrongTargetType_Throws()
		{
			var mesh = new Mesh();
			var value = new RuntimeObjectValue(mesh);

			Assert.Throws<ParsingException>(
				() => ValueSourceFactory.CreateValueSource<Sprite>(EmptyContext(), value));
		}
	}
}
