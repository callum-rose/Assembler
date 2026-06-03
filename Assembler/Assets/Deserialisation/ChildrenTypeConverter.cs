using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	/// <summary>
	/// Deserialises an entity's <c>Children</c> in either form:
	/// a sequence of anonymous children (<c>- Template: ...</c>), or a keyed
	/// mapping where the key is the child's id (matching the IDs-as-keys
	/// convention used by top-level <c>Entities</c>). For the mapping form the
	/// key is promoted to <see cref="EntityDto.Id"/> unless one is already set.
	/// </summary>
	internal class ChildrenTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(List<EntityDto>);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			var children = new List<EntityDto>();

			if (parser.TryConsume<SequenceStart>(out _))
			{
				while (!parser.TryConsume<SequenceEnd>(out _))
				{
					children.Add((EntityDto)rootDeserializer(typeof(EntityDto))!);
				}

				return children;
			}

			parser.Consume<MappingStart>();

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
