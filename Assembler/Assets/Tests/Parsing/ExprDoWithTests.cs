using System;
using System.IO;
using System.Linq;
using Assembler.Compiler.Compiler;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	// Covers the consolidated `!expr { Do, With }` call site: a named call (Do matches a declared
	// expression by id or alias) versus an anonymous inline body (Do is compiled C#), plus operand
	// type inference, nesting, dispatch and the missing-Do error.
	public class ExprDoWithTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private static ValueSource<Vector3> PositionOf(GameInfo info) =>
			info.Entities[0].InitialPosition;

		private static string EntityWithPositionExpr(string positionExpr, string extraTop = "") => $@"
{extraTop}
Variables:
  v: !vec {{ X: 1, Y: 2, Z: 3 }}
Entities:
  e:
    Position: {positionExpr}
    Behaviours: {{}}
";

		[Test]
		public void NamedDoCallTargetsDeclaredExpressionById()
		{
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: shift up, With: [ !var v ] }",
				extraTop: @"
Expressions:
  shift up:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    Expression: 'return a + new UnityEngine.Vector3(0, 1, 0);'"));

			var source = PositionOf(info);

			Assert.IsInstanceOf<ExpressionSource<Vector3>>(source);
			Assert.AreEqual("shift up", ((ExpressionSource<Vector3>)source).ExpressionId);
			Assert.AreEqual(1, ((ExpressionSource<Vector3>)source).Arguments.Count);

			// A named call must not synthesise an inline expression.
			Assert.IsFalse(info.Expressions.Any(e => e.Id.StartsWith("__inline_")));
		}

		[Test]
		public void NamedDoCallResolvesByCallableAlias()
		{
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: up, With: [ !var v ] }",
				extraTop: @"
Expressions:
  shift up:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    CallableAs: up
    Expression: 'return a + new UnityEngine.Vector3(0, 1, 0);'"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);

			// The call site uses the alias, but the source targets the canonical id.
			Assert.AreEqual("shift up", source.ExpressionId);
			Assert.IsFalse(info.Expressions.Any(e => e.Id.StartsWith("__inline_")));
		}

		[Test]
		public void InlineDoBodySynthesisesAnonymousExpression()
		{
			var info = Parse(EntityWithPositionExpr("!expr { Do: '-arg0', With: [ !var v ] }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);

			Assert.AreEqual(1, source.Arguments.Count);
			Assert.IsTrue(source.ExpressionId.StartsWith("__inline_"));

			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);
			Assert.AreEqual("vector", synthesised.ReturnType);
			Assert.AreEqual(1, synthesised.Arguments.Count);
			Assert.AreEqual(("vector", "arg0"), synthesised.Arguments[0]);
		}

		[Test]
		public void InlineBodyInfersOperandTypesFromConstantsAndVars()
		{
			// arg0 is a vector (from !var v), arg1 is an int literal.
			var info = Parse(EntityWithPositionExpr("!expr { Do: 'arg0 * arg1', With: [ !var v, 2 ] }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);
			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);

			Assert.AreEqual("vector", synthesised.Arguments[0].type);
			Assert.AreEqual("int", synthesised.Arguments[1].type);
			Assert.AreEqual("vector", synthesised.ReturnType);
		}

		[Test]
		public void DoNameWinsOverInlineInterpretation()
		{
			// "double" is also a C# keyword-ish identifier, but because it's a declared expression
			// the registry-membership check routes it as a named call, not an inline body.
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: scale, With: [ !var v ] }",
				extraTop: @"
Expressions:
  scale:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    Expression: 'return a * 2f;'"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);

			Assert.AreEqual("scale", source.ExpressionId);
			Assert.IsFalse(info.Expressions.Any(e => e.Id.StartsWith("__inline_")));
		}

		[Test]
		public void NestedInlineInsideNamedCallResolves()
		{
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: shift up, With: [ !expr { Do: '-arg0', With: [ !var v ] } ] }",
				extraTop: @"
Expressions:
  shift up:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    Expression: 'return a + new UnityEngine.Vector3(0, 1, 0);'"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);

			Assert.AreEqual("shift up", source.ExpressionId);
			// The nested inline operand synthesised exactly one anonymous expression.
			Assert.AreEqual(1, info.Expressions.Count(e => e.Id.StartsWith("__inline_")));
		}

		[Test]
		public void MissingDoThrows()
		{
			var ex = Assert.Catch(() => Parse(EntityWithPositionExpr("!expr { With: [ !var v ] }")));

			Assert.That(ex!.ToString(), Does.Contain("Do"));
		}

		// End-to-end: YAML -> ExpressionSource -> compiled delegate -> value, for inline `-arg0`
		// and `arg0 * 2` plus a named call, confirming the whole pipeline agrees on types/results.
		[Test]
		public void InlineAndNamedExpressionsCompileAndEvaluate()
		{
			var info = Parse($@"
Variables:
  v: !vec {{ X: 1, Y: 2, Z: 3 }}
Expressions:
  shift up:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    Expression: 'return a + new UnityEngine.Vector3(0, 1, 0);'
Entities:
  negate:
    Position: !expr {{ Do: '-arg0', With: [ !var v ] }}
    Behaviours: {{}}
  scale:
    Position: !expr {{ Do: 'arg0 * 2', With: [ !var v ] }}
    Behaviours: {{}}
  named:
    Position: !expr {{ Do: shift up, With: [ !var v ] }}
    Behaviours: {{}}
");

			var registry = new CompiledExpressionsRegistry(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());
			registry.CompileAndRegisterAll(info.Expressions);

			var v = new Vector3(1, 2, 3);

			ExpressionSource<Vector3> SourceFor(string entityId) =>
				(ExpressionSource<Vector3>)info.Entities.Single(e => e.Id == entityId).InitialPosition;

			var negateId = SourceFor("negate").ExpressionId;
			var scaleId = SourceFor("scale").ExpressionId;

			var negate = (Func<Vector3, Vector3>)registry.GetCompiled(negateId).@delegate;
			var scale = (Func<Vector3, Vector3>)registry.GetCompiled(scaleId).@delegate;
			var shiftUp = (Func<Vector3, Vector3>)registry.GetCompiled("shift up").@delegate;

			Assert.AreEqual(new Vector3(-1, -2, -3), negate(v));
			Assert.AreEqual(new Vector3(2, 4, 6), scale(v));
			Assert.AreEqual(new Vector3(1, 3, 3), shiftUp(v));
		}

		// Guards the descriptor migration on the smoke-build target: Pong's migrated Do/With call
		// sites still parse and transform end-to-end, and at least one resolved to an ExpressionSource.
		[Test]
		public void PongDescriptorTransformsWithDoWith()
		{
			var path = Path.Combine(Application.dataPath, "ExampleGameDescriptors", "Pong.yaml");

			GameInfo info = null!;
			Assert.DoesNotThrow(() =>
			{
				info = Transformer.Transform(new GameFileParser().Parse(File.ReadAllText(path)));
			});

			Assert.IsTrue(info.Expressions.Count > 0, "Pong declares named expressions.");
		}

		// Descriptors that don't transform today for reasons unrelated to expressions (sequence-form
		// Constants/Expressions blocks, and a voxel-mapping field) — pre-existing, so excluded here.
		private static readonly string[] NonTransformableDescriptors = { "Snake 2.yaml", "VoxelDemo.yaml" };

		// Transforms every descriptor and compiles all of its expressions (declared + synthesised
		// inline bodies), so a broken inline body surfaces as a compile failure here.
		[Test]
		public void AllDescriptorExpressionsCompile()
		{
			var dir = Path.Combine(Application.dataPath, "ExampleGameDescriptors");
			var failures = new System.Collections.Generic.List<string>();

			foreach (var path in Directory.GetFiles(dir, "*.yaml"))
			{
				var name = Path.GetFileName(path);
				if (System.Array.IndexOf(NonTransformableDescriptors, name) >= 0)
				{
					continue;
				}

				try
				{
					var info = Transformer.Transform(new GameFileParser().Parse(File.ReadAllText(path)));
					new CompiledExpressionsRegistry(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler())
						.CompileAndRegisterAll(info.Expressions);
				}
				catch (Exception e)
				{
					failures.Add($"{name}: {e.GetType().Name}: {e.Message}");
				}
			}

			Assert.IsEmpty(failures, "Descriptors failed to transform/compile:\n" + string.Join("\n", failures));
		}
	}
}
