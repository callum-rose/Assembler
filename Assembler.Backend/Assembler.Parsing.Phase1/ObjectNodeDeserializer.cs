using Assembler.Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Parsing.Phase1;

internal class ObjectNodeDeserializer : INodeDeserializer
{
	public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer,
		out object? value, ObjectDeserializer rootDeserializer)
	{
		if (expectedType != typeof(object))
		{
			value = null;
			return false;
		}

		var current = reader.Current;

		switch (current)
		{
			case Scalar scalar when scalar.Tag == "!ref":
				value = nestedObjectDeserializer(reader, typeof(RefDto));
				return true;
			
			case Scalar scalar:
				reader.MoveNext();
				value = ParseScalar(scalar);
				return true;

			case MappingStart mappingStart when mappingStart.Tag == "!vec":
				value = nestedObjectDeserializer(reader, typeof(VecDto));
				return true;
			
			case MappingStart:
			{
				var dict = new Dictionary<string, object>();
				reader.MoveNext();

				while (!reader.TryConsume<MappingEnd>(out _))
				{
					var key = reader.Consume<Scalar>().Value;
					var entryValue = nestedObjectDeserializer(reader, typeof(object));
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
					var item = nestedObjectDeserializer(reader, typeof(object));
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

		return scalar.Value;
	}
}