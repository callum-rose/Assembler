using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>
	/// Shared helper for the modifier camera behaviours (<c>camera noise</c>, <c>camera zoom</c>,
	/// <c>camera confiner</c>) — Cinemachine pipeline components/extensions that attach to an existing
	/// <see cref="CinemachineCamera"/>. They must sit on the same entity as a virtual-camera behaviour
	/// (<c>camera follow</c>/<c>camera orbit</c>/<c>camera group</c>) and be listed <b>after</b> it so the
	/// vcam component exists by the time the modifier initialises.
	/// </summary>
	internal static class CameraModifier
	{
		/// <summary>Resolve the <see cref="CinemachineCamera"/> on this entity, throwing a clear error naming the
		/// modifier behaviour and the required ordering when it is missing.</summary>
		public static CinemachineCamera RequireVirtualCamera(GameObject host, string behaviourName)
		{
			var cam = host.GetComponent<CinemachineCamera>();
			if (cam == null)
			{
				throw new MissingComponentException(
					$"'{behaviourName}' is a camera modifier and needs a virtual camera on the same entity. " +
					"Add a 'camera follow', 'camera orbit' or 'camera group' behaviour and list it before " +
					$"'{behaviourName}'.");
			}

			return cam;
		}
	}
}
