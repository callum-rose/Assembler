using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Info-layer reference to another entity used by camera behaviours (follow, look-at, group, …).
	/// A discriminated union parsed from a structured property: <c>{ Tag: player }</c> resolves every
	/// entity carrying the entity-tag (re-queried at runtime so spawned entities are caught), while
	/// <c>{ Id: player }</c> resolves the single entity with that id (captured at build time).
	/// </summary>
	public abstract record CameraTargetSource
	{
		public abstract CameraTargetSource SubstituteParameters(TransformContext ctx);

		/// <summary>Parse a single <c>{ Tag: … }</c> / <c>{ Id: … }</c> target, or null when the property is absent.</summary>
		public static CameraTargetSource? Parse(TransformContext ctx, AssemblerValue? raw, string behaviourId, string field)
		{
			if (raw is null or NoValue)
			{
				return null;
			}

			if (raw is not DictValue dict)
			{
				throw new ParsingException(
					$"Camera behaviour '{behaviourId}': {field} must be a map of the form {{ Tag: <entity-tag> }} or {{ Id: <entity-id> }}.");
			}

			var hasTag = dict.Value.TryGetValue("Tag", out var tagValue) && tagValue is not NoValue;
			var hasId = dict.Value.TryGetValue("Id", out var idValue) && idValue is not NoValue;

			if (hasTag == hasId)
			{
				throw new ParsingException(
					$"Camera behaviour '{behaviourId}': {field} must specify exactly one of 'Tag' or 'Id'.");
			}

			if (hasTag)
			{
				return new TagTarget(Transformer.CreateValueSource<string>(ctx, tagValue!));
			}

			return idValue is StringValue s
				? new IdTarget(s.Value)
				: throw new ParsingException(
					$"Camera behaviour '{behaviourId}': {field} 'Id' must be a literal entity id string.");
		}

		/// <summary>Parse a list of targets (for <c>camera group</c>). Accepts a list of <c>{ Tag/Id }</c> maps.</summary>
		public static IReadOnlyList<CameraTargetSource> ParseList(TransformContext ctx, AssemblerValue? raw, string behaviourId, string field)
		{
			if (raw is null or NoValue)
			{
				return System.Array.Empty<CameraTargetSource>();
			}

			if (raw is not ListValue list)
			{
				throw new ParsingException(
					$"Camera behaviour '{behaviourId}': {field} must be a list of {{ Tag: … }} / {{ Id: … }} entries.");
			}

			return list.Value
				.Select(item => Parse(ctx, item, behaviourId, field)
					?? throw new ParsingException($"Camera behaviour '{behaviourId}': {field} contains an empty entry."))
				.ToArray();
		}
	}

	/// <summary>Resolves all entities carrying <see cref="Tag"/>, re-queried at runtime (catches spawns).</summary>
	public sealed record TagTarget(ValueSource<string> Tag) : CameraTargetSource
	{
		public override CameraTargetSource SubstituteParameters(TransformContext ctx) =>
			new TagTarget(Tag.SubstituteParameters(ctx));
	}

	/// <summary>Resolves the single entity with <see cref="Id"/>, captured from the build-time transform registry.</summary>
	public sealed record IdTarget(string Id) : CameraTargetSource
	{
		public override CameraTargetSource SubstituteParameters(TransformContext ctx) => this;
	}
}
