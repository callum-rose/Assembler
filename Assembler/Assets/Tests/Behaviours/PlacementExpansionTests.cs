using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assembler.Behaviours.AI;
using Assembler.Behaviours.UI;
using Assembler.Building;
using Assembler.Compiler.Compiler;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tests.Behaviours
{
	public class PlacementExpansionTests
	{
		private GameObject _root = null!;
		private EntityTransformRegistry _entityTransforms = null!;
		private GameEntityFactory _factory = null!;

		[SetUp]
		public void SetUp()
		{
			_root = new GameObject("Game");
			_entityTransforms = new EntityTransformRegistry();

			var clock = new RealtimeGameClock();
			var typeRegistry = BuiltInTypeRegistry.Default;

			var parseContext = new TransformContext(
				Array.Empty<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				typeRegistry,
				new Dictionary<Type, MethodInfo>(),
				new InlineExpressionAccumulator());

			// A trivial template: one entity with no behaviours, so Create produces a bare GameObject. Its
			// position/rotation are ConstantSource(zero) exactly as the Transformer builds a template with no
			// explicit Position/Rotation (an absent value type falls back to a zero constant, never None).
			var template = new ConcreteEntityInfo(
				"pill",
				Array.Empty<string>(),
				new ConstantSource<Vector3>(Vector3.zero),
				new ConstantSource<Vector3>(Vector3.zero),
				Array.Empty<BehaviourInfo>(),
				Array.Empty<ValueInfo>(),
				Array.Empty<ChildEntityInfo>());

			_factory = new GameEntityFactory(
				new VariableRegistry(),
				new CompiledExpressionsRegistry(typeRegistry, new ExpressionMethodCompiler()),
				new Assembler.Building.BehaviourRegistry(),
				new AssetRegistry(),
				new StringTableRegistry(new LocaleSettings("en")),
				_entityTransforms,
				new EntityQueryService(),
				new LineOfSightService(),
				new ExclusiveGroupRegistry(clock),
				clock,
				new Dictionary<string, EntityInfo> { ["pill"] = template },
				parseContext,
				_root.transform,
				ControlsInfo.Empty,
				ScriptableObject.CreateInstance<InputActionAsset>(),
				ScriptableObject.CreateInstance<UiPrefabLibrary>());
		}

		[TearDown]
		public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

		private static PlacementInfo Placement(string id, IEnumerable<Vector3> positions) =>
			new(id,
				"pill",
				new ConstantSource<List<Vector3>>(positions.ToList()),
				None<Vector3>.Instance,
				new Dictionary<string, AssemblerValue>(),
				Array.Empty<string>());

		[Test]
		public void ExpandsToOneInfoPerPositionWithSuffixedIds()
		{
			var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) };

			var instances = _factory.ExpandPlacement(Placement("field pills", positions));

			Assert.AreEqual(3, instances.Count);
			CollectionAssert.AreEqual(
				new[] { "field pills_0", "field pills_1", "field pills_2" },
				instances.Select(i => i.Id).ToArray());
			CollectionAssert.AreEqual(positions,
				instances.Select(i => ((ConstantSource<Vector3>)i.InitialPosition).Value).ToArray());
		}

		[Test]
		public void EmptyPositionListCreatesNothing()
		{
			var instances = _factory.ExpandPlacement(Placement("none", Array.Empty<Vector3>()));
			Assert.AreEqual(0, instances.Count);
		}

		[Test]
		public void UnknownTemplateThrows()
		{
			var placement = new PlacementInfo("orphans",
				"missing",
				new ConstantSource<List<Vector3>>(new List<Vector3> { Vector3.zero }),
				None<Vector3>.Instance,
				new Dictionary<string, AssemblerValue>(),
				Array.Empty<string>());

			Assert.Throws<InvalidOperationException>(() => _factory.ExpandPlacement(placement));
		}

		[Test]
		public void CreateRegistersEachInstanceAsLiveGameObjectWithUniqueId()
		{
			var positions = new[] { new Vector3(-1, -3, 0), new Vector3(0, -3, 0) };

			foreach (var instance in _factory.ExpandPlacement(Placement("border pills", positions)))
			{
				_factory.Create(instance);
			}

			for (var i = 0; i < positions.Length; i++)
			{
				var transform = _entityTransforms.Get($"border pills_{i}");
				Assert.IsNotNull(transform);
				Assert.AreEqual(positions[i], transform.position);
				Assert.AreSame(_root.transform, transform.parent);
			}
		}
	}
}
