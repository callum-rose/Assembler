using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Per-GameObject cleanup sink for live-property bindings. Variables live game-globally in the
	/// <c>VariableRegistry</c>, so a destroyed entity's subscription/tick must be torn down or its <c>apply</c>
	/// would touch a dead Unity component. Added on demand by
	/// <see cref="LivePropertyBindingExtensions.BindLive{T}"/>; disposes everything it holds in
	/// <see cref="OnDestroy"/>.
	/// </summary>
	public sealed class LivePropertyBindings : MonoBehaviour
	{
		private readonly List<IDisposable> _bindings = new();

		private void OnDestroy()
		{
			foreach (var binding in _bindings)
			{
				binding.Dispose();
			}

			_bindings.Clear();
		}

		public void Add(IDisposable binding) => _bindings.Add(binding);

		public void Add(Action teardown) => _bindings.Add(new ActionDisposable(teardown));

		private sealed class ActionDisposable : IDisposable
		{
			private readonly Action _teardown;

			public ActionDisposable(Action teardown) => _teardown = teardown;

			public void Dispose() => _teardown();
		}
	}
}
