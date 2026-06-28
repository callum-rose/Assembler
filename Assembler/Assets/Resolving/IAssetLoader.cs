using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.Resolving
{
	/// <summary>
	/// Loads a single asset by path. The path is always async — a caller can't know whether a given asset id
	/// happens to be remote (Addressables), so a synchronous load that secretly blocks would be a leaky
	/// abstraction. Loaders that have no real async work (Resources) return an already-completed task.
	/// </summary>
	public interface IAssetLoader
	{
		Task<T> LoadAsync<T>(string path) where T : Object;
	}
}
