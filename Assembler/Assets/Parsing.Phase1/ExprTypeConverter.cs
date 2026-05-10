using System;
using System.Collections.Generic;
using Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Parsing.Phase1
{
	internal class ExprTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ExprRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			var expr = new ExprRefDto();
			parser.Consume<MappingStart>();
			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "ExpressionId":
						expr.ExpressionId = parser.Consume<Scalar>().Value;
						break;
					case "Arguments":
						var args = rootDeserializer(typeof(List<object>));
						expr.Arguments = ((List<object>)args).ToArray();
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}
			return expr;
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
