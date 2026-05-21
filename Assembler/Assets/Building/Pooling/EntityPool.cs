using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Building.Pooling
{
	public sealed class EntityPool
	{
		private readonly Dictionary<string, Stack<PooledEntity>> _stacks = new();
		private readonly Dictionary<string, Transform> _subRoots = new();
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
			pooled.GameObject.transform.SetParent(GetSubRoot(templateId), worldPositionStays: false);

			if (!_stacks.TryGetValue(templateId, out var stack))
			{
				_stacks[templateId] = stack = new Stack<PooledEntity>();
			}

			stack.Push(pooled);
		}

		private Transform GetSubRoot(string templateId)
		{
			if (_subRoots.TryGetValue(templateId, out var sub)) return sub;

			var go = new GameObject(templateId);
			go.transform.SetParent(GetRoot(), worldPositionStays: false);
			_subRoots[templateId] = go.transform;
			return go.transform;
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
