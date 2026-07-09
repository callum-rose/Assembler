using Assembler.Time;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>
	/// Advances the game's DOTween animations off the injected <see cref="IGameClock"/> instead of Unity's
	/// wall-clock. <see cref="AnimationBehaviour"/> marks its sequences <see cref="UpdateType.Manual"/> so DOTween's
	/// own loop ignores them; this driver pumps every manual tween once per frame with the clock's delta. The effect:
	/// tweens freeze with the game (<c>set timescale 0</c>) instead of animating — and, crucially, their
	/// <c>OnComplete</c> (which fires listener/trigger chains, i.e. game logic) no longer fires into a paused game —
	/// honour the clock's timescale, and under the fixed-step clock advance by a constant step so tween-completion
	/// timing reproduces on replay (issue #241).
	/// </summary>
	/// <remarks>
	/// <see cref="DefaultExecutionOrderAttribute"/> places this in the gameplay tick, after the clock tick (-10000),
	/// replayed input (-9000) and the per-frame driver (-8000), so a tween that completes this frame notifies from a
	/// stable point each frame. <see cref="DOTween.ManualUpdate"/> only touches <see cref="UpdateType.Manual"/>
	/// tweens, so any non-game tween left on the default update loop is unaffected.
	/// </remarks>
	[DefaultExecutionOrder(-7900)]
	public sealed class TweenDriver : MonoBehaviour
	{
		private IGameClock _clock = null!;

		// Ensure DOTween is initialised before the first ManualUpdate (it would otherwise only initialise lazily on
		// the first tween creation, which may come after this driver's first Update). Idempotent.
		private void Awake() => DOTween.Init();

		private void Update() => DOTween.ManualUpdate(_clock.DeltaTime, _clock.UnscaledDeltaTime);

		public void Initialise(IGameClock clock) => _clock = clock;
	}
}
