using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Time
{
	/// <summary>Sets the game clock's time scale when Executed by an upstream trigger. A scale of 0 pauses gameplay, 0.5 is slow-motion, 1 is normal speed.</summary>
	/// <remarks>
	/// Properties:
	///   Scale: Playback rate applied to the shared game clock; 0 pauses, 0.5 halves speed, 1 is normal. Negative values are clamped to 0.
	/// </remarks>
	public class SetTimeScale : GameBehaviour<SetTimeScaleData>, INeedsGameClock, IAmExecutable
	{
		public IGameClock Clock { get; set; } = null!;

		public void Execute(TriggerContext ctx)
		{
			Clock.TimeScale = Data.Scale.Get(ctx);
		}
	}
}
