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
	// type inference, nesting, dispatch and the missing-Do error. `With` is a map of `name: value`
	// operands — bound by name to the declared arguments (named call) or used as the body's parameter
	// names (inline body); the positional arg0/arg1 form has been removed.
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
				"!expr { Do: shift up, With: { a: !var v } }",
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
				"!expr { Do: up, With: { a: !var v } }",
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
		public void NamedCallBindsOperandsByNameRegardlessOfOrder()
		{
			// Declared order is (base: vector, lift: float); the call site lists them reversed. Binding
			// by position would put the float operand into the vector slot and fail — binding by name
			// pairs each operand with its declared argument, so this parses and targets the declared id.
			GameInfo info = null!;
			Assert.DoesNotThrow(() => info = Parse(EntityWithPositionExpr(
				"!expr { Do: lift, With: { lift: 1.0, base: !var v } }",
				extraTop: @"
Expressions:
  lift:
    ArgumentTypes: [ vector, float ]
    ArgumentNames: [ base, lift ]
    ReturnType: vector
    Expression: 'return base + new UnityEngine.Vector3(0f, lift, 0f);'")));

			var source = (ExpressionSource<Vector3>)PositionOf(info);
			Assert.AreEqual("lift", source.ExpressionId);
			Assert.AreEqual(2, source.Arguments.Count);
		}

		[Test]
		public void NamedCallMissingOperandNameThrows()
		{
			var ex = Assert.Throws<ParsingException>(() => Parse(EntityWithPositionExpr(
				"!expr { Do: shift up, With: { wrong: !var v } }",
				extraTop: @"
Expressions:
  shift up:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    Expression: 'return a + new UnityEngine.Vector3(0, 1, 0);'")));

			Assert.That(ex!.Message, Does.Contain("a"));
		}

		[Test]
		public void DuplicateOperandNameThrows()
		{
			var ex = Assert.Throws<ParsingException>(() => Parse(EntityWithPositionExpr(
				"!expr { Do: 'a + a', With: { a: !var v, a: 2 } }")));

			Assert.That(ex!.Message, Does.Contain("more than once"));
		}

		[Test]
		public void InlineDoBodySynthesisesAnonymousExpression()
		{
			var info = Parse(EntityWithPositionExpr("!expr { Do: '-a', With: { a: !var v } }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);

			Assert.AreEqual(1, source.Arguments.Count);
			Assert.IsTrue(source.ExpressionId.StartsWith("__inline_"));

			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);
			Assert.AreEqual("vector", synthesised.ReturnType);
			Assert.AreEqual(1, synthesised.Arguments.Count);
			Assert.AreEqual(("vector", "a"), synthesised.Arguments[0]);
		}

		[Test]
		public void InlineBodyInfersOperandTypesFromConstantsAndVars()
		{
			// scale is a vector (from !var v), factor is an int literal.
			var info = Parse(EntityWithPositionExpr("!expr { Do: 'scale * factor', With: { scale: !var v, factor: 2 } }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);
			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);

			Assert.AreEqual("vector", synthesised.Arguments[0].type);
			Assert.AreEqual("int", synthesised.Arguments[1].type);
			Assert.AreEqual("vector", synthesised.ReturnType);
		}

		[Test]
		public void InlineBodyInfersEntityPropertyOperandAsVector()
		{
			// An `!entity { Property: Rotation }` operand resolves to Vector3, a type known from the ref
			// kind alone — inference must pick "vector", not the "float" default, so no explicit
			// ArgumentTypes hint is needed (issue #399).
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: 'heading * 2', With: { heading: !entity { Id: e, Property: Rotation } } }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);
			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);

			Assert.AreEqual(("vector", "heading"), synthesised.Arguments[0]);
			Assert.AreEqual("vector", synthesised.ReturnType);
		}

		[Test]
		public void InlineBodyInfersRigidbodyPropertyOperandAsVector()
		{
			// A `!rigidbody { Property: Velocity }` operand likewise resolves to Vector3.
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: '-vel', With: { vel: !rigidbody { Id: e, Property: Velocity } } }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);
			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);

			Assert.AreEqual(("vector", "vel"), synthesised.Arguments[0]);
			Assert.AreEqual("vector", synthesised.ReturnType);
		}

		[Test]
		public void DoNameWinsOverInlineInterpretation()
		{
			// "scale" is also a plausible inline identifier, but because it's a declared expression the
			// registry-membership check routes it as a named call, not an inline body.
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: scale, With: { a: !var v } }",
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
				"!expr { Do: shift up, With: { a: !expr { Do: '-x', With: { x: !var v } } } }",
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
			var ex = Assert.Catch(() => Parse(EntityWithPositionExpr("!expr { With: { a: !var v } }")));

			Assert.That(ex!.ToString(), Does.Contain("Do"));
		}

		[Test]
		public void EmptyDoThrows()
		{
			var ex = Assert.Catch(() => Parse(EntityWithPositionExpr("!expr { Do: '', With: { a: !var v } }")));

			Assert.That(ex!.ToString(), Does.Contain("Do"));
		}

		[Test]
		public void SequenceFormWithThrows()
		{
			// The positional list form is no longer accepted; it must be a map.
			var ex = Assert.Catch(() => Parse(EntityWithPositionExpr("!expr { Do: '-a', With: [ !var v ] }")));

			Assert.That(ex!.ToString(), Does.Contain("map"));
		}

		// End-to-end: YAML -> ExpressionSource -> compiled delegate -> value, for inline `-a`
		// and `a * 2` plus a named call, confirming the whole pipeline agrees on types/results.
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
    Position: !expr {{ Do: '-a', With: {{ a: !var v }} }}
    Behaviours: {{}}
  scale:
    Position: !expr {{ Do: 'a * 2', With: {{ a: !var v }} }}
    Behaviours: {{}}
  named:
    Position: !expr {{ Do: shift up, With: {{ a: !var v }} }}
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

		// --- optional inline hints: ReturnType / ArgumentTypes / RegisterTypes / RegisterTypeStatics ---

		[Test]
		public void InlineArgumentTypesHintOverridesInference()
		{
			// With { a: 1, b: 2 } would infer int operands; the explicit hint forces float.
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: 'a + b', With: { a: 1, b: 2 }, ArgumentTypes: [ float, float ] }"));

			var synthesised = info.Expressions.Single(e => e.Id.StartsWith("__inline_"));

			Assert.AreEqual(("float", "a"), synthesised.Arguments[0]);
			Assert.AreEqual(("float", "b"), synthesised.Arguments[1]);
		}

		[Test]
		public void InlineArgumentTypesCountMismatchThrows()
		{
			Assert.Throws<ParsingException>(() => Parse(EntityWithPositionExpr(
				"!expr { Do: 'a + b', With: { a: !var v }, ArgumentTypes: [ vector, vector ] }")));
		}

		[Test]
		public void InlineUnknownReturnTypeThrows()
		{
			Assert.Throws<ParsingException>(() => Parse(EntityWithPositionExpr(
				"!expr { Do: '-a', With: { a: !var v }, ReturnType: nonsense }")));
		}

		[Test]
		public void InlineReturnTypeHintFlowsToSynthesisedExpression()
		{
			var info = Parse(EntityWithPositionExpr("!expr { Do: '-a', With: { a: !var v }, ReturnType: vector }"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);
			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);

			Assert.AreEqual("vector", synthesised.ReturnType);
		}

		// In an object-typed context (a !text argument) the use-site type can't be inferred for a
		// zero-operand body, so an explicit ReturnType is what makes it work.
		[Test]
		public void InlineReturnTypeUnlocksObjectContext()
		{
			var yaml = @"
Localisation:
  DefaultLocale: en
  Locales: { en: { k: ""value {0}"" } }
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: !text { Key: k, Arguments: [ !expr { Do: 'RandomInt(0, 6)', ReturnType: int } ] }
";
			GameInfo info = null!;
			Assert.DoesNotThrow(() => info = Parse(yaml));

			var synthesised = info.Expressions.Single(e => e.Id.StartsWith("__inline_"));
			Assert.AreEqual("int", synthesised.ReturnType);

			Assert.DoesNotThrow(() =>
				new CompiledExpressionsRegistry(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler())
					.CompileAndRegisterAll(info.Expressions));
		}

		[Test]
		public void ZeroOperandInlineInObjectContextWithoutReturnTypeThrows()
		{
			var yaml = @"
Localisation:
  DefaultLocale: en
  Locales: { en: { k: ""value {0}"" } }
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: !text { Key: k, Arguments: [ !expr { Do: 'RandomInt(0, 6)' } ] }
";
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}

		// RegisterTypes makes a type available to the inline body by short name; without it
		// `Mathf` (unqualified) would not resolve.
		[Test]
		public void InlineRegisterTypesEnableShortTypeNameAndCompile()
		{
			var info = Parse(@"
Entities:
  e:
    Position: !expr { Do: 'new UnityEngine.Vector3(Mathf.PI, 0f, 0f)', RegisterTypes: [ UnityEngine.Mathf ] }
    Behaviours: {}
");
			var source = (ExpressionSource<Vector3>)PositionOf(info);
			var synthesised = info.Expressions.Single(e => e.Id == source.ExpressionId);
			Assert.Contains("UnityEngine.Mathf", synthesised.RegisterTypes);

			var registry = new CompiledExpressionsRegistry(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());
			registry.CompileAndRegisterAll(info.Expressions);

			var func = (Func<Vector3>)registry.GetCompiled(source.ExpressionId).@delegate;
			Assert.AreEqual(Mathf.PI, func().x, 0.0001f);
		}

		[Test]
		public void NamedCallIgnoresInlineHints()
		{
			// ReturnType on a named call is ignored (logged); the source still targets the named id
			// and no inline expression is synthesised.
			var info = Parse(EntityWithPositionExpr(
				"!expr { Do: shift up, With: { a: !var v }, ReturnType: int, RegisterTypes: [ UnityEngine.Mathf ] }",
				extraTop: @"
Expressions:
  shift up:
    ArgumentTypes: [ vector ]
    ArgumentNames: [ a ]
    ReturnType: vector
    Expression: 'return a + new UnityEngine.Vector3(0, 1, 0);'"));

			var source = (ExpressionSource<Vector3>)PositionOf(info);

			Assert.AreEqual("shift up", source.ExpressionId);
			Assert.IsFalse(info.Expressions.Any(e => e.Id.StartsWith("__inline_")));
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
