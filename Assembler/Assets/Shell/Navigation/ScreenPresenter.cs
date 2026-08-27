using UnityEngine;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// Base for a presenter whose screen takes no parameters — the feed, the archive, the settings page.
	/// </summary>
	public abstract class ScreenPresenter : IScreenPresenter
	{
		void IScreenPresenter.Enter(IScreenParams? parameters)
		{
			// Not fatal, but never intended: somebody pushed this screen with an argument it has no way to read.
			// Silence here is an afternoon spent wondering why the id never arrived.
			if (parameters is not null)
			{
				Debug.LogWarning(
					$"{GetType().Name} takes no parameters, but was entered with {parameters.GetType().Name}. " +
					"The argument is being dropped.");
			}

			Enter();
		}

		void IScreenPresenter.Exit()
		{
			Exit();
		}

		/// <inheritdoc cref="IScreenPresenter.Enter"/>
		protected virtual void Enter()
		{
		}

		/// <inheritdoc cref="IScreenPresenter.Exit"/>
		protected virtual void Exit()
		{
		}
	}
}
