using System.Collections.Generic;

namespace Assembler.Input
{
	/// <summary>
	/// One physical-input binding for an action on a particular platform. Either a plain Input System control
	/// path (<see cref="Path"/>, e.g. <c>&lt;Keyboard&gt;/w</c>) or a composite (<see cref="Composite"/>,
	/// e.g. <c>2DVector</c>) whose <see cref="Parts"/> map part names (Up/Down/Left/Right) to control paths.
	/// </summary>
	public sealed record BindingInfo(string? Path, string? Composite, IReadOnlyDictionary<string, string> Parts)
	{
		public bool IsComposite => Composite != null;

		public static BindingInfo Simple(string path) =>
			new(path, null, new Dictionary<string, string>());

		public static BindingInfo CompositeOf(string composite, IReadOnlyDictionary<string, string> parts) =>
			new(null, composite, parts);
	}
}
