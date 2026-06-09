using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>
	/// Base class for transform tween animations (move/rotate/scale) driven by DOTween. On Execute it kills any
	/// running tween, then animates <see cref="Current"/> from Start to End over Duration using the configured easing,
	/// and notifies listeners on completion. Subclasses bind <see cref="Current"/> to the property being animated.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   Start: Value to animate from. Falls back to the current transform value when unset.
	///   End: Value to animate to.
	///   Duration: Animation length in seconds (clamped to a minimum of 0).
	///   Easing: Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad,
	///     inCubic/…, inQuart/…, inQuint/…, inExpo/…, inCirc/…, inElastic/…, inBack/…, inBounce/…, and the
	///     flash variants (flash, inFlash, outFlash, inOutFlash). Case/space-insensitive. Defaults to inOutSine.
	/// </remarks>
	public abstract class TransformAnimation : GameBehaviour<TransformAnimationData>
	{
		private Tween? _activeTween;

		protected abstract Vector3 Current { get; set; }

		public override void Execute(TriggerContext ctx)
		{
			_activeTween?.Kill();

			var start = Data.Start.ValueOr(ctx, Current);
			var end = Data.End.Get(ctx);
			var duration = Mathf.Max(0f, Data.Duration.Get(ctx));
			var ease = ToEase(Data.Easing.Get(ctx));

			Current = start;

			var captured = ctx;

			_activeTween = DOTween.To(
					() => Current,
					v => Current = v,
					end,
					duration)
				.SetEase(ease)
				.SetLink(gameObject)
				.OnComplete(() =>
				{
					_activeTween = null;
					NotifyListeners(captured);
				});
		}

		// The Easing members mirror DOTween's Ease names one-for-one, so this maps straight across.
		private static Ease ToEase(Easing easing) =>
			easing switch
			{
				Easing.Linear => Ease.Linear,
				Easing.InSine => Ease.InSine,
				Easing.OutSine => Ease.OutSine,
				Easing.InOutSine => Ease.InOutSine,
				Easing.InQuad => Ease.InQuad,
				Easing.OutQuad => Ease.OutQuad,
				Easing.InOutQuad => Ease.InOutQuad,
				Easing.InCubic => Ease.InCubic,
				Easing.OutCubic => Ease.OutCubic,
				Easing.InOutCubic => Ease.InOutCubic,
				Easing.InQuart => Ease.InQuart,
				Easing.OutQuart => Ease.OutQuart,
				Easing.InOutQuart => Ease.InOutQuart,
				Easing.InQuint => Ease.InQuint,
				Easing.OutQuint => Ease.OutQuint,
				Easing.InOutQuint => Ease.InOutQuint,
				Easing.InExpo => Ease.InExpo,
				Easing.OutExpo => Ease.OutExpo,
				Easing.InOutExpo => Ease.InOutExpo,
				Easing.InCirc => Ease.InCirc,
				Easing.OutCirc => Ease.OutCirc,
				Easing.InOutCirc => Ease.InOutCirc,
				Easing.InElastic => Ease.InElastic,
				Easing.OutElastic => Ease.OutElastic,
				Easing.InOutElastic => Ease.InOutElastic,
				Easing.InBack => Ease.InBack,
				Easing.OutBack => Ease.OutBack,
				Easing.InOutBack => Ease.InOutBack,
				Easing.InBounce => Ease.InBounce,
				Easing.OutBounce => Ease.OutBounce,
				Easing.InOutBounce => Ease.InOutBounce,
				Easing.Flash => Ease.Flash,
				Easing.InFlash => Ease.InFlash,
				Easing.OutFlash => Ease.OutFlash,
				Easing.InOutFlash => Ease.InOutFlash,
				_ => Ease.InOutSine
			};

		private void OnDestroy()
		{
			_activeTween?.Kill();
			_activeTween = null;
		}
	}
}
