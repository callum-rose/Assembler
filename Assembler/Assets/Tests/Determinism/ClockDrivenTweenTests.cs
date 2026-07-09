using System.Collections;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Input;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Determinism
{
	/// <summary>
	/// Part 2 of issue #241: DOTween animations run on the game clock, not Unity wall-clock. The regression this
	/// guards is a tween continuing to animate — and its <c>OnComplete</c> continuing to fire listener/trigger
	/// chains (game logic) — while the game is nominally paused (<c>set timescale 0</c>). Drives a linear move
	/// tween, pauses the game clock mid-tween, and asserts the tween freezes, then resumes when the clock does.
	/// </summary>
	/// <remarks>PlayMode: the <c>TweenDriver</c> pumps DOTween from <c>Update</c>, which only runs in play mode.</remarks>
	public sealed class ClockDrivenTweenTests
	{
		// A mover that, on start, slides from origin to +10x over one second, linearly (so progress is proportional
		// to elapsed game time and easy to assert). No Controls / physics / UI — just the animation under the clock.
		private const string TweenYaml = @"
Game:
  Title: Clock Tween Probe
  Description: Clock-driven tween probe (issue 241) - not a playable game.

World:
  Dimensionality: 2

Variables:
  is dead: false

Entities:
  mover:
    Position: !vec { X: 0, Y: 0 }
    Behaviours:
      start:
        Type: on start trigger
        Listeners: [ slide ]
      slide:
        Type: animation
        Properties:
          Animate: move
          End: !vec { X: 10, Y: 0, Z: 0 }
          Duration: 1.0
          Easing: linear

  referee:
    Position: !vec { X: 0, Y: 0 }
    Behaviours:
      tick:
        Type: every frame trigger
        Listeners: [ gate ]
      gate:
        Type: condition gate
        Properties:
          Condition: !var is dead
        Listeners:
          - !gameover
";

		[UnityTest]
		public IEnumerator Tween_pauses_and_resumes_with_the_game_clock()
		{
			var gameDto = new GameFileParser().Parse(TweenYaml);
			var gameInfo = Transformer.Transform(gameDto);
			var controls = ControlsTransformer.Transform(gameDto.Controls);

			var resolveTask = gameInfo.ResolveAsync(controls, InputPlatform.Auto);
			yield return new WaitUntil(() => resolveTask.IsCompleted);
			if (resolveTask.IsFaulted)
			{
				throw resolveTask.Exception!;
			}

			var root = resolveTask.Result.Instantiate(new RunOptions(GameClockMode.FixedStep, 1u));
			var clock = root.GetComponent<GameClockDriver>().Clock;
			var mover = root.GetComponentsInChildren<GameEntity>(true).First(e => e.Id == "mover").transform;

			// Let the on-start trigger fire and the tween advance for a few frames (well short of its 1s duration).
			for (var f = 0; f < 10; f++)
			{
				yield return null;
			}

			Assert.That(mover.position.x, Is.GreaterThan(0.01f), "Tween did not advance under the running clock.");

			// Pause the game clock. The clock snapshots its delta once per frame, so let one frame pass for the
			// pause to take effect, then record the frozen position.
			clock.Pause();
			yield return null;
			var pausedX = mover.position.x;

			for (var f = 0; f < 15; f++)
			{
				yield return null;
			}

			Assert.That(mover.position.x, Is.EqualTo(pausedX).Within(1e-5f),
				"Tween advanced while the game clock was paused — it is still running on Unity time.");

			// Resume: the tween picks up where it left off.
			clock.Resume();
			for (var f = 0; f < 10; f++)
			{
				yield return null;
			}

			Assert.That(mover.position.x, Is.GreaterThan(pausedX + 1e-3f), "Tween did not resume with the game clock.");

			Object.Destroy(root);
			yield return null;
		}
	}
}
