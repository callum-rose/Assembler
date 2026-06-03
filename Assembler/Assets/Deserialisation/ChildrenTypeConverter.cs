using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	/// <summary>
	/// Deserialises an entity's <c>Children</c> as a keyed mapping where the key is
	/// the child's id, matching the IDs-as-keys convention used by top-level
	/// <c>Entities</c>. The key is promoted to <see cref="EntityDto.Id"/> unless one
	/// is already set. The sequence/list form is intentionally rejected so there is a
	/// single, consistent way to declare children.
	/// </summary>
	internal class ChildrenTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(List<EntityDto>);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			if (!parser.TryConsume<MappingStart>(out _))
			{
				throw new YamlException(parser.Current?.Start ?? Mark.Empty, parser.Current?.End ?? Mark.Empty,
					"Entity 'Children' must be a mapping of child-id -> child " +
					"(e.g. `Children:` then `  myChild:` on the next line). The sequence/list form " +
					"(`- ...`) is no longer supported — give each child an id key.");
			}

			var children = new List<EntityDto>();

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				var child = (EntityDto)rootDeserializer(typeof(EntityDto))!;
				children.Add(child with { Id = child.Id ?? key });
			}

			return children;
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
