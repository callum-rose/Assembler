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

				case Scalar scalar when scalar.Tag == "!int":
					reader.MoveNext();
					value = int.Parse(scalar.Value);
					return true;

				case Scalar scalar when scalar.Tag == "!float":
					reader.MoveNext();
					value = float.Parse(scalar.Value, System.Globalization.CultureInfo.InvariantCulture);
					return true;

				case Scalar scalar when scalar.Tag == "!bool":
					reader.MoveNext();
					value = bool.Parse(scalar.Value);
					return true;

				case Scalar scalar when scalar.Tag == "!string":
					reader.MoveNext();
					value = scalar.Value;
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

				case SequenceStart sequenceStart when sequenceStart.Tag == "!vec":
				{
					var list = new List<VecDto>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var item = nestedObjectDeserializer(reader, typeof(VecDto));
						list.Add((VecDto)item!);
					}

					value = list;
					return true;
				}

				case SequenceStart sequenceStart when sequenceStart.Tag == "!colour":
				{
					var list = new List<ColourDto>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var item = nestedObjectDeserializer(reader, typeof(ColourDto));
						list.Add((ColourDto)item!);
					}

					value = list;
					return true;
				}

				case SequenceStart sequenceStart when sequenceStart.Tag == "!int":
				{
					var list = new List<int>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var s = reader.Consume<Scalar>();
						list.Add(int.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture));
					}

					value = list;
					return true;
				}

				case SequenceStart sequenceStart when sequenceStart.Tag == "!float":
				{
					var list = new List<float>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var s = reader.Consume<Scalar>();
						list.Add(float.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture));
					}

					value = list;
					return true;
				}

				case SequenceStart sequenceStart when sequenceStart.Tag == "!bool":
				{
					var list = new List<bool>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var s = reader.Consume<Scalar>();
						list.Add(bool.Parse(s.Value));
					}

					value = list;
					return true;
				}

				case SequenceStart sequenceStart when sequenceStart.Tag == "!string":
				{
					var list = new List<string>();
					reader.MoveNext();

					while (!reader.TryConsume<SequenceEnd>(out _))
					{
						var s = reader.Consume<Scalar>();
						list.Add(s.Value);
					}

					value = list;
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
