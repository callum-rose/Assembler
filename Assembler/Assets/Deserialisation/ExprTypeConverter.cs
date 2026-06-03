using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class ExprTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ExprRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			string? @do = null;
			object[]? with = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "Do":
						@do = parser.Consume<Scalar>().Value;
						break;
					case "With":
						var args = rootDeserializer(typeof(List<object>));
						with = ((List<object>)args).ToArray();
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			if (@do is null)
			{
				throw new YamlException("!expr requires a 'Do' key (an expression name or an inline C# body).");
			}

			return new ExprRefDto
			{
				Do = @do,
				With = with
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
