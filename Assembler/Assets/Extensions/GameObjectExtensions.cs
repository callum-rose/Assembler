using UnityEngine;

namespace Assembler.Extensions
{
	public static class GameObjectExtensions
	{
		/// <summary>
		/// Returns the component of type <typeparamref name="T"/> on the GameObject, adding one if it does
		/// not already have it. Uses Unity's overloaded equality so destroyed (fake-null) components are
		/// treated as absent.
		/// </summary>
		public static T GetOrAddComponent<T>(this GameObject go) where T : Component
		{
			var existing = go.GetComponent<T>();
			return existing != null ? existing : go.AddComponent<T>();
		}
	}
}
