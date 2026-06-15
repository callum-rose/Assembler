using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Building
{
	/// <summary>
	/// A pool of recycled entity GameObject shells, keyed by template id. Keying by template id alone is safe:
	/// <c>TemplateInstantiator</c> substitution only changes resolved <em>values</em>, never the behaviour/child
	/// set, and <c>Spawn</c> passes no additional behaviours, so every instance of a template id has an identical
	/// component graph — any rented shell can be rebuilt for any spawn of the same template. Returned shells are
	/// inactive; rented shells are returned as-is for the factory to reconfigure and reactivate. Growth is lazy:
	/// the pool only ever holds shells that have actually been despawned, so games that never destroy keep today's
	/// behaviour and pay nothing.
	/// </summary>
	public sealed class EntityPool
	{
		private readonly Dictionary<string, Stack<GameObject>> _byTemplate = new();

		/// <summary>Rents a recycled shell for <paramref name="templateId"/>, or returns <c>false</c> if none are
		/// pooled (the caller then builds a fresh one).</summary>
		public bool TryRent(string templateId, out GameObject shell)
		{
			if (_byTemplate.TryGetValue(templateId, out var stack) && stack.Count > 0)
			{
				shell = stack.Pop();
				return true;
			}

			shell = null!;
			return false;
		}

		/// <summary>Returns a despawned shell to its template's pool for later reuse.</summary>
		public void Return(string templateId, GameObject shell)
		{
			if (!_byTemplate.TryGetValue(templateId, out var stack))
			{
				_byTemplate[templateId] = stack = new Stack<GameObject>();
			}

			stack.Push(shell);
		}
	}
}
