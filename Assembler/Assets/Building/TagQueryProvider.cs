using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Building
{
	internal sealed class TagQueryProvider : IValueProvider<IReadOnlyList<string>>
	{
		private readonly string _entityTag;

		public TagQueryProvider(string entityTag)
		{
			_entityTag = entityTag;
		}

		public IReadOnlyList<string> Value =>
			Object.FindObjectsByType<GameEntity>(FindObjectsSortMode.None)
				.Where(e => e.Tags.Contains(_entityTag))
				.Select(e => e.gameObject.name)
				.Distinct()
				.ToArray();

		set => throw new NotSupportedException("Tag query providers are read-only.");
	}
}
