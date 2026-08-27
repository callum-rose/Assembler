using System;
using System.Reflection;

namespace Tests.Shell
{
	/// <summary>
	/// Writes the private serialized fields the shell's assets and views are configured through.
	/// </summary>
	/// <remarks>
	/// The fields are private on purpose — everything outside a component reads it through its own API — so a
	/// test that has to stand one up in code has the same job the editor's inspector does. Doing it here rather
	/// than through <c>SerializedObject</c> keeps the same helper usable from the play-mode tests, which cannot
	/// reference <c>UnityEditor</c>.
	/// </remarks>
	internal static class ShellReflection
	{
		public static T Set<T>(T target, string field, object? value)
		{
			var info = Field(typeof(T), field);
			info.SetValue(target, value);

			return target;
		}

		private static FieldInfo Field(Type type, string name)
		{
			for (var current = type; current is not null; current = current.BaseType)
			{
				var found = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

				if (found is not null)
				{
					return found;
				}
			}

			throw new ArgumentException($"No private field '{name}' on {type.Name}.", nameof(name));
		}
	}
}
