using System;
using System.Collections.Generic;
using Assembler.Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Parsing.Phase1
{
	internal class ExprTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ExprRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			string? expressionId = null;
			object[]? arguments = null;
			
			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "ExpressionId":
						expressionId = parser.Consume<Scalar>().Value;
						break;
					case "Arguments":
						var args = rootDeserializer(typeof(List<object>));
						arguments = ((List<object>)args).ToArray();
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			return new ExprRefDto
			{
				ExpressionId = expressionId,
				Arguments = arguments
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
