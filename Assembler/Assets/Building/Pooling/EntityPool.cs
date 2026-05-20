using System.Collections.Generic;
using Assembler.Behaviours;
using UnityEngine;

namespace Assembler.Building.Pooling
{
	public sealed class EntityPool
	{
		private readonly Dictionary<string, Stack<PooledEntity>> _stacks = new();
		private Transform? _root;

		public bool TryRent(string templateId, out PooledEntity pooled)
		{
			if (_stacks.TryGetValue(templateId, out var stack) && stack.Count > 0)
			{
				pooled = stack.Pop();
				return true;
			}

			pooled = null!;
			return false;
		}

		public void Return(string templateId, PooledEntity pooled)
		{
			pooled.GameObject.SetActive(false);
			pooled.GameObject.transform.SetParent(GetRoot(), worldPositionStays: false);

			if (!_stacks.TryGetValue(templateId, out var stack))
			{
				_stacks[templateId] = stack = new Stack<PooledEntity>();
			}

			stack.Push(pooled);
		}

		private Transform GetRoot()
		{
			if (_root != null) return _root;

			var go = new GameObject("[EntityPool]");
			go.SetActive(false);
			_root = go.transform;
			return _root;
		}
	}
}
