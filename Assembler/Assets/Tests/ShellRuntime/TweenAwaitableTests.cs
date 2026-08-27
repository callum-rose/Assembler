using System;
using System.Threading;
using Assembler.Shell.Motion;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Shell
{
	/// <summary>
	/// The contract of <see cref="TweenExtensions.ToAwaitable"/>: it finishes on kill, throws on cancellation,
	/// and leaves whatever was already hooked to the tween alone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Killing directly rather than letting the tween run: kill is the terminal event the bridge is built on, so
	/// driving it is the point, and every case resolves within the call.
	/// </para>
	/// <para>
	/// <b>Play mode, not edit mode.</b> DOTween only initialises once there is a running player — in the editor
	/// it hands out tweens that can be created but never killed, so an edit-mode version of these would assert
	/// against a library that is not actually working.
	/// </para>
	/// </remarks>
	public class TweenAwaitableTests
	{
		private float _value;

		[SetUp]
		public void SetUp()
		{
			_value = 0f;
			DOTween.Init();
		}

		[TearDown]
		public void TearDown()
		{
			DOTween.KillAll();
		}

		[Test]
		public void ResolvesWhenTheTweenIsKilled()
		{
			var tween = Tween();
			var awaitable = tween.ToAwaitable();
			var awaiter = awaitable.GetAwaiter();

			Assert.IsFalse(awaiter.IsCompleted, "a live tween should not have resolved yet");

			tween.Kill();

			Assert.IsTrue(awaiter.IsCompleted, "killing the tween should resolve the awaitable");
			awaiter.GetResult();
		}

		// The interesting half of "resolves on kill": a tween cut short — which is what the shell's
		// kill-on-disable link does every time a cached screen deactivates — must still resolve, or the
		// navigator transition awaiting it never returns.
		[Test]
		public void ResolvesWhenTheTweenIsKilledBeforeItCompletes()
		{
			var tween = Tween();
			var awaitable = tween.ToAwaitable();

			tween.Goto(0.1f);
			tween.Kill(complete: false);

			Assert.IsTrue(awaitable.GetAwaiter().IsCompleted);
			Assert.AreNotEqual(1f, _value, "the tween should have been cut short, not completed");
		}

		[Test]
		public void ResolvesImmediatelyWhenTheTweenIsAlreadyDead()
		{
			var tween = Tween();
			tween.Kill();

			Assert.IsTrue(tween.ToAwaitable().GetAwaiter().IsCompleted);
		}

		[Test]
		public void ThrowsWhenTheTokenIsCancelled()
		{
			using var source = new CancellationTokenSource();
			var tween = Tween();
			var awaiter = tween.ToAwaitable(source.Token).GetAwaiter();

			source.Cancel();

			Assert.IsTrue(awaiter.IsCompleted);
			Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
			Assert.IsFalse(tween.active, "cancelling should have killed the tween too");
		}

		[Test]
		public void ThrowsWhenTheTokenIsAlreadyCancelled()
		{
			using var source = new CancellationTokenSource();
			source.Cancel();

			var tween = Tween();
			var awaiter = tween.ToAwaitable(source.Token).GetAwaiter();

			Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
			Assert.IsFalse(tween.active);
		}

		// A cancellation resolves the source before it kills the tween, so the kill callback's TrySetResult
		// loses the race. Getting that order wrong turns every cancellation into a silent success.
		[Test]
		public void CancellationBeatsTheKillItCauses()
		{
			using var source = new CancellationTokenSource();
			var awaiter = Tween().ToAwaitable(source.Token).GetAwaiter();

			source.Cancel();

			Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
		}

		[Test]
		public void ComposesWithAnExistingKillCallback()
		{
			bool called = false;
			var tween = Tween();
			tween.OnKill(() => called = true);

			var awaiter = tween.ToAwaitable().GetAwaiter();
			tween.Kill();

			Assert.IsTrue(called, "the callback already on the tween should still have run");
			Assert.IsTrue(awaiter.IsCompleted);
		}

		// The composed callback runs the existing one first, so a callback that throws could take the resolve
		// down with it and hang whatever was awaiting. DOTween's safe mode absorbs the throw before it gets that
		// far, which is why nothing is asserted about the exception — but safe mode is a project setting, and
		// the resolve must survive either way.
		[Test]
		public void ResolvesEvenWhenAnExistingKillCallbackThrows()
		{
			var tween = Tween();
			tween.OnKill(() => throw new InvalidOperationException("boom"));

			var awaiter = tween.ToAwaitable().GetAwaiter();

			try
			{
				tween.Kill();
			}
			catch (InvalidOperationException)
			{
				// Safe mode off: the throw reaches here, and the awaitable still has to have resolved.
			}

			Assert.IsTrue(awaiter.IsCompleted, "a throwing callback must not leave the awaitable hanging");
			awaiter.GetResult();
		}

		[Test]
		public void RejectsANullTween()
		{
			Assert.Throws<ArgumentNullException>(() => ((Tween)null!).ToAwaitable());
		}

		private Tween Tween()
		{
			return DOTween
				.To(() => _value, value => _value = value, 1f, 10f)
				.SetAutoKill(false)
				.SetRecyclable(false);
		}
	}
}
