using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	/// <summary>
	/// Reads a single <see cref="ListenerDto"/>, which a descriptor may author two ways: as a scalar
	/// shorthand or as the full mapping.
	/// <list type="bullet">
	/// <item><c>move left</c> — a behaviour on the listener's own entity (EntityId defaults to self).</item>
	/// <item><c>score tracker / increment score</c> — an explicit <c>entity / behaviour</c> pair; the split is
	/// on the last <c>/</c> so child-entity paths (<c>panel/score</c>) work as the entity part.</item>
	/// <item>The mapping form <c>{ EntityId:, BehaviourId:, EntityTag:, BehaviourTag:, Outputs: }</c>.</item>
	/// </list>
	/// The <c>!gameover</c> tag deserialises to <see cref="GameOverListenerDto"/> via its own converter, so it
	/// never reaches this one.
	/// </summary>
	internal class ListenerTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ListenerDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			if (parser.TryConsume<Scalar>(out var scalar))
			{
				var slash = scalar.Value.LastIndexOf('/');

				return slash < 0
					? new ListenerDto { BehaviourId = scalar.Value.Trim() }
					: new ListenerDto
					{
						EntityId = scalar.Value[..slash].Trim(),
						BehaviourId = scalar.Value[(slash + 1)..].Trim()
					};
			}

			parser.Consume<MappingStart>();

			object? entityId = null;
			string? behaviourId = null;
			object? entityTag = null;
			object? behaviourTag = null;
			Dictionary<string, string>? outputs = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					// EntityId may be a tagged value (e.g. !var / !parameter), so defer to the root deserializer
					// rather than consuming a bare scalar.
					case "EntityId":
						entityId = rootDeserializer(typeof(object));
						break;
					case "BehaviourId":
						behaviourId = parser.Consume<Scalar>().Value;
						break;
					case "EntityTag":
						entityTag = rootDeserializer(typeof(object));
						break;
					case "BehaviourTag":
						behaviourTag = rootDeserializer(typeof(object));
						break;
					case "Outputs":
						outputs = (Dictionary<string, string>?)rootDeserializer(typeof(Dictionary<string, string>));
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			return new ListenerDto
			{
				EntityId = entityId,
				BehaviourId = behaviourId,
				EntityTag = entityTag,
				BehaviourTag = behaviourTag,
				Outputs = outputs
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
