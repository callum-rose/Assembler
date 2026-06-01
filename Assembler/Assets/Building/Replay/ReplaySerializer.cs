using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Assembler.Input;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// Versioned JSON (<c>formatVersion</c>) serializer for <see cref="Replay"/>, hand-rolled over the closed set of
	/// payload value types (bool, int, float, string, Vector2, Vector3, Color) so it needs no third-party JSON
	/// dependency. Output is canonical (no incidental whitespace, sorted payload keys) so two recordings of the same
	/// run serialize byte-identically. Files use the <c>.replay.json</c> extension.
	/// </summary>
	public static class ReplaySerializer
	{
		public const int FormatVersion = 1;
		public const string FileExtension = ".replay.json";

		// ---- Serialize -----------------------------------------------------

		public static string Serialize(Replay replay)
		{
			var sb = new StringBuilder();
			sb.Append('{');
			WriteMemberRaw(sb, "formatVersion", FormatVersion.ToString(CultureInfo.InvariantCulture));
			sb.Append(',');
			WriteMemberRaw(sb, "descriptorHash", Quote(replay.DescriptorHash));
			sb.Append(',');
			WriteMemberRaw(sb, "seed", replay.Seed.ToString(CultureInfo.InvariantCulture));
			sb.Append(',');
			WriteMemberRaw(sb, "fixedDeltaTime", Num(replay.FixedDeltaTime));
			sb.Append(',');
			WriteMemberRaw(sb, "platform", Quote(replay.Platform.ToString()));
			sb.Append(',');

			WriteMemberRaw(sb, "frames", string.Empty);
			sb.Append('[');
			for (var f = 0; f < replay.Frames.Count; f++)
			{
				if (f > 0) sb.Append(',');
				WriteFrame(sb, replay.Frames[f]);
			}

			sb.Append(']');
			sb.Append('}');
			return sb.ToString();
		}

		private static void WriteFrame(StringBuilder sb, InputFrame frame)
		{
			sb.Append('{');
			WriteMemberRaw(sb, "tick", frame.Tick.ToString(CultureInfo.InvariantCulture));
			sb.Append(',');
			WriteMemberRaw(sb, "activations", string.Empty);
			sb.Append('[');
			for (var a = 0; a < frame.Activations.Count; a++)
			{
				if (a > 0) sb.Append(',');
				WriteActivation(sb, frame.Activations[a]);
			}

			sb.Append(']');
			sb.Append('}');
		}

		private static void WriteActivation(StringBuilder sb, InputActivation activation)
		{
			sb.Append('{');
			WriteMemberRaw(sb, "entityId", Quote(activation.Trigger.EntityId));
			sb.Append(',');
			WriteMemberRaw(sb, "behaviourId", Quote(activation.Trigger.BehaviourId));
			sb.Append(',');
			WriteMemberRaw(sb, "payload", string.Empty);
			sb.Append('[');
			for (var p = 0; p < activation.Payload.Count; p++)
			{
				if (p > 0) sb.Append(',');
				var entry = activation.Payload[p];
				sb.Append('{');
				WriteMemberRaw(sb, "key", Quote(entry.Key));
				sb.Append(',');
				WriteValue(sb, entry.Value);
				sb.Append('}');
			}

			sb.Append(']');
			sb.Append('}');
		}

		// Writes the "type" and "value" members for one payload value, dispatched on its runtime type.
		private static void WriteValue(StringBuilder sb, object value)
		{
			switch (value)
			{
				case bool b:
					WriteTypeValue(sb, "bool", b ? "true" : "false");
					break;
				case int i:
					WriteTypeValue(sb, "int", i.ToString(CultureInfo.InvariantCulture));
					break;
				case float f:
					WriteTypeValue(sb, "float", Num(f));
					break;
				case string s:
					WriteTypeValue(sb, "string", Quote(s));
					break;
				case Vector2 v2:
					WriteTypeValue(sb, "vector2", $"[{Num(v2.x)},{Num(v2.y)}]");
					break;
				case Vector3 v3:
					WriteTypeValue(sb, "vector3", $"[{Num(v3.x)},{Num(v3.y)},{Num(v3.z)}]");
					break;
				case Color c:
					WriteTypeValue(sb, "color", $"[{Num(c.r)},{Num(c.g)},{Num(c.b)},{Num(c.a)}]");
					break;
				default:
					throw new NotSupportedException(
						$"Replay payload value of type '{value?.GetType().FullName ?? "null"}' is not serializable.");
			}
		}

		private static void WriteTypeValue(StringBuilder sb, string type, string rawValue)
		{
			WriteMemberRaw(sb, "type", Quote(type));
			sb.Append(',');
			WriteMemberRaw(sb, "value", rawValue);
		}

		// Appends `"name":rawValue` — rawValue must already be a JSON token (quoted string, number, array, etc.).
		private static void WriteMemberRaw(StringBuilder sb, string name, string rawValue)
		{
			sb.Append(Quote(name)).Append(':').Append(rawValue);
		}

		private static string Num(float value) => value.ToString("R", CultureInfo.InvariantCulture);

		private static string Quote(string s)
		{
			var sb = new StringBuilder(s.Length + 2);
			sb.Append('"');
			foreach (var ch in s)
			{
				switch (ch)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default: sb.Append(ch); break;
				}
			}

			sb.Append('"');
			return sb.ToString();
		}

		// ---- Deserialize ---------------------------------------------------

		public static Replay Deserialize(string json)
		{
			var root = (Dictionary<string, object>)new JsonParser(json).Parse();

			var version = Convert.ToInt32(root["formatVersion"], CultureInfo.InvariantCulture);
			if (version != FormatVersion)
			{
				throw new NotSupportedException($"Unsupported replay formatVersion {version} (expected {FormatVersion}).");
			}

			var hash = (string)root["descriptorHash"];
			var seed = (uint)Convert.ToInt64(root["seed"], CultureInfo.InvariantCulture);
			var fixedDeltaTime = (float)Convert.ToDouble(root["fixedDeltaTime"], CultureInfo.InvariantCulture);
			var platform = (InputPlatform)Enum.Parse(typeof(InputPlatform), (string)root["platform"]);

			var frames = new List<InputFrame>();
			foreach (var frameObj in (List<object>)root["frames"])
			{
				var frame = (Dictionary<string, object>)frameObj;
				var tick = Convert.ToInt32(frame["tick"], CultureInfo.InvariantCulture);

				var activations = new List<InputActivation>();
				foreach (var activationObj in (List<object>)frame["activations"])
				{
					var activation = (Dictionary<string, object>)activationObj;
					var descriptor = new BehaviourDescriptor((string)activation["entityId"], (string)activation["behaviourId"]);

					var payload = new List<KeyValuePair<string, object>>();
					foreach (var entryObj in (List<object>)activation["payload"])
					{
						var entry = (Dictionary<string, object>)entryObj;
						var key = (string)entry["key"];
						payload.Add(new KeyValuePair<string, object>(key, ReadValue((string)entry["type"], entry["value"])));
					}

					activations.Add(new InputActivation(descriptor, payload));
				}

				frames.Add(new InputFrame(tick, activations));
			}

			return new Replay(hash, seed, fixedDeltaTime, platform, frames);
		}

		private static object ReadValue(string type, object raw)
		{
			switch (type)
			{
				case "bool": return (bool)raw;
				case "int": return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
				case "float": return (float)Convert.ToDouble(raw, CultureInfo.InvariantCulture);
				case "string": return (string)raw;
				case "vector2":
				{
					var a = (List<object>)raw;
					return new Vector2(ToFloat(a[0]), ToFloat(a[1]));
				}
				case "vector3":
				{
					var a = (List<object>)raw;
					return new Vector3(ToFloat(a[0]), ToFloat(a[1]), ToFloat(a[2]));
				}
				case "color":
				{
					var a = (List<object>)raw;
					return new Color(ToFloat(a[0]), ToFloat(a[1]), ToFloat(a[2]), ToFloat(a[3]));
				}
				default:
					throw new NotSupportedException($"Unknown replay payload value type '{type}'.");
			}
		}

		private static float ToFloat(object raw) => (float)Convert.ToDouble(raw, CultureInfo.InvariantCulture);

		/// <summary>
		/// A minimal recursive-descent JSON parser producing an object graph of
		/// <see cref="Dictionary{TKey,TValue}"/> (objects), <see cref="List{T}"/> (arrays), <see cref="string"/>,
		/// <see cref="double"/>, <see cref="bool"/>, and null. Sufficient for the replay schema this class writes.
		/// </summary>
		private sealed class JsonParser
		{
			private readonly string _s;
			private int _i;

			public JsonParser(string s) => _s = s;

			public object Parse()
			{
				var value = ParseValue();
				SkipWhitespace();
				if (_i != _s.Length)
				{
					throw new FormatException($"Trailing characters in replay JSON at index {_i}.");
				}

				return value;
			}

			private object ParseValue()
			{
				SkipWhitespace();
				var c = _s[_i];
				switch (c)
				{
					case '{': return ParseObject();
					case '[': return ParseArray();
					case '"': return ParseString();
					case 't': Expect("true"); return true;
					case 'f': Expect("false"); return false;
					case 'n': Expect("null"); return null!;
					default: return ParseNumber();
				}
			}

			private Dictionary<string, object> ParseObject()
			{
				var result = new Dictionary<string, object>();
				_i++; // '{'
				SkipWhitespace();
				if (_s[_i] == '}') { _i++; return result; }

				while (true)
				{
					SkipWhitespace();
					var key = ParseString();
					SkipWhitespace();
					Require(':');
					result[key] = ParseValue();
					SkipWhitespace();
					var c = _s[_i++];
					if (c == '}') break;
					if (c != ',') throw new FormatException($"Expected ',' or '}}' at index {_i - 1}.");
				}

				return result;
			}

			private List<object> ParseArray()
			{
				var result = new List<object>();
				_i++; // '['
				SkipWhitespace();
				if (_s[_i] == ']') { _i++; return result; }

				while (true)
				{
					result.Add(ParseValue());
					SkipWhitespace();
					var c = _s[_i++];
					if (c == ']') break;
					if (c != ',') throw new FormatException($"Expected ',' or ']' at index {_i - 1}.");
				}

				return result;
			}

			private string ParseString()
			{
				Require('"');
				var sb = new StringBuilder();
				while (true)
				{
					var c = _s[_i++];
					if (c == '"') break;
					if (c == '\\')
					{
						var esc = _s[_i++];
						sb.Append(esc switch
						{
							'"' => '"',
							'\\' => '\\',
							'/' => '/',
							'n' => '\n',
							'r' => '\r',
							't' => '\t',
							'b' => '\b',
							'f' => '\f',
							_ => esc
						});
					}
					else
					{
						sb.Append(c);
					}
				}

				return sb.ToString();
			}

			private double ParseNumber()
			{
				var start = _i;
				while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0)
				{
					_i++;
				}

				return double.Parse(_s.Substring(start, _i - start), CultureInfo.InvariantCulture);
			}

			private void SkipWhitespace()
			{
				while (_i < _s.Length && char.IsWhiteSpace(_s[_i]))
				{
					_i++;
				}
			}

			private void Require(char expected)
			{
				if (_s[_i] != expected)
				{
					throw new FormatException($"Expected '{expected}' at index {_i}.");
				}

				_i++;
			}

			private void Expect(string literal)
			{
				if (_i + literal.Length > _s.Length || _s.Substring(_i, literal.Length) != literal)
				{
					throw new FormatException($"Expected '{literal}' at index {_i}.");
				}

				_i += literal.Length;
			}
		}
	}
}
