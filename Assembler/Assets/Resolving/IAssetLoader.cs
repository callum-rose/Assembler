using UnityEngine;

namespace Assembler.Resolving
{
	public interface IAssetLoader
	{
		T Load<T>(string path) where T : Object;
	}
}