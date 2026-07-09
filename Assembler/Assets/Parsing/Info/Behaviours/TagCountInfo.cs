using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Parsed configuration for the <c>tag count</c> behaviour: a single required <see cref="Tag"/> naming the
	/// entity tag to count across the whole world. The count is produced at runtime as a trigger output (not a
	/// variable write), so there is no output property to configure here.
	/// </summary>
	public record TagCountInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Tag)
		: BehaviourInfo(Id, Listeners)
	{
		public static TagCountInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			if (!props.ContainsKey("Tag"))
			{
				throw new ParsingException($"tag count '{id}': 'Tag' is required (the entity tag to count).");
			}

			return new TagCountInfo(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Tag")));
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TagCountInfo(Id,
				substitutedListeners,
				Tag.SubstituteParameters(ctx));
	}
}
