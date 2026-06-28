using Assembler.Parsing.Info.Behaviours;
using DG.Tweening;

namespace Assembler.Behaviours.Animations
{
	/// <summary>Maps the descriptor-facing <see cref="Easing"/> / <see cref="SequenceLoopType"/> enums onto their
	/// DOTween equivalents. Sole home for the mapping shared by the animation behaviour.</summary>
	public static class EasingExtensions
	{
		// The Easing members mirror DOTween's Ease names one-for-one, so this maps straight across.
		public static Ease ToEase(this Easing easing) =>
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

		public static LoopType ToLoopType(this SequenceLoopType loopType) =>
			loopType switch
			{
				SequenceLoopType.Restart => LoopType.Restart,
				SequenceLoopType.Yoyo => LoopType.Yoyo,
				SequenceLoopType.Incremental => LoopType.Incremental,
				_ => LoopType.Restart
			};
	}
}
