using System;
using System.Threading;
using Assembler.Shell.Theming;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Shell.Motion
{
	/// <summary>
	/// The bridge between DOTween and the shell's <see cref="Awaitable"/>-based transitions, plus the two
	/// settings every shell tween is obliged to carry (UIPLAN 8.2, 8.3).
	/// </summary>
	public static class TweenExtensions
	{
		/// <summary>
		/// Applies the two rules every shell tween lives by: killed when its owner is disabled, and driven by
		/// unscaled time.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <b>Kill on disable</b> because screens are cached rather than destroyed (UIPLAN 3.2), so they
		/// deactivate constantly. An unlinked tween left running against a deactivated screen completes
		/// invisibly and writes its end value over whatever the next <c>OnEnter</c> just set up.
		/// </para>
		/// <para>
		/// <b>Unscaled</b> because a paused game sets <c>Time.timeScale = 0</c> (UIPLAN 10.3) and the chrome
		/// drawn over it — the pause sheet, the result slip, a button inside either — still has to move.
		/// Shell motion is never gameplay motion, so there is nothing that wants scaled time.
		/// </para>
		/// </remarks>
		public static T SetShellDefaults<T>(this T tween, GameObject owner, bool unscaled = true) where T : Tween
		{
			if (tween == null)
			{
				throw new ArgumentNullException(nameof(tween));
			}

			if (owner == null)
			{
				throw new ArgumentNullException(nameof(owner));
			}

			return tween
				.SetLink(owner, LinkBehaviour.KillOnDisable)
				.SetUpdate(unscaled);
		}

		/// <summary>Tweens a rect's anchored position.</summary>
		/// <remarks>
		/// DOTween ships its uGUI shortcuts — <c>DOAnchorPos</c>, <c>DOFade</c> and the rest — as loose source
		/// files under <c>Assets/Plugins</c>, which means they compile into the default assembly and are
		/// invisible from an assembly definition like this one. Building the two the shell needs out of
		/// <see cref="DOTween.To{T1,T2,TPlugOptions}"/> costs a line each and leaves the vendored plugin alone;
		/// giving its Modules folder an assembly definition would instead take those shortcuts away from
		/// everything that currently sees them.
		/// </remarks>
		public static Tweener TweenAnchoredPosition(this RectTransform rect, Vector2 target, float duration)
		{
			if (rect == null)
			{
				throw new ArgumentNullException(nameof(rect));
			}

			return DOTween
				.To(() => rect.anchoredPosition, value => rect.anchoredPosition = value, target, duration)
				.SetTarget(rect);
		}

		/// <inheritdoc cref="TweenAnchoredPosition"/>
		/// <summary>Tweens a canvas group's alpha.</summary>
		public static Tweener TweenAlpha(this CanvasGroup group, float target, float duration)
		{
			if (group == null)
			{
				throw new ArgumentNullException(nameof(group));
			}

			return DOTween
				.To(() => group.alpha, value => group.alpha = value, target, duration)
				.SetTarget(group);
		}

		/// <summary>Eases <paramref name="tween"/> the way the theme's named <paramref name="spec"/> says to.</summary>
		/// <remarks>
		/// The duration is not applied here — DOTween takes it at creation — so a call site reads
		/// <c>DOAnchorPos(target, spec.Duration).SetMotion(spec)</c>. Motion literals are banned in shell code
		/// (UIPLAN 8.4); both halves of a spec come off the theme.
		/// </remarks>
		public static T SetMotion<T>(this T tween, MotionSpec spec) where T : Tween
		{
			if (tween == null)
			{
				throw new ArgumentNullException(nameof(tween));
			}

			if (spec is null)
			{
				throw new ArgumentNullException(nameof(spec));
			}

			return tween.SetEase(spec.Ease);
		}

		/// <summary>
		/// An <see cref="Awaitable"/> that finishes when <paramref name="tween"/> is killed, and throws
		/// <see cref="OperationCanceledException"/> if <paramref name="cancellationToken"/> fires first.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <b>It resolves on kill, not on complete.</b> Kill is the one terminal event DOTween guarantees: a
		/// tween that completes is killed straight after, and a tween cut short by
		/// <see cref="SetShellDefaults{T}"/>'s kill-on-disable link never completes at all. Awaiting
		/// <c>OnComplete</c> would deadlock a navigator transition the moment a screen deactivated mid-fade.
		/// </para>
		/// <para>
		/// <b>It composes, it does not replace.</b> The tween's existing <c>onKill</c> callback is captured and
		/// invoked first, so awaiting a tween never silently unhooks a callback a caller had already attached.
		/// </para>
		/// <para>
		/// <b>Main-thread tokens only.</b> The completion source is resolved from the cancellation callback,
		/// which runs on whichever thread cancelled — so pass a token raised on the main thread
		/// (<c>destroyCancellationToken</c>, <c>Application.exitCancellationToken</c>, or one linked to them).
		/// </para>
		/// </remarks>
		public static Awaitable ToAwaitable(this Tween tween, CancellationToken cancellationToken = default)
		{
			if (tween == null)
			{
				throw new ArgumentNullException(nameof(tween));
			}

			var completionSource = new AwaitableCompletionSource();

			// An inactive tween has already been killed — possibly before this call, possibly because it was
			// created dead. There is no onKill left to hook, so resolve now rather than wait for an event that
			// has been and gone.
			if (!tween.active)
			{
				completionSource.SetResult();
				return completionSource.Awaitable;
			}

			if (cancellationToken.IsCancellationRequested)
			{
				completionSource.SetCanceled();
				tween.Kill();
				return completionSource.Awaitable;
			}

			var registration = default(CancellationTokenRegistration);
			var previousOnKill = tween.onKill;

			tween.onKill = () =>
			{
				registration.Dispose();

				try
				{
					previousOnKill?.Invoke();
				}
				finally
				{
					// TrySet, not Set: a cancellation has already resolved the source, and this kill is the one
					// it asked for.
					completionSource.TrySetResult();
				}
			};

			if (!cancellationToken.CanBeCanceled)
			{
				return completionSource.Awaitable;
			}

			// Cancel first, then kill. The kill runs the callback above, whose TrySetResult then loses the race
			// against this TrySetCanceled — which is what makes the await throw rather than return quietly.
			registration = cancellationToken.Register(() =>
			{
				completionSource.TrySetCanceled();
				tween.Kill();
			});

			return completionSource.Awaitable;
		}
	}
}
