using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class ColourDtoExtensions
	{
		public static Color ToColor(this ColourDto dto, IReadOnlyList<ValueInfo> resolvedValues)
		{
			if (dto.Raw is not null)
			{
				return ParseRawColour(dto.Raw);
			}

			return new Color(
				FloatHelper.Resolve(dto.R, resolvedValues),
				FloatHelper.Resolve(dto.G, resolvedValues),
				FloatHelper.Resolve(dto.B, resolvedValues),
				dto.A is not null ? FloatHelper.Resolve(dto.A, resolvedValues) : 1f
			);
		}

		public static Color ToColor(this ColourValue value, IReadOnlyList<ValueInfo> resolvedValues)
		{
			if (value.Raw is StringValue stringValue)
			{
				return ParseRawColour(stringValue.Value);
			}

			return new Color(
				FloatHelper.Resolve(value.R, resolvedValues),
				FloatHelper.Resolve(value.G, resolvedValues),
				FloatHelper.Resolve(value.B, resolvedValues),
				value.A is not NoValue ? FloatHelper.Resolve(value.A, resolvedValues) : 1f
			);
		}

		private static Color ParseRawColour(string raw)
		{
			var trimmed = raw.Trim();

			if (ColorUtility.TryParseHtmlString(trimmed, out var color))
			{
				return color;
			}

			if (trimmed.StartsWith("#"))
			{
				throw new ParsingException($"Invalid hex colour: '{trimmed}'");
			}

			return trimmed.ToLowerInvariant() switch
			{
				"red" => Color.red,
				"green" => Color.green,
				"blue" => Color.blue,
				"white" => Color.white,
				"black" => Color.black,
				"yellow" => Color.yellow,
				"cyan" => Color.cyan,
				"magenta" => Color.magenta,
				"grey" or "gray" => Color.grey,
				"clear" => Color.clear,
				_ => throw new ParsingException($"Unknown colour name: '{trimmed}'")
			};
		}
	}
}
