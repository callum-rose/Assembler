using Assembler.Definitions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Parsing;

public class ValueOrReferenceConverter : IYamlTypeConverter
{
	public bool Accepts(Type type) => type.IsAssignableTo(typeof(ValueOrReference));

	public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
	{
		if (parser.TryConsume<Scalar>(out var scalar))
		{
			return scalar.Tag switch
			{
				{ IsEmpty: true } => scalar.Value switch
				{
					var value when int.TryParse(value, out var intValue) => new Value<int>(intValue),
					var value when float.TryParse(value, out var floatValue) => new Value<float>(floatValue),
					var value when bool.TryParse(value, out var boolValue) => new Value<bool>(boolValue),
					_ => new Reference(scalar.Value)// Treat as reference, not string
				},
				{ Value: "!int" } => new Value<int>(int.Parse(scalar.Value)),
				{ Value: "!float" } => new Value<float>(float.Parse(scalar.Value)),
				{ Value: "!bool" } => new Value<bool>(bool.Parse(scalar.Value)),
				{ Value: "!string" } => new Value<string>(scalar.Value),
				{ Value: "!ref" } => new Reference(scalar.Value),
				_ => throw new YamlException(scalar.Start, scalar.End, $"Unknown tag: {scalar.Tag}")
			};
		}

		if (parser.Current is MappingStart { Tag: { IsEmpty: false, Value: "!vec" } })
		{
			return ParseVector(parser, rootDeserializer);
		}

		throw new YamlException("Expected a scalar or !vec mapping value.");
	}

	public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
	{
		if (value is not ValueOrReference valueOrRef)
		{
			throw new ArgumentException($"Expected ValueOrReference, got {value?.GetType()}");
		}

		switch (valueOrRef)
		{
			case Value<int> intValue:
				emitter.Emit(new Scalar(null, "!int", intValue.Val.ToString(), ScalarStyle.Plain, false, false));
				break;
			case Value<float> floatValue:
				emitter.Emit(
					new Scalar(null, "!float", floatValue.Val.ToString("F10"), ScalarStyle.Plain, false, false));
				break;
			case Value<bool> boolValue:
				emitter.Emit(new Scalar(null,
					"!bool",
					boolValue.Val.ToString().ToLower(),
					ScalarStyle.Plain,
					false,
					false));
				break;
			case Value<string> stringValue:
				emitter.Emit(new Scalar(null, "!string", stringValue.Val, ScalarStyle.DoubleQuoted, false, false));
				break;
			case Value<VectorDef> vectorValue:
				WriteVector(emitter, vectorValue.Val, serializer);
				break;
			case Reference reference:
				emitter.Emit(new Scalar(null, "!ref", reference.Ref, ScalarStyle.Plain, true, false));
				break;
			case None:
				emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Plain, true, false));
				break;
			default:
				throw new NotSupportedException($"Unsupported ValueOrReference type: {valueOrRef.GetType()}");
		}
	}

	private Value<VectorDef> ParseVector(IParser parser, ObjectDeserializer rootDeserializer)
	{
		parser.Consume<MappingStart>();

		ValueOrReference<float> x = new None<float>(), y = new None<float>(), z = new None<float>();

		while (!parser.TryConsume<MappingEnd>(out _))
		{
			var key = parser.Consume<Scalar>();
			var value = (ValueOrReference<float>)ReadYaml(parser, typeof(ValueOrReference<float>), rootDeserializer);

			switch (key.Value)
			{
				case "x":
					x = value;
					break;
				case "y":
					y = value;
					break;
				case "z":
					z = value;
					break;
				default:
					throw new YamlException(key.Start, key.End, $"Unknown vector component: {key.Value}");
			}
		}

		if (x is None<float> || y is None<float>)
		{
			throw new YamlException("Vector must have x and y (but not necessarily z) components");
		}

		return new Value<VectorDef>(new VectorDef { X = x, Y = y, Z = z });
	}

	private void WriteVector(IEmitter emitter, VectorDef vectorDef, ObjectSerializer serializer)
	{
		emitter.Emit(new MappingStart(null, "!vec", false, MappingStyle.Flow));

		emitter.Emit(new Scalar("x"));
		WriteYaml(emitter, vectorDef.X, typeof(ValueOrReference<float>), serializer);

		emitter.Emit(new Scalar("y"));
		WriteYaml(emitter, vectorDef.Y, typeof(ValueOrReference<float>), serializer);

		if (vectorDef.Z is not None<float>)
		{
			emitter.Emit(new Scalar("z"));
			WriteYaml(emitter, vectorDef.Z, typeof(ValueOrReference<float>), serializer);
		}

		emitter.Emit(new MappingEnd());
	}
}