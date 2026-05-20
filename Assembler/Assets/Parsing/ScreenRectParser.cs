using System.Collections.Generic;
using System.Globalization;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	internal static class ScreenRectParser
	{
		internal static ScreenRect Parse(object? raw)
		{
			IReadOnlyDictionary<string, AssemblerValue>? dict = raw switch
			{
				DictValue dv => dv.Value,
				_ => null
			};

			string Get(string key)
			{
				if (dict != null && dict.TryGetValue(key, out var av))
					return AssemblerValueToString(av);
				if (raw is Dictionary<object, object> d1 && d1.TryGetValue(key, out var v1))
					return v1?.ToString() ?? string.Empty;
				if (raw is Dictionary<string, object> d2 && d2.TryGetValue(key, out var v2))
					return v2?.ToString() ?? string.Empty;
				return string.Empty;
			}

			float GetFloat(string key) =>
				float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

			var anchorStr = Get("Anchor").ToLowerInvariant().Replace("-", "").Replace(" ", "");
			var anchor = anchorStr switch
			{
				"topleft" => AnchorPoint.TopLeft,
				"topright" => AnchorPoint.TopRight,
				"bottomleft" => AnchorPoint.BottomLeft,
				"bottomright" => AnchorPoint.BottomRight,
				"center" => AnchorPoint.Center,
				_ => AnchorPoint.TopLeft
			};

			return new ScreenRect
			{
				Anchor = anchor,
				X = GetFloat("X"),
				Y = GetFloat("Y"),
				Width = GetFloat("Width"),
				Height = GetFloat("Height")
			};
		}

		private static string AssemblerValueToString(AssemblerValue value) => value switch
		{
			StringValue s => s.Value,
			IntValue i => i.Value.ToString(CultureInfo.InvariantCulture),
			FloatValue f => f.Value.ToString(CultureInfo.InvariantCulture),
			BoolValue b => b.Value.ToString(),
			_ => string.Empty
		};
	}
}
