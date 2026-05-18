using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class ObjectNodeDeserializer : INodeDeserializer
	{
		public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer,
			out object? value, ObjectDeserializer rootDeserializer)
		{
			if (expectedType != typeof(object) &&
			    expectedType != typeof(IDictionary<object, object>) &&
			    expectedType != typeof(Dictionary<object, object>))
			{
				value = null;
				return false;
			}

			var current = reader.Current;

			switch (current)
			{
				case Scalar scalar when scalar.Tag == "!var":
					value = nestedObjectDeserializer(reader, typeof(VarRefDto));
					return true;

				case Scalar scalar when scalar.Tag == "!colour":
					value = nestedObjectDeserializer(reader, typeof(ColourDto));
					return true;

				case Scalar scalar:
					reader.MoveNext();
					value = ParseScalar(scalar);
					return true;

				case MappingStart mappingStart when mappingStart.Tag == "!vec":
					value = nestedObjectDeserializer(reader, typeof(VecDto));
					return true;

				case MappingStart mappingStart when mappingStart.Tag == "!colour":
					value = nestedObjectDeserializer(reader, typeof(ColourDto));
					return true;

				case MappingStart mappingStart when mappingStart.Tag == "!expr":
					value = nestedObjectDeserializer(reader, typeof(ExprRefDto));
					return true;

				case MappingStart:
				{
					var dict = new Dictionary<string, object>();
					reader.MoveNext();

					while (!reader.TryConsume<MappingEnd>(out _))
					{
						var key = reader.Consume<Scalar>().Value;
						var entryValue = rootDeserializer(typeof(object));
						dict[key] = entryValue!;
					}

					value = dict;
					return true;
				}

				case SequenceStart:
				{
					var list = new List<object>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var item = rootDeserializer(typeof(object));
						list.Add(item!);
					}

					value = list;
					return true;
				}

				default:
					value = null;
					return false;
			}
		}

		private static object ParseScalar(Scalar scalar)
		{
			if (int.TryParse(scalar.Value, out var intValue))
			{
				return intValue;
			}

			if (float.TryParse(scalar.Value, out var floatValue))
			{
				return floatValue;
			}

			if (bool.TryParse(scalar.Value, out var boolValue))
			{
				return boolValue;
			}

			return scalar.Value;
		}
	}
}