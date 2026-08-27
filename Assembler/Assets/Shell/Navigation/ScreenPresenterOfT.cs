using UnityEngine;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// Base for a presenter whose screen is pushed with a <typeparamref name="TParams"/> — it does the one cast
	/// so that no presenter has to.
	/// </summary>
	/// <typeparam name="TParams">The screen's argument type.</typeparam>
	public abstract class ScreenPresenter<TParams> : IScreenPresenter where TParams : class, IScreenParams
	{
		void IScreenPresenter.Enter(IScreenParams? parameters)
		{
			if (parameters is not null and not TParams)
			{
				Debug.LogError(
					$"{GetType().Name} expects {typeof(TParams).Name}, but was entered with " +
					$"{parameters.GetType().Name}. The screen is opening with no argument.");
			}

			Enter(parameters as TParams);
		}

		void IScreenPresenter.Exit()
		{
			Exit();
		}

		/// <inheritdoc cref="IScreenPresenter.Enter"/>
		/// <param name="parameters">
		/// Null when the screen was pushed without an argument — which a screen reached from more than one place
		/// should expect, and answer with its own empty state rather than a null reference.
		/// </param>
		protected abstract void Enter(TParams? parameters);

		/// <inheritdoc cref="IScreenPresenter.Exit"/>
		protected virtual void Exit()
		{
		}
	}
}
