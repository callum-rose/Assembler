using System;
using System.Collections.Generic;
using Assembler.Input;
using Assembler.Parsing.Info;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// Versioned JSON (<c>formatVersion</c>) serializer for <see cref="Replay"/> over the closed set of payload value
	/// types (bool, int, float, string, Vector2, Vector3, Color). Built on Newtonsoft.Json. Output is compact and
	/// preserves member/array order (payload keys are pre-sorted by the recorder), so two recordings of the same run
	/// serialize identically. Files use the <c>.replay.json</c> extension.
	/// </summary>
	public static class ReplaySerializer
	{
		public const int FormatVersion = 1;
		public const string FileExtension = ".replay.json";

		// ---- Serialize -----------------------------------------------------

		public static string Serialize(Replay replay)
		{
			var frames = new JArray();
			foreach (var frame in replay.Frames)
			{
				frames.Add(SerializeFrame(frame));
			}

			var root = new JObject
			{
				["formatVersion"] = FormatVersion,
				["descriptorHash"] = replay.DescriptorHash,
				["seed"] = replay.Seed,
				["fixedDeltaTime"] = replay.FixedDeltaTime,
				["platform"] = replay.Platform.ToString(),
				["frames"] = frames
			};

			return root.ToString(Formatting.None);
		}

		private static JObject SerializeFrame(InputFrame frame)
		{
			var activations = new JArray();
			foreach (var activation in frame.Activations)
			{
				activations.Add(SerializeActivation(activation));
			}

			return new JObject
			{
				["tick"] = frame.Tick,
				["activations"] = activations
			};
		}

		private static JObject SerializeActivation(InputActivation activation)
		{
			var payload = new JArray();
			foreach (var entry in activation.Payload)
			{
				var (type, value) = EncodeValue(entry.Value);
				payload.Add(new JObject
				{
					["key"] = entry.Key,
					["type"] = type,
					["value"] = value
				});
			}

			return new JObject
			{
				["entityId"] = activation.Trigger.EntityId,
				["behaviourId"] = activation.Trigger.BehaviourId,
				["payload"] = payload
			};
		}

		private static (string type, JToken value) EncodeValue(object value) => value switch
		{
			bool b => ("bool", new JValue(b)),
			int i => ("int", new JValue(i)),
			float f => ("float", new JValue(f)),
			string s => ("string", new JValue(s)),
			Vector2 v => ("vector2", new JArray { v.x, v.y }),
			Vector3 v => ("vector3", new JArray { v.x, v.y, v.z }),
			Color c => ("color", new JArray { c.r, c.g, c.b, c.a }),
			_ => throw new NotSupportedException(
				$"Replay payload value of type '{value?.GetType().FullName ?? "null"}' is not serializable.")
		};

		// ---- Deserialize ---------------------------------------------------

		public static Replay Deserialize(string json)
		{
			var root = JObject.Parse(json);

			var version = (int)root["formatVersion"]!;
			if (version != FormatVersion)
			{
				throw new NotSupportedException($"Unsupported replay formatVersion {version} (expected {FormatVersion}).");
			}

			var hash = (string)root["descriptorHash"]!;
			var seed = (uint)root["seed"]!;
			var fixedDeltaTime = (float)root["fixedDeltaTime"]!;
			var platform = (InputPlatform)Enum.Parse(typeof(InputPlatform), (string)root["platform"]!);

			var frames = new List<InputFrame>();
			foreach (var frameToken in (JArray)root["frames"]!)
			{
				var frame = (JObject)frameToken;
				var tick = (int)frame["tick"]!;

				var activations = new List<InputActivation>();
				foreach (var activationToken in (JArray)frame["activations"]!)
				{
					var activation = (JObject)activationToken;
					var descriptor = new BehaviourDescriptor(
						(string)activation["entityId"]!, (string)activation["behaviourId"]!);

					var payload = new List<KeyValuePair<string, object>>();
					foreach (var entryToken in (JArray)activation["payload"]!)
					{
						var entry = (JObject)entryToken;
						var key = (string)entry["key"]!;
						var type = (string)entry["type"]!;
						payload.Add(new KeyValuePair<string, object>(key, DecodeValue(type, entry["value"]!)));
					}

					activations.Add(new InputActivation(descriptor, payload));
				}

				frames.Add(new InputFrame(tick, activations));
			}

			return new Replay(hash, seed, fixedDeltaTime, platform, frames);
		}

		private static object DecodeValue(string type, JToken value) => type switch
		{
			"bool" => (bool)value,
			"int" => (int)value,
			"float" => (float)value,
			"string" => (string)value!,
			"vector2" => new Vector2((float)value[0]!, (float)value[1]!),
			"vector3" => new Vector3((float)value[0]!, (float)value[1]!, (float)value[2]!),
			"color" => new Color((float)value[0]!, (float)value[1]!, (float)value[2]!, (float)value[3]!),
			_ => throw new NotSupportedException($"Unknown replay payload value type '{type}'.")
		};
	}
}
