using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Compiler.Compiler;

namespace Assembler.Voxels.Scripting
{
	/// <summary>
	/// Compiles and runs Claude-authored voxel scripts in-process via
	/// <see cref="ExpressionMethodCompiler"/>. The script body is bound to a
	/// <see cref="VoxelBuilder"/> parameter named <c>b</c> and must end with
	/// <c>return b.Build();</c>. Successes report a compact summary back to Claude
	/// (voxel count / bounds / palette); failures report the error with
	/// <c>IsError</c> so Claude can fix the script within the same tool loop.
	/// </summary>
	public sealed class VoxelScriptExecutor : IVoxelScriptExecutor
	{
		public const string ToolName = "run_voxel_script";

		private const string InputSchemaJson =
			"{\"type\":\"object\"," +
			"\"properties\":{\"script\":{\"type\":\"string\"," +
			"\"description\":\"A C# method body (no signature) that builds a voxel model using the bound VoxelBuilder 'b' and ends with 'return b.Build();'.\"}}," +
			"\"required\":[\"script\"]}";

		private const string ToolDescription =
			"Build a voxel model procedurally by running a short C# script against the VoxelBuilder host API. " +
			"Prefer this for regular, symmetric, or parametric models. The script is a method body bound to a " +
			"VoxelBuilder named 'b'; it must return b.Build(). Coordinates are integers in Z-up space. On success " +
			"you get a summary (voxel count, bounds, palette size); on error you get the compile/runtime message so " +
			"you can correct and retry.";

		private readonly VoxelScriptLimits _limits;

		public VoxelScriptExecutor(VoxelScriptLimits? limits = null)
		{
			_limits = limits ?? VoxelScriptLimits.Default;
		}

		public AnthropicTool Tool => new(ToolName, ToolDescription, InputSchemaJson);

		public string? LastScript { get; private set; }
		public string? LastGoxelTextZUp { get; private set; }
		public VoxelModel? LastModel { get; private set; }

		public async Task<AnthropicToolResult> HandleToolUseAsync(AnthropicToolUse use, CancellationToken ct)
		{
			if (!TryReadScript(use.InputJson, out var script, out var inputError))
			{
				return new AnthropicToolResult(use.Id, inputError, true);
			}

			try
			{
				var model = await RunAsync(script, ct).ConfigureAwait(false);
				var goxel = GoxelTextWriter.Write(model);

				LastScript = script;
				LastModel = model;
				LastGoxelTextZUp = goxel;

				return new AnthropicToolResult(use.Id, Summarize(model), false);
			}
			catch (VoxelScriptException ex)
			{
				return new AnthropicToolResult(use.Id, "Script error: " + ex.Message, true);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new AnthropicToolResult(use.Id,
					"Script failed to compile or run: " + ex.Message, true);
			}
		}

		private async Task<VoxelModel> RunAsync(string script, CancellationToken ct)
		{
			var limits = _limits;

			// CTS linked to the outer token so external cancellation also stops the builder.
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			// Run compile + execute off the calling thread so a hard wall-clock
			// timeout can fire even if the script never cooperatively yields.
			var runTask = Task.Run(() =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(VoxelAxis));
				var func = compiler.CompileFunc<VoxelBuilder, VoxelModel>(script, "b");
				var builder = new VoxelBuilder(limits, cts.Token);
				return func(builder);
			}); // no ct on Task.Run — the builder enforces the budget cooperatively

			var finished = await Task.WhenAny(runTask, Task.Delay(limits.WallClock, ct)).ConfigureAwait(false);
			if (finished != runTask)
			{
				ct.ThrowIfCancellationRequested();
				// Signal the builder so the runTask self-terminates via its cooperative
				// check rather than spinning until the next stopwatch sample.
				cts.Cancel();
				throw new VoxelScriptException(
					$"Script exceeded its wall-clock budget of {limits.WallClock.TotalSeconds:0.##}s.");
			}

			return await runTask.ConfigureAwait(false);
		}

		private static bool TryReadScript(string inputJson, out string script, out string error)
		{
			script = string.Empty;
			error = string.Empty;
			try
			{
				using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson);
				if (!doc.RootElement.TryGetProperty("script", out var scriptElement) ||
					scriptElement.ValueKind != JsonValueKind.String)
				{
					error = "Tool input must be a JSON object with a string 'script' field.";
					return false;
				}

				script = scriptElement.GetString() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(script))
				{
					error = "The 'script' field was empty.";
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				error = "Could not parse tool input JSON: " + ex.Message;
				return false;
			}
		}

		private static string Summarize(VoxelModel model)
		{
			var size = model.Size;
			return $"OK: built {model.Voxels.Count} voxels, " +
				   $"bounds min ({model.Min.x}, {model.Min.y}, {model.Min.z}) max ({model.Max.x}, {model.Max.y}, {model.Max.z}), " +
				   $"size {size.x}x{size.y}x{size.z}, {model.Palette.Length} colour(s).";
		}
	}
}
