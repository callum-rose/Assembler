using Assembler.Definitions;
using YamlDotNet.Serialization;

namespace Assembler.Parsing;

public static class Deserialiser
{
	/// <summary>
	/// Deserializes YAML and validates the resulting configuration.
	/// Throws YamlValidationException if validation fails.
	/// </summary>
	public static GameConfigurationDef Deserialize(string yaml)
	{
		var config = DeserializeWithoutValidation(yaml);
		var validator = new GameConfigurationValidator(config);
		var errors = validator.Validate();

		if (errors.Count > 0)
		{
			throw new YamlValidationException(
				$"Game configuration validation failed with {errors.Count} error(s):\n\n" +
				string.Join("\n", errors.Select(e => $"  - {e}")));
		}

		return config;
	}

	/// <summary>
	/// Deserializes YAML without validation. Useful for testing or advanced scenarios.
	/// </summary>
	public static GameConfigurationDef DeserializeWithoutValidation(string yaml)
	{
		return new DeserializerBuilder()
			.WithTagMapping("!int", typeof(Value<int>))
			.WithTagMapping("!float", typeof(Value<float>))
			.WithTagMapping("!bool", typeof(Value<bool>))
			.WithTagMapping("!string", typeof(Value<string>))
			.WithTagMapping("!ref", typeof(Value<))
			.WithTagMapping("!vec", typeof(ValueOrReference))
			.WithTypeConverter(new ValueOrReferenceConverter())
			.WithTagMapping("!listeners", typeof(IReadOnlyList<ListenerDef>))
			.WithTypeConverter(new ListenersConverter())
			.Build()
			.Deserialize<GameConfigurationDef>(yaml);
	}
}