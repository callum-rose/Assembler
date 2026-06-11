using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Input
{
	/// <summary>Locks (and optionally hides) the hardware cursor so relative mouse-look deltas keep flowing past the screen edges; applied on start and on Execute, and restored when the entity is destroyed (e.g. on game-over).</summary>
	/// <remarks>
	/// A value action bound to <c>&lt;Mouse&gt;/delta</c> only reports continuous motion while the OS pointer is
	/// locked; without this the pointer pins against the window edge and look deltas drop to zero. Wire one of
	/// these on the player (it locks on start), and optionally a second with <c>Locked: false</c> driven by a
	/// pause/menu trigger to release the cursor.
	/// Properties:
	///   Locked: Whether to lock the cursor to the window centre (default true).
	///   Visible: Whether the cursor stays visible while locked (default false).
	/// </remarks>
	public class CursorLock : GameBehaviour<CursorLockData>
	{
		protected override void OnInitialise(CursorLockData data) => Apply(TriggerContext.Empty);

		public override void Execute(TriggerContext ctx) => Apply(ctx);

		private void OnDestroy()
		{
			UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
			UnityEngine.Cursor.visible = true;
		}

		private void Apply(TriggerContext ctx)
		{
			UnityEngine.Cursor.lockState = Data.Locked.ValueOr(ctx, true)
				? UnityEngine.CursorLockMode.Locked
				: UnityEngine.CursorLockMode.None;
			UnityEngine.Cursor.visible = Data.Visible.ValueOr(ctx, false);
		}
	}
}
