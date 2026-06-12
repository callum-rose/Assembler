using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	/// <summary>
	/// Stage-4 refiner over a scripted gateway: a good ```edits``` block parses to
	/// ops, garbage and referentially-broken edits each burn a retry, three bad
	/// rounds throw, and a lone replan op is recognised without a dry run.
	/// </summary>
	public sealed class ModelRefinerTests
	{
		private static IReadOnlyList<ModelEditOp> Propose(FakeGateway gateway) =>
			new ModelRefiner(gateway, VoxelizationConfig.Default)
				.ProposeAsync(VillagerFixture.Build(), ReferenceBrief.None, "make the shirt red", CancellationToken.None)
				.GetAwaiter().GetResult();

		[Test]
		public void ProposeAsync_ParsesAGoodEditsBlock()
		{
			var gateway = new FakeGateway().Enqueue("Sure.\n```edits\n- { op: recolour, key: B, colour: \"#cc2222\" }\n```");

			var ops = Propose(gateway);

			Assert.That(ops.Single(), Is.EqualTo(new RecolourOp('B', new Color32(0xcc, 0x22, 0x22, 0xff))));
			Assert.That(gateway.Calls.Single().Stage, Is.EqualTo(ModelRefiner.Stage));
		}

		[Test]
		public void ProposeAsync_RetriesOnGarbageThenSucceeds()
		{
			var gateway = new FakeGateway()
				.Enqueue("no fenced block here")
				.Enqueue("```edits\n- { op: recolour, key: B, colour: \"#cc2222\" }\n```");

			var ops = Propose(gateway);

			Assert.That(ops.Single(), Is.TypeOf<RecolourOp>());
			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
		}

		[Test]
		public void ProposeAsync_DryRunBouncesReferentiallyBrokenEdits()
		{
			// 'Z' is not a declared palette key — the dry-run Apply rejects it, so
			// the refiner retries with the feedback instead of returning a doomed op.
			var gateway = new FakeGateway()
				.Enqueue("```edits\n- { op: recolour, key: Z, colour: \"#cc2222\" }\n```")
				.Enqueue("```edits\n- { op: recolour, key: B, colour: \"#cc2222\" }\n```");

			var ops = Propose(gateway);

			Assert.That(ops.Single(), Is.TypeOf<RecolourOp>());
			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
			Assert.That(gateway.Calls[1].Messages.Last().Content, Does.Contain("don't apply cleanly"));
		}

		[Test]
		public void ProposeAsync_ThrowsAfterThreeBadAttempts()
		{
			var gateway = new FakeGateway()
				.Enqueue("nope").Enqueue("still nope").Enqueue("and again");

			Assert.That(() => Propose(gateway),
				Throws.TypeOf<VoxelizationException>().With.Message.Contains("after 3 attempts"));
			Assert.That(gateway.Calls.Count, Is.EqualTo(3));
		}

		[Test]
		public void ProposeAsync_RecognisesAReplanOpWithoutADryRun()
		{
			var gateway = new FakeGateway().Enqueue("```edits\n- { op: replan, reason: \"add a hat\" }\n```");

			var ops = Propose(gateway);

			Assert.That(ops.Single(), Is.EqualTo(new ReplanOp("add a hat")));
			Assert.That(gateway.Calls.Count, Is.EqualTo(1));
		}
	}
}
