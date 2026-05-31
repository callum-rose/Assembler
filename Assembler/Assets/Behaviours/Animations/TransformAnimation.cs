using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <remarks>
	/// Properties:
	///   Start: Starting value; defaults to the transform's current value when left unset.
	///   End: Target value to tween towards.
	///   Duration: Tween length in seconds (clamped to a minimum of 0).
	///   Easing: DOTween ease name, case- and space-insensitive (e.g. "InOutSine", "OutBack"). Defaults to InOutSine.
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
			var ease = ParseEase(Data.Easing.ValueOr(ctx, string.Empty));

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

		private static Ease ParseEase(string name) =>
			name.ToLower().Replace(" ", "") switch
			{
				"linear" => Ease.Linear,
				"insine" => Ease.InSine,
				"outsine" => Ease.OutSine,
				"inoutsine" => Ease.InOutSine,
				"inquad" => Ease.InQuad,
				"outquad" => Ease.OutQuad,
				"inoutquad" => Ease.InOutQuad,
				"incubic" => Ease.InCubic,
				"outcubic" => Ease.OutCubic,
				"inoutcubic" => Ease.InOutCubic,
				"inquart" => Ease.InQuart,
				"outquart" => Ease.OutQuart,
				"inoutquart" => Ease.InOutQuart,
				"inquint" => Ease.InQuint,
				"outquint" => Ease.OutQuint,
				"inoutquint" => Ease.InOutQuint,
				"inexpo" => Ease.InExpo,
				"outexpo" => Ease.OutExpo,
				"inoutexpo" => Ease.InOutExpo,
				"incirc" => Ease.InCirc,
				"outcirc" => Ease.OutCirc,
				"inoutcirc" => Ease.InOutCirc,
				"inelastic" => Ease.InElastic,
				"outelastic" => Ease.OutElastic,
				"inoutelastic" => Ease.InOutElastic,
				"inback" => Ease.InBack,
				"outback" => Ease.OutBack,
				"inoutback" => Ease.InOutBack,
				"inbounce" => Ease.InBounce,
				"outbounce" => Ease.OutBounce,
				"inoutbounce" => Ease.InOutBounce,
				"flash" => Ease.Flash,
				"inflash" => Ease.InFlash,
				"outflash" => Ease.OutFlash,
				"inoutflash" => Ease.InOutFlash,
				_ => Ease.InOutSine
			};

		private void OnDestroy()
		{
			_activeTween?.Kill();
			_activeTween = null;
		}
	}
}