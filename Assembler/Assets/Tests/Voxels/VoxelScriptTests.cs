using System;
using System.Threading;
using Assembler.Anthropic;
using Assembler.Compiler.Compiler;
using Assembler.Voxels;
using Assembler.Voxels.Scripting;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxels
{
	public class VoxelScriptTests
	{
		// ---- Compiler + builder integration -------------------------------

		[Test]
		public void CompiledScript_BuildsExpectedModel()
		{
			const string script =
				"for (int x = 0; x < 4; x++) { b.Box(x, 0, 0, x, 3, 3, b.Rgb(255, 0, 0)); } return b.Build();";

			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(VoxelAxis));
			var func = compiler.CompileFunc<VoxelBuilder, VoxelModel>(script, "b");

			var model = func(new VoxelBuilder());

			// 4 columns (x in 0..3), each Box(x,0,0,x,3,3) = 1 * 4 * 4 = 16 voxels.
			Assert.AreEqual(64, model.Voxels.Count);
			Assert.AreEqual(new Vector3Int(0, 0, 0), model.Min);
			Assert.AreEqual(new Vector3Int(3, 3, 3), model.Max);
			Assert.AreEqual(1, model.Palette.Length, "all voxels share one colour");
		}

		[Test]
		public void CompiledScript_UsesAxisEnumAndHelpers()
		{
			const string script =
				"var c = b.Hex(\"00ff00\"); b.Cylinder(0, 0, 0, 2, 5, VoxelAxis.Y, c); return b.Build();";

			var compiler = new ExpressionMethodCompiler();
			compiler.RegisterType(typeof(VoxelAxis));
			var func = compiler.CompileFunc<VoxelBuilder, VoxelModel>(script, "b");

			var model = func(new VoxelBuilder());

			Assert.Greater(model.Voxels.Count, 0);
			Assert.AreEqual(1, model.Palette.Length);
			// Height 5 along Y, centred on 0 → spans y -2..2.
			Assert.AreEqual(-2, model.Min.y);
			Assert.AreEqual(2, model.Max.y);
		}

		// ---- Executor tool protocol ---------------------------------------

		[Test]
		public void Executor_ValidScript_ReportsSuccessAndCapturesModel()
		{
			var executor = new VoxelScriptExecutor();
			var use = new AnthropicToolUse("use-1", VoxelScriptExecutor.ToolName,
				Script("b.Box(0, 0, 0, 1, 1, 1, b.Rgb(10, 20, 30)); return b.Build();"));

			var result = executor.HandleToolUseAsync(use, CancellationToken.None).GetAwaiter().GetResult();

			Assert.IsFalse(result.IsError, result.Content);
			Assert.AreEqual("use-1", result.ToolUseId);
			StringAssert.Contains("8 voxels", result.Content);
			Assert.IsNotNull(executor.LastModel);
			Assert.AreEqual(8, executor.LastModel!.Voxels.Count);
			Assert.IsFalse(string.IsNullOrEmpty(executor.LastGoxelTextZUp));
			Assert.IsFalse(string.IsNullOrEmpty(executor.LastScript));
		}

		[Test]
		public void Executor_CompileError_ReturnsToolError()
		{
			var executor = new VoxelScriptExecutor();
			var use = new AnthropicToolUse("use-2", VoxelScriptExecutor.ToolName,
				Script("this is definitely not valid code"));

			var result = executor.HandleToolUseAsync(use, CancellationToken.None).GetAwaiter().GetResult();

			Assert.IsTrue(result.IsError);
			Assert.AreEqual("use-2", result.ToolUseId);
			Assert.IsNull(executor.LastModel);
		}

		[Test]
		public void Executor_MissingScriptField_ReturnsToolError()
		{
			var executor = new VoxelScriptExecutor();
			var use = new AnthropicToolUse("use-3", VoxelScriptExecutor.ToolName, "{\"notscript\":1}");

			var result = executor.HandleToolUseAsync(use, CancellationToken.None).GetAwaiter().GetResult();

			Assert.IsTrue(result.IsError);
		}

		// ---- Safety limits ------------------------------------------------

		[Test]
		public void Builder_ExceedingVoxelCap_Throws()
		{
			var builder = new VoxelBuilder(new VoxelScriptLimits { MaxVoxels = 10 });

			Assert.Throws<VoxelScriptException>(() =>
				builder.Box(0, 0, 0, 9, 9, 9, new Color32(1, 2, 3, 255)));
		}

		[Test]
		public void Builder_ExceedingWallClock_Throws()
		{
			var builder = new VoxelBuilder(new VoxelScriptLimits
			{
				MaxVoxels = 10_000_000,
				WallClock = TimeSpan.FromMilliseconds(1),
			});

			// Re-setting the same cell never grows the count, so only the
			// wall-clock budget can stop this finite-but-long loop.
			var colour = new Color32(1, 1, 1, 255);
			Assert.Throws<VoxelScriptException>(() =>
			{
				for (var i = 0; i < 50_000_000; i++)
				{
					builder.Set(0, 0, 0, colour);
				}
			});
		}

		[Test]
		public void Executor_VoxelCapExceeded_ReturnedAsToolError()
		{
			var executor = new VoxelScriptExecutor(new VoxelScriptLimits { MaxVoxels = 10 });
			var use = new AnthropicToolUse("use-4", VoxelScriptExecutor.ToolName,
				Script("b.Box(0, 0, 0, 20, 20, 20, b.Rgb(1, 2, 3)); return b.Build();"));

			var result = executor.HandleToolUseAsync(use, CancellationToken.None).GetAwaiter().GetResult();

			Assert.IsTrue(result.IsError);
			StringAssert.Contains("cap", result.Content);
		}

		private static string Script(string body)
		{
			// Wrap a script body into the tool's JSON input ({"script": "..."}).
			var escaped = body.Replace("\\", "\\\\").Replace("\"", "\\\"");
			return "{\"script\":\"" + escaped + "\"}";
		}
	}
}
