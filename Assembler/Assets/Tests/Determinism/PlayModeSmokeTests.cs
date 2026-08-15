using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Determinism
{
	// Isolates the headless PlayMode runner from the real determinism test: a trivial [UnityTest] that just
	// advances a frame and passes. If this runs green headlessly but the determinism test hangs, the fault is in
	// the determinism test; if this hangs too, it's the batch-mode PlayMode runner / play→edit transition.
	public sealed class PlayModeSmokeTests
	{
		[UnityTest]
		public IEnumerator Advances_a_frame_and_passes()
		{
			yield return null;
			Assert.Pass();
		}
	}
}
