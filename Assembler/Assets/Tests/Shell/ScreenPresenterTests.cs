using System.Text.RegularExpressions;
using Assembler.Shell.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Shell
{
	/// <summary>
	/// What a presenter is handed when its screen is entered — and what happens when it is handed the wrong
	/// thing, which is a mistake worth hearing about rather than one to absorb quietly.
	/// </summary>
	public class ScreenPresenterTests
	{
		[Test]
		public void HandsATypedPresenterItsOwnParameters()
		{
			var presenter = new TypedPresenter();
			var parameters = new Parameters("edition-014");

			((IScreenPresenter)presenter).Enter(parameters);

			Assert.AreSame(parameters, presenter.Received);
		}

		// A screen reachable from more than one place will be entered with nothing sooner or later, and that is
		// the screen's own empty state to draw — not a crash.
		[Test]
		public void HandsNullStraightThrough()
		{
			var presenter = new TypedPresenter();

			((IScreenPresenter)presenter).Enter(null);

			Assert.IsNull(presenter.Received);
			Assert.IsTrue(presenter.Entered);
		}

		[Test]
		public void ComplainsWhenTheArgumentIsTheWrongType()
		{
			var presenter = new TypedPresenter();
			LogAssert.Expect(LogType.Error, new Regex("expects Parameters"));

			((IScreenPresenter)presenter).Enter(new OtherParameters());

			Assert.IsNull(presenter.Received, "the screen should open empty rather than on somebody else's data");
		}

		[Test]
		public void ComplainsWhenAPresenterThatTakesNothingIsGivenSomething()
		{
			var presenter = new PlainPresenter();
			LogAssert.Expect(LogType.Warning, new Regex("takes no parameters"));

			((IScreenPresenter)presenter).Enter(new Parameters("edition-014"));

			Assert.IsTrue(presenter.Entered);
		}

		private sealed class Parameters : IScreenParams
		{
			public Parameters(string gameId)
			{
				GameId = gameId;
			}

			public string GameId { get; }
		}

		private sealed class OtherParameters : IScreenParams
		{
		}

		private sealed class TypedPresenter : ScreenPresenter<Parameters>
		{
			public Parameters? Received { get; private set; }

			public bool Entered { get; private set; }

			protected override void Enter(Parameters? parameters)
			{
				Received = parameters;
				Entered = true;
			}
		}

		private sealed class PlainPresenter : ScreenPresenter
		{
			public bool Entered { get; private set; }

			protected override void Enter()
			{
				Entered = true;
			}
		}
	}
}
