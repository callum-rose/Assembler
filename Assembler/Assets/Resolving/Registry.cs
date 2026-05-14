using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	public class Registry<T>
	{
		private readonly Dictionary<string, T> _items = new();
		
		public void Register(string id, T gameEntity)
		{
			_items[id] = gameEntity;
		}
		
		public T Get(string id)
		{
			if (!_items.TryGetValue(id, out var entity))
			{
				throw new Exception($"Item not registered for id: {id}");
			}

			return entity;
		}
	}
}