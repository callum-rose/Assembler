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
			IReadOnlyList<ExprArgDto>? with = null;
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
						with = ReadWithMap(parser, rootDeserializer);
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

		// `With` is a map of `name: value` operands. Each value resolves through the same root
		// deserializer used for any nested value, so tags (!var, !clock, nested !expr, …) are honoured.
		// Declaration order is preserved so positional hints like ArgumentTypes still line up.
		private static IReadOnlyList<ExprArgDto> ReadWithMap(IParser parser, ObjectDeserializer rootDeserializer)
		{
			if (parser.Current is SequenceStart)
			{
				throw new YamlException(
					"!expr 'With' must be a map of 'name: value' operands (e.g. With: { velocity: !var bird velocity }), " +
					"not a sequence. The positional arg0/arg1 form has been removed.");
			}

			parser.Consume<MappingStart>();

			var args = new List<ExprArgDto>();
			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var name = parser.Consume<Scalar>().Value;
				var value = rootDeserializer(typeof(object));
				args.Add(new ExprArgDto(name, value));
			}

			return args;
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
