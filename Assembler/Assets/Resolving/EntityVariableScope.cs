using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;

namespace Assembler.Resolving
{
	public sealed class EntityVariableScope : IDisposable
	{
		private Dictionary<string, IValueProvider>? _locals = new();

		/// <summary>The entity's local variables keyed by id. Empty once disposed. Used by debug tooling to inspect/edit state.</summary>
		public IEnumerable<KeyValuePair<string, IValueProvider>> All =>
			_locals ?? Enumerable.Empty<KeyValuePair<string, IValueProvider>>();

		public static EntityVariableScope Create(IEnumerable<ValueInfo> values)
		{
			var scope = new EntityVariableScope();

			foreach (var valueInfo in values)
			{
				scope.Create(valueInfo);
			}

			return scope;
		}

		public void Create(ValueInfo valueInfo)
		{
			if (_locals is null)
			{
				throw new ObjectDisposedException(nameof(EntityVariableScope));
			}

			_locals[valueInfo.Id] = VariableRegistry.BuildProvider(valueInfo);
		}

		public bool TryGet(string id, out IValueProvider provider)
		{
			if (_locals != null && _locals.TryGetValue(id, out var found))
			{
				provider = found;
				return true;
			}

			provider = null!;
			return false;
		}

		public void Dispose()
		{
			_locals = null;
		}
	}
}