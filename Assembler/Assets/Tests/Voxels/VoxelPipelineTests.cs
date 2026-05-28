using System;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels;
using Assembler.Voxels.Pipeline;
using Assembler.Voxels.Pipeline.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxels
{
	public class VoxelPipelineTests
	{
		[Test]
		public void Extract_FencedBlock_LeavesContentUnchanged()
		{
			const string raw = "Here you go:\n```goxel\n0 1 2 ff0000\n3 4 5 00ff00\n```\nThanks.";
			var ctx = new VoxelPipelineContext { RawAssistantText = raw };

			var result = new ExtractGoxelBlockStage().ExecuteAsync(ctx, CancellationToken.None).Result;

			// Extract is now pure — coordinates pass through verbatim. Use
			// SwapYZAxesStage if you need to convert from Claude's Y-up to
			// storage Z-up.
			StringAssert.Contains("0 1 2 ff0000", result.GoxelTextZUp);
			StringAssert.Contains("3 4 5 00ff00", result.GoxelTextZUp);
		}

		[Test]
		public void Extract_MissingFence_Throws()
		{
			var ctx = new VoxelPipelineContext { RawAssistantText = "no fenced block here" };
			Assert.ThrowsAsync<InvalidOperationException>(
				async () => await new ExtractGoxelBlockStage().ExecuteAsync(ctx, CancellationToken.None));
		}

		[Test]
		public void SwapYZAxes_SwapsCoordinatesAndIsInvolutive()
		{
			const string original = "0 1 2 ff0000\n3 4 5 00ff00\n# comment line\n";
			var ctx = new VoxelPipelineContext { GoxelTextZUp = original };

			var once = new SwapYZAxesStage().ExecuteAsync(ctx, CancellationToken.None).Result;
			StringAssert.Contains("0 2 1 ff0000", once.GoxelTextZUp);
			StringAssert.Contains("3 5 4 00ff00", once.GoxelTextZUp);
			StringAssert.Contains("# comment line", once.GoxelTextZUp);

			var twice = new SwapYZAxesStage().ExecuteAsync(once, CancellationToken.None).Result;
			Assert.AreEqual(original, twice.GoxelTextZUp);
		}

		[Test]
		public void DedupeVoxels_KeepsLastOccurrencePerPosition()
		{
			const string text =
				"0 0 0 ff0000\n" +
				"1 0 0 ff0000\n" +
				"# pip override\n" +
				"0 0 0 000000\n" +    // overrides line 1
				"2 0 0 00ff00\n" +
				"1 0 0 0000ff\n";     // overrides line 2
			var ctx = new VoxelPipelineContext { GoxelTextZUp = text };

			var result = new DedupeVoxelsStage().ExecuteAsync(ctx, CancellationToken.None).Result;

			var lines = result.GoxelTextZUp!.Split('\n');
			CollectionAssert.AreEqual(
				new[] { "# pip override", "0 0 0 000000", "2 0 0 00ff00", "1 0 0 0000ff", "" },
				lines);
		}

		[Test]
		public void DedupeVoxels_NoDuplicates_IsIdentity()
		{
			const string text = "0 0 0 ff0000\n1 0 0 00ff00\n# done";
			var ctx = new VoxelPipelineContext { GoxelTextZUp = text };

			var result = new DedupeVoxelsStage().ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual(text, result.GoxelTextZUp);
		}

		[Test]
		public void ParseAndEncode_RoundTripsThroughContext()
		{
			const string text = "0 0 0 ff0000\n1 0 0 00ff00\n0 1 0 ff0000";
			var ctx = new VoxelPipelineContext { GoxelTextZUp = text };

			var afterParse = new ParseGoxelTextStage().ExecuteAsync(ctx, CancellationToken.None).Result;
			Assert.IsNotNull(afterParse.Model);
			Assert.AreEqual(3, afterParse.Model!.Voxels.Count);

			var afterEncode = new EncodeVoxStage().ExecuteAsync(afterParse, CancellationToken.None).Result;
			Assert.IsNotNull(afterEncode.VoxBytes);
			Assert.Greater(afterEncode.VoxBytes!.Length, 0);
		}

		[Test]
		public void RecordHistory_AppendsEntryWithClock()
		{
			var clock = new FixedClock(new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc));
			var ctx = new VoxelPipelineContext
			{
				Clock = clock,
				UserPrompt = "make an apple",
				GoxelTextZUp = "0 0 0 ff0000",
			};

			var result = new RecordHistoryStage("generate").ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual(1, result.Project.history.Count);
			var entry = result.Project.history[0];
			Assert.AreEqual("generate", entry.kind);
			Assert.AreEqual("make an apple", entry.prompt);
			Assert.AreEqual("0 0 0 ff0000", entry.goxelText);
			Assert.AreEqual(clock.UtcNow.ToString("o"), entry.timestampIso);
			// Original ctx is untouched (immutable record semantics).
			Assert.AreEqual(0, ctx.Project.history.Count);
		}

		[Test]
		public void RecordHistory_RefineUsesRefinementInstruction()
		{
			var ctx = new VoxelPipelineContext
			{
				UserPrompt = "ignored",
				RefinementInstruction = "make it blue",
				GoxelTextZUp = "0 0 0 0000ff",
			};

			var result = new RecordHistoryStage("refine-chat").ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual("make it blue", result.Project.history[0].prompt);
		}

		[Test]
		public void WriteVoxFile_WritesToSink_AndSetsSavedPath()
		{
			var sink = new InMemoryFileSink();
			var ctx = new VoxelPipelineContext { FileSink = sink, VoxBytes = new byte[] { 1, 2, 3, 4 } };

			var result = new WriteVoxFileStage("foo/bar.vox").ExecuteAsync(ctx, CancellationToken.None).Result;

			Assert.AreEqual("foo/bar.vox", result.SavedVoxPath);
			Assert.AreEqual(1, sink.Writes.Count);
			Assert.AreEqual("foo/bar.vox", sink.Writes[0].path);
			CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, sink.Writes[0].bytes);
		}

		[Test]
		public void Pipeline_EncodeVoxAutoInsertsParseModel()
		{
			const string text = "0 0 0 ff0000";
			var pipeline = SeedTextPipeline(text).EncodeVox();

			var result = pipeline.ExecuteAsync(CancellationToken.None).Result;

			Assert.IsNotNull(result.Model);
			Assert.IsNotNull(result.VoxBytes);
		}

		[Test]
		public void Pipeline_SaveAsVoxFileAutoInsertsParseAndEncode()
		{
			const string text = "0 0 0 ff0000";
			var sink = new InMemoryFileSink();
			var services = VoxelPipelineServices.Default with { FileSink = sink };
			var pipeline = SeedTextPipeline(text, services).SaveAsVoxFile("out.vox");

			var result = pipeline.ExecuteAsync(CancellationToken.None).Result;

			Assert.AreEqual("out.vox", result.SavedPath);
			Assert.AreEqual(1, sink.Writes.Count);
			Assert.IsNotNull(result.Model);
			Assert.IsNotNull(result.VoxBytes);
		}

		[Test]
		public void Pipeline_FromExisting_CarriesContextForward()
		{
			const string text = "0 0 0 ff0000";
			var first = SeedTextPipeline(text).ParseModel().EncodeVox().ExecuteAsync(CancellationToken.None).Result;
			Assert.IsNotNull(first.VoxBytes);

			var sink = new InMemoryFileSink();
			var services = VoxelPipelineServices.Default with { FileSink = sink };
			var second = VoxelGenerationPipeline.FromExisting(first, services)
				.SaveAsVoxFile("again.vox")
				.ExecuteAsync(CancellationToken.None).Result;

			Assert.AreEqual("again.vox", second.SavedPath);
			Assert.IsNotNull(second.Model, "Model should carry over from prior result.");
			Assert.IsNotNull(second.VoxBytes);
		}

		[Test]
		public void Pipeline_ObserverReceivesStartAndFinishPerStage()
		{
			var observer = new RecordingObserver();
			var ctx = new VoxelPipelineContext { GoxelTextZUp = "0 0 0 ff0000", Observer = observer };
			var pipeline = VoxelGenerationPipeline.FromExisting(new VoxelPipelineResult(ctx))
				.WithObserver(observer)
				.ParseModel()
				.EncodeVox();

			pipeline.ExecuteAsync(CancellationToken.None).Wait();

			Assert.Contains("ParseGoxelText:started", observer.Events);
			Assert.Contains("ParseGoxelText:finished", observer.Events);
			Assert.Contains("EncodeVox:started", observer.Events);
			Assert.Contains("EncodeVox:finished", observer.Events);
		}

		// Helpers ----------------------------------------------------------------

		private static VoxelGenerationPipeline SeedTextPipeline(string goxelText, VoxelPipelineServices? services = null)
		{
			services ??= VoxelPipelineServices.Default;
			var seed = new VoxelPipelineResult(new VoxelPipelineContext { GoxelTextZUp = goxelText });
			return VoxelGenerationPipeline.FromExisting(seed, services);
		}

		private sealed class FixedClock : IVoxelClock
		{
			public FixedClock(DateTime utcNow) => UtcNow = utcNow;
			public DateTime UtcNow { get; }
		}

		private sealed class InMemoryFileSink : IVoxelFileSink
		{
			public System.Collections.Generic.List<(string path, byte[] bytes)> Writes { get; } = new();
			public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
			{
				Writes.Add((path, (byte[])bytes.Clone()));
				return Task.CompletedTask;
			}
		}

		private sealed class RecordingObserver : IVoxelPipelineObserver
		{
			public System.Collections.Generic.List<string> Events { get; } = new();
			public void OnStageStarted(string stageName) => Events.Add(stageName + ":started");
			public void OnStageFinished(string stageName, TimeSpan elapsed) => Events.Add(stageName + ":finished");
			public void OnStageFailed(string stageName, Exception ex) => Events.Add(stageName + ":failed");
			public void OnLog(string message) => Events.Add("log:" + message);
			public void OnStreamDelta(string delta) => Events.Add("delta:" + delta);
		}
	}
}
