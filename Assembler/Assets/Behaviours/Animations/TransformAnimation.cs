using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public abstract class TransformAnimation : GameBehaviour<TransformAnimationData>
	{
		private Tween _activeTween;

		protected abstract Vector3 ReadCurrent();
		protected abstract void Apply(Vector3 value);

		public override void Execute()
		{
			_activeTween?.Kill();

			var hasExplicitStart = Data.Start is not NullValueProvider<Vector3>;
			var start = hasExplicitStart ? Data.Start.Value : ReadCurrent();
			var end = Data.End.Value;
			var duration = Mathf.Max(0f, Data.Duration.Value);
			var ease = ParseEase(Data.Easing is NullValueProvider<string> ? null : Data.Easing.Value);

			if (hasExplicitStart)
			{
				Apply(start);
			}

			var current = start;

			_activeTween = DOTween.To(
					() => current,
					v =>
					{
						current = v;
						Apply(v);
					},
					end,
					duration)
				.SetEase(ease)
				.SetLink(gameObject)
				.OnComplete(() =>
				{
					_activeTween = null;
					NotifyListeners();
				});
		}

		private static Ease ParseEase(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return Ease.Linear;
			}

			if (Enum.TryParse<Ease>(name, ignoreCase: true, out var ease))
			{
				return ease;
			}

			Debug.LogWarning($"Unknown easing '{name}', falling back to Linear.");
			return Ease.Linear;
		}

		private void OnDestroy()
		{
			_activeTween?.Kill();
			_activeTween = null;
		}
	}
}
