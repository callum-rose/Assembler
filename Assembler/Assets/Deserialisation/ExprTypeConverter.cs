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
			string? returnType = null;
			string[]? argumentTypes = null;
			string[]? registerTypes = null;
			string[]? registerTypeStatics = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "Do":
						@do = parser.Consume<Scalar>().Value;
						break;
					case "With":
						with = (rootDeserializer(typeof(List<object>)) as List<object>)?.ToArray();
						break;
					case "ReturnType":
						returnType = parser.Consume<Scalar>().Value;
						break;
					case "ArgumentTypes":
						argumentTypes = (rootDeserializer(typeof(List<string>)) as List<string>)?.ToArray();
						break;
					case "RegisterTypes":
						registerTypes = (rootDeserializer(typeof(List<string>)) as List<string>)?.ToArray();
						break;
					case "RegisterTypeStatics":
						registerTypeStatics = (rootDeserializer(typeof(List<string>)) as List<string>)?.ToArray();
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			if (string.IsNullOrWhiteSpace(@do))
			{
				throw new YamlException("!expr requires a non-empty 'Do' key (an expression name or an inline C# body).");
			}

			return new ExprRefDto
			{
				Do = @do,
				With = with,
				ReturnType = returnType,
				ArgumentTypes = argumentTypes,
				RegisterTypes = registerTypes,
				RegisterTypeStatics = registerTypeStatics
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
