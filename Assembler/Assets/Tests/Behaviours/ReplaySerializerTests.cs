using System.Collections.Generic;
using Assembler.Building.Replay;
using Assembler.Input;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Locks in the replay wire format: serialization is canonical (round-trips byte-identically) and every value
	/// in the closed payload type set survives a serialize/deserialize round trip exactly. See the Determinism
	/// (Level 1) section in CLAUDE.md.
	/// </summary>
	public class ReplaySerializerTests
	{
		private static Replay SampleReplay()
		{
			var activation1 = new InputActivation(
				new BehaviourDescriptor("snake", "move"),
				new List<KeyValuePair<string, object>>
				{
					new("axis", new Vector2(1f, 0f)),
					new("speed", 2.5f),
					new("steps", 3),
				});

			var activation2 = new InputActivation(
				new BehaviourDescriptor("player", "look"),
				new List<KeyValuePair<string, object>>
				{
					new("active", true),
					new("name", "p1\"quoted\""),
					new("position", new Vector3(-2.25f, 4f, 0.5f)),
					new("tint", new Color(0.1f, 0.2f, 0.3f, 1f)),
				});

			return new Replay(
				"deadbeef",
				12345u,
				1f / 60f,
				InputPlatform.Desktop,
				new List<InputFrame>
				{
					new(0, new List<InputActivation> { activation1 }),
					new(5, new List<InputActivation> { activation2 }),
				});
		}

		[Test]
		public void RoundTrips_ByteIdentically()
		{
			var replay = SampleReplay();

			var first = ReplaySerializer.Serialize(replay);
			var reparsed = ReplaySerializer.Deserialize(first);
			var second = ReplaySerializer.Serialize(reparsed);

			Assert.AreEqual(first, second);
		}

		[Test]
		public void Serialize_IsDeterministic()
		{
			Assert.AreEqual(ReplaySerializer.Serialize(SampleReplay()), ReplaySerializer.Serialize(SampleReplay()));
		}

		[Test]
		public void PreservesHeader()
		{
			var replay = ReplaySerializer.Deserialize(ReplaySerializer.Serialize(SampleReplay()));

			Assert.AreEqual("deadbeef", replay.DescriptorHash);
			Assert.AreEqual(12345u, replay.Seed);
			Assert.AreEqual(1f / 60f, replay.FixedDeltaTime);
			Assert.AreEqual(InputPlatform.Desktop, replay.Platform);
			Assert.AreEqual(2, replay.Frames.Count);
		}

		[Test]
		public void PreservesPayloadValuesAndTypes()
		{
			var replay = ReplaySerializer.Deserialize(ReplaySerializer.Serialize(SampleReplay()));

			var frame0 = replay.Frames[0];
			Assert.AreEqual(0, frame0.Tick);
			var move = frame0.Activations[0];
			Assert.AreEqual(new BehaviourDescriptor("snake", "move"), move.Trigger);

			var byKey = ToMap(move.Payload);
			Assert.AreEqual(new Vector2(1f, 0f), byKey["axis"]);
			Assert.AreEqual(2.5f, byKey["speed"]);
			Assert.IsInstanceOf<float>(byKey["speed"]);
			Assert.AreEqual(3, byKey["steps"]);
			Assert.IsInstanceOf<int>(byKey["steps"]);

			var look = ToMap(replay.Frames[1].Activations[0].Payload);
			Assert.AreEqual(true, look["active"]);
			Assert.AreEqual("p1\"quoted\"", look["name"]);
			Assert.AreEqual(new Vector3(-2.25f, 4f, 0.5f), look["position"]);
			Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 1f), look["tint"]);
		}

		private static Dictionary<string, object> ToMap(IReadOnlyList<KeyValuePair<string, object>> payload)
		{
			var map = new Dictionary<string, object>();
			foreach (var entry in payload)
			{
				map[entry.Key] = entry.Value;
			}

			return map;
		}
	}
}
