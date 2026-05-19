using System;
using System.Collections.Generic;
using Assembler.Parsing.Info;

namespace Assembler.Resolving
{
	public sealed class EntityVariableScope : IDisposable
	{
		private Dictionary<string, IValueProvider>? _locals = new();

		public void Create(ValueInfo valueInfo)
		{
			if (_locals == null)
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
