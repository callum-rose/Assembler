using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// Base for the shell's asset-backed enums — <see cref="ColorRole"/> and <see cref="TextStyleId"/>. A member
	/// is an asset rather than a number, so everything that binds one binds it by GUID: renaming a member,
	/// reordering the folder or deleting one in the middle cannot silently repaint the app, and adding one is a
	/// new asset rather than a code change.
	/// </summary>
	/// <remarks>
	/// The inspector draws these as a dropdown of every member asset of the type (see
	/// <c>Editor/ScriptableEnumDrawer</c>), so authoring one still feels like picking an enum member.
	/// </remarks>
	public abstract class ScriptableEnum : ScriptableObject
	{
		[Tooltip("What this member is for. Documentation for whoever picks it — nothing reads it at runtime.")]
		[TextArea(2, 4)]
		[SerializeField] private string description = string.Empty;

		/// <summary>What this member is for, as authored on the asset.</summary>
		public string Description => description;

		/// <summary>The member's name. Overridden so a warning names the member, not its type as well.</summary>
		public override string ToString()
		{
			return this == null ? "(missing)" : name;
		}
	}
}
