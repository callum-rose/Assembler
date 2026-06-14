using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Resolving;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Assembler.Behaviours
{
	public abstract class GameBehaviour : MonoBehaviour
	{
		[SerializeField] private string[] tags = Array.Empty<string>();

		public string[] Tags
		{
			get => tags;
			set => tags = value;
		}

		/// <summary>The entity this behaviour belongs to. Wired once by the build pipeline (see
		/// <see cref="SetEntity"/>) so every behaviour can reach its owning entity — its id, tags and scope —
		/// without a <c>GetComponent</c> lookup.</summary>
		protected GameEntity Entity { get; private set; } = null!;

		/// <summary>This behaviour's descriptor id. Wired by <see cref="SetBase"/> during the two-phase build
		/// before any behaviour runs, so it is never observed null — hence the <c>null!</c>.</summary>
		protected string Id { get; private set; } = null!;

		private IReadOnlyList<Listener> _listeners = Array.Empty<Listener>();

#if DEBUG_CONSOLE
		/// <summary>
		/// Raised whenever any behaviour notifies its listeners, just before the listeners run. Lets the
		/// debug console observe the otherwise-invisible trigger flow. Compiled out unless DEBUG_CONSOLE
		/// is defined, so release builds pay nothing.
		/// </summary>
		public static event Action<GameBehaviour, TriggerContext>? Fired;

		/// <summary>The resolved listeners this behaviour notifies when it fires. Debug-only graph inspection.</summary>
		public IReadOnlyList<Listener> DebugListeners => _listeners;

		[ShowInInspector, ReadOnly] private IReadOnlyList<GameBehaviour> _listeningBehaviours = Array.Empty<GameBehaviour>();
#endif

		/// <summary>Wires the owning entity. Called once by the build pipeline before initialisation; the
		/// private property setter keeps <see cref="Entity"/> read-only to subclasses.</summary>
		public void SetEntity(GameEntity entity) => Entity = entity;

		/// <summary>Reset transient runtime state so a pooled component is clean for reuse. Called by the build
		/// pipeline on respawn, <em>after</em> the component has been re-initialised (so <c>Data</c> reflects this
		/// spawn) — the point that mirrors Unity's <c>Start</c>, which does NOT re-fire on a reused component.
		/// Default is a no-op: stateless behaviours need nothing. Override to (a) clear private mutable state
		/// (counters, debounce timestamps, gesture fields, cached physics) that would otherwise leak from the
		/// previous life, and (b) re-arm any one-shot <c>Start</c> logic (auto-start timers, scan coroutines,
		/// initial state-machine hooks). Teardown of something <c>OnInitialise</c> itself re-creates (a live
		/// subscription, input wiring) belongs in <c>OnInitialise</c> made idempotent, not here, since this runs
		/// after it. Public (not protected) because the cross-assembly factory drives it, mirroring
		/// <c>Initialise</c>.</summary>
		public virtual void OnReuse() { }

		protected void SetBase(BehaviourData behaviourData, IReadOnlyList<Listener> listeners)
		{
			Id = behaviourData.Id;
			_listeners = listeners;

#if DEBUG_CONSOLE
			_listeningBehaviours = _listeners.SelectMany(l => l.DebugTargets()).ToArray();
#endif
		}

		protected void NotifyListeners(TriggerContext ctx)
		{
#if DEBUG_CONSOLE
			Fired?.Invoke(this, ctx);
#endif
			foreach (var listener in _listeners)
			{
				listener.Notify(ctx);
			}
		}
	}

	/// <summary>
	/// Strongly-typed <see cref="GameBehaviour"/> base that binds a behaviour to its <typeparamref name="TData"/>
	/// configuration. The resolved data is exposed via <see cref="Data"/> after <see cref="Initialise"/> runs.
	/// </summary>
	/// <typeparam name="TData">The <see cref="BehaviourData"/> type carrying this behaviour's serialized configuration.</typeparam>
	public abstract class GameBehaviour<TData> : GameBehaviour where TData : BehaviourData
	{
		/// <summary>The resolved configuration for this behaviour. Assigned by <see cref="Initialise"/> during the
		/// two-phase build before any behaviour runs, so it is never observed null — hence the <c>null!</c>.</summary>
		protected TData Data { get; private set; } = null!;

		public void Initialise(TData data, IReadOnlyList<Listener> listeners)
		{
			Data = data;

			SetBase(data, listeners);
			OnInitialise(data);
		}

		protected virtual void OnInitialise(TData data) { }
	}
}
