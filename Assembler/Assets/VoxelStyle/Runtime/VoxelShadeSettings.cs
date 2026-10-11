using UnityEngine;

namespace Assembler.VoxelStyle
{
	/// <summary>
	/// Drives the tuning globals of the <c>Assembler/Voxel Shade</c> shader, so one inspector tunes every
	/// palette material at once. Edits apply live, in edit mode and play mode. With more than one enabled,
	/// the last to update wins.
	/// </summary>
	[ExecuteAlways]
	public sealed class VoxelShadeSettings : MonoBehaviour
	{
		public enum DebugView
		{
			Lit = 0,
			AO = 1,
			Tone = 2,
			Edge = 3,
		}

		[Header("Ambient occlusion")]
		[Tooltip("How much of the baked AO channel applies. 0 ignores it.")]
		[Range(0f, 1f)] public float aoStrength = 1f;

		[Header("Tone (top-down gradient)")]
		[Tooltip("Brightness multiplier at the top of the root part.")]
		[Range(0f, 1.5f)] public float toneTop = 1f;
		[Tooltip("Brightness multiplier at the bottom of the root part, for dark and mid colours.")]
		[Range(0f, 1.5f)] public float toneBottom = 0.75f;
		[Tooltip("Brightness multiplier at the bottom for light colours (whites), which read as grey if darkened as much.")]
		[Range(0f, 1.5f)] public float toneBottomLight = 0.9f;
		[Tooltip("Luminance from which a colour starts blending toward the light-colour bottom; fully there at 1.")]
		[Range(0f, 0.99f)] public float toneLightFrom = 0.5f;

		[Header("Edge light (convex edges)")]
		[Tooltip("Brightness lift on a convex edge: 0.2 is 20% brighter.")]
		[Range(0f, 1f)] public float edgeStrength = 0.2f;
		[Tooltip("How far the edge light reaches into a face, in voxels.")]
		[Range(0.01f, 0.5f)] public float edgeWidth = 0.35f;

		[Header("Surface")]
		[Range(0f, 1f)] public float smoothness = 0f;

		[Header("Debug")]
		public DebugView debugView = DebugView.Lit;

		static readonly int AOStrengthId = Shader.PropertyToID("_VoxelAOStrength");
		static readonly int ToneTopId = Shader.PropertyToID("_VoxelToneTop");
		static readonly int ToneBottomId = Shader.PropertyToID("_VoxelToneBottom");
		static readonly int ToneBottomLightId = Shader.PropertyToID("_VoxelToneBottomLight");
		static readonly int ToneLightFromId = Shader.PropertyToID("_VoxelToneLightFrom");
		static readonly int EdgeStrengthId = Shader.PropertyToID("_VoxelEdgeStrength");
		static readonly int EdgeWidthId = Shader.PropertyToID("_VoxelEdgeWidth");
		static readonly int SmoothnessId = Shader.PropertyToID("_VoxelSmoothness");
		static readonly int DebugViewId = Shader.PropertyToID("_VoxelDebugView");

		void OnEnable() => Apply();

		void Update() => Apply();

		void OnValidate() => Apply();

		public void Apply()
		{
			Shader.SetGlobalFloat(AOStrengthId, aoStrength);
			Shader.SetGlobalFloat(ToneTopId, toneTop);
			Shader.SetGlobalFloat(ToneBottomId, toneBottom);
			Shader.SetGlobalFloat(ToneBottomLightId, toneBottomLight);
			Shader.SetGlobalFloat(ToneLightFromId, toneLightFrom);
			Shader.SetGlobalFloat(EdgeStrengthId, edgeStrength);
			Shader.SetGlobalFloat(EdgeWidthId, edgeWidth);
			Shader.SetGlobalFloat(SmoothnessId, smoothness);
			Shader.SetGlobalInteger(DebugViewId, (int)debugView);
		}
	}
}
