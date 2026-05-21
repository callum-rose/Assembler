using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class EntityChildTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(EntityChildDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			if (parser.Accept<Scalar>(out var scalar))
			{
				parser.MoveNext();
				return new EntityChildDto
				{
					Ref = scalar.Value
				};
			}

			var entity = rootDeserializer(typeof(EntityDto));

			return new EntityChildDto
			{
				Entity = (EntityDto?)entity
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
