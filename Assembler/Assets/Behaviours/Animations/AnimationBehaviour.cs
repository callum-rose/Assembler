using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>
	/// Compiles an ordered list of tween steps into a single DOTween sequence and plays it as a unit, notifying
	/// listeners once when the whole sequence completes. Each step tweens one transform property (move/rotate/scale)
	/// or is a pure wait, and is placed relative to the previous step by its mode (append after, join alongside, or
	/// insert at an absolute time). The sequence is rebuilt from the live providers on every Execute.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   Steps: Ordered list of tween-step maps; each has Animate (move=position, rotate=eulerAngles, scale=localScale, or wait=pure delay), Mode (append after the previous step [default], join alongside the previously appended step, or insert at the absolute time given by At; ignored for wait), Start (Vector3 to tween from via DOTween .From — omit to chain from the live value at that point), End (Vector3 to tween to; required for move/rotate/scale), Duration (seconds, clamped to ≥ 0; the pause length for a wait), At (absolute sequence time used when Mode is insert), and Easing (default inOutSine). For a single-tween animation, omit Steps and set Animate/Start/End/Duration/Easing at the top level instead.
	///   Loops: How many times the whole sequence plays; 1 (default) plays once, -1 loops forever.
	///   LoopType: How the sequence repeats when Loops ≠ 1 — restart (default), yoyo, or incremental.
	/// </remarks>
	public class AnimationBehaviour : GameBehaviour<AnimationData>, IAmExecutable
	{
		private Sequence? _sequence;

		public void Execute(TriggerContext ctx)
		{
			_sequence?.Kill();

			var sequence = DOTween.Sequence().SetLink(gameObject);

			foreach (var step in Data.Steps)
			{
				var target = step.Target.Get(ctx);
				var duration = Mathf.Max(0f, step.Duration.Get(ctx));

				if (target == AnimationTarget.Wait)
				{
					sequence.AppendInterval(duration);
					continue;
				}

				var tween = target switch
				{
					AnimationTarget.Rotate => DOTween.To(() => transform.eulerAngles, v => transform.eulerAngles = v, step.End.Get(ctx), duration),
					AnimationTarget.Scale => DOTween.To(() => transform.localScale, v => transform.localScale = v, step.End.Get(ctx), duration),
					_ => DOTween.To(() => transform.position, v => transform.position = v, step.End.Get(ctx), duration)
				};
				tween.SetEase(step.Easing.ValueOr(ctx, Easing.InOutSine).ToEase());

				step.Start.UseIfValueExists(ctx, from => tween.From(from));

				switch (step.Mode.Get(ctx))
				{
					case SequenceMode.Join:
						sequence.Join(tween);
						break;
					case SequenceMode.Insert:
						sequence.Insert(Mathf.Max(0f, step.At.ValueOr(ctx, 0f)), tween);
						break;
					default:
						sequence.Append(tween);
						break;
				}
			}

			var loops = Data.Loops.ValueOr(ctx, 1);

			if (loops != 1)
			{
				sequence.SetLoops(loops, Data.LoopType.ValueOr(ctx, SequenceLoopType.Restart).ToLoopType());
			}

			sequence.OnComplete(() =>
			{
				_sequence = null;
				NotifyListeners(ctx);
			});

			_sequence = sequence;
		}

		private void OnDestroy()
		{
			_sequence?.Kill();
			_sequence = null;
		}
	}
}
