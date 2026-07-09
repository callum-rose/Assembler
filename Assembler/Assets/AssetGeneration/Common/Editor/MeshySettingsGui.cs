#nullable enable

using Assembler.AssetGeneration.ImageToMesh;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// The Meshy image-to-3D generation controls (model, format, texturing, geometry/remesh, output
	/// toggles) shared by the standalone Image → Mesh window and stage 2 of the Text → Voxels
	/// pipeline. Draws into a <see cref="MeshyRequest"/> draft; the reference image path and the API
	/// key are drawn by the owning window, not here.
	/// </summary>
	public static class MeshySettingsGui
	{
		// The Meshy image-to-3D AI models, newest first.
		private static readonly string[] Models = { "meshy-6", "meshy-5", "meshy-4" };

		public const string DefaultModel = "meshy-6";

		/// <summary>A draft seeded with the same defaults both windows used, minus the per-run image path.</summary>
		public static MeshyRequest Default() => new()
		{
			AiModel = DefaultModel,
			Format = ModelFormat.Obj,
			GenerateTexture = true,
			EnablePbr = true,
			HdTexture = false,
			Remesh = true,
			Topology = MeshyTopology.Triangle,
			Decimation = DecimationMode.None,
			TargetPolycount = 30000,
			SavePreRemeshedModel = false,
			RemoveLighting = true,
			Moderation = false,
			AutoSize = false,
			OriginAt = ModelOrigin.Bottom,
			MultiViewThumbnails = false,
			AlphaThumbnail = false,
		};

		public static void Draw(ref MeshyRequest r)
		{
			r.AiModel = ModelPopup.Draw("Meshy Model", r.AiModel, Models, "Meshy generation model.");
			r.Format = (ModelFormat)EditorGUILayout.EnumPopup(
				new GUIContent("Output Format", "Model format to generate and download (sent as target_formats)."), r.Format);

			EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
			r.GenerateTexture = EditorGUILayout.Toggle(
				new GUIContent("Generate Texture", "Generate a texture for the model."), r.GenerateTexture);
			using (new EditorGUI.DisabledScope(!r.GenerateTexture))
			{
				r.EnablePbr = EditorGUILayout.Toggle(
					new GUIContent("Enable PBR Maps", "Also generate metallic/roughness/normal maps."), r.EnablePbr);
				r.HdTexture = EditorGUILayout.Toggle(
					new GUIContent("HD Texture", "Generate a higher-resolution texture (hd_texture)."), r.HdTexture);
			}

			EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
			r.Remesh = EditorGUILayout.Toggle(
				new GUIContent("Remesh", "Let Meshy clean up the topology (should_remesh)."), r.Remesh);
			using (new EditorGUI.DisabledScope(!r.Remesh))
			{
				r.Topology = (MeshyTopology)EditorGUILayout.EnumPopup(
					new GUIContent("Topology", "Target face topology when remeshing (topology)."), r.Topology);
				r.Decimation = (DecimationMode)EditorGUILayout.EnumPopup(
					new GUIContent("Decimation", "Remesh decimation preset (decimation_mode). 'None' lets Meshy decide, or set a target polycount instead."),
					r.Decimation);
				// target_polycount is the alternative to a decimation preset, so only offer it when no preset is chosen.
				using (new EditorGUI.DisabledScope(r.Decimation != DecimationMode.None))
				{
					r.TargetPolycount = EditorGUILayout.IntSlider(
						new GUIContent("Target Polycount", "Target triangle count when remeshing (target_polycount, 100–300000)."),
						r.TargetPolycount, 100, 300000);
				}
				r.SavePreRemeshedModel = EditorGUILayout.Toggle(
					new GUIContent("Save Pre-Remeshed Model", "Also keep the model before remeshing (save_pre_remeshed_model)."),
					r.SavePreRemeshedModel);
			}

			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			// remove_lighting is only supported on meshy-6; grey it out (and force false) for other models.
			var supportsRemoveLighting = r.AiModel == "meshy-6";
			if (!supportsRemoveLighting)
			{
				r.RemoveLighting = false;
			}

			using (new EditorGUI.DisabledScope(!supportsRemoveLighting))
			{
				r.RemoveLighting = EditorGUILayout.Toggle(
					new GUIContent("Remove Lighting", "Bake out baked-in lighting from the source image (remove_lighting). Only available on meshy-6."),
					r.RemoveLighting);
			}
			r.AutoSize = EditorGUILayout.Toggle(
				new GUIContent("Auto Size", "Auto-scale the model to a realistic size (auto_size)."), r.AutoSize);
			r.OriginAt = (ModelOrigin)EditorGUILayout.EnumPopup(
				new GUIContent("Origin At", "Where the model's pivot sits (origin_at)."), r.OriginAt);
			r.Moderation = EditorGUILayout.Toggle(
				new GUIContent("Moderation", "Run content moderation on the input (moderation)."), r.Moderation);
			r.MultiViewThumbnails = EditorGUILayout.Toggle(
				new GUIContent("Multi-View Thumbnails", "Generate thumbnails from several angles (multi_view_thumbnails)."), r.MultiViewThumbnails);
			r.AlphaThumbnail = EditorGUILayout.Toggle(
				new GUIContent("Alpha Thumbnail", "Generate a thumbnail with a transparent background (alpha_thumbnail)."), r.AlphaThumbnail);
		}

		/// <summary>
		/// Persist the draft (minus <see cref="MeshyRequest.ImagePath"/>) under keys prefixed with
		/// <paramref name="prefix"/>. A struct of get/set properties can't go through
		/// <see cref="JsonUtility"/>, so the fields are written individually.
		/// </summary>
		public static void Save(string prefix, MeshyRequest r)
		{
			EditorPrefs.SetString(prefix + "AiModel", r.AiModel);
			EditorPrefs.SetInt(prefix + "Format", (int)r.Format);
			EditorPrefs.SetBool(prefix + "GenerateTexture", r.GenerateTexture);
			EditorPrefs.SetBool(prefix + "EnablePbr", r.EnablePbr);
			EditorPrefs.SetBool(prefix + "HdTexture", r.HdTexture);
			EditorPrefs.SetBool(prefix + "Remesh", r.Remesh);
			EditorPrefs.SetInt(prefix + "Topology", (int)r.Topology);
			EditorPrefs.SetInt(prefix + "Decimation", (int)r.Decimation);
			EditorPrefs.SetInt(prefix + "TargetPolycount", r.TargetPolycount);
			EditorPrefs.SetBool(prefix + "SavePreRemeshedModel", r.SavePreRemeshedModel);
			EditorPrefs.SetBool(prefix + "RemoveLighting", r.RemoveLighting);
			EditorPrefs.SetBool(prefix + "Moderation", r.Moderation);
			EditorPrefs.SetBool(prefix + "AutoSize", r.AutoSize);
			EditorPrefs.SetInt(prefix + "OriginAt", (int)r.OriginAt);
			EditorPrefs.SetBool(prefix + "MultiViewThumbnails", r.MultiViewThumbnails);
			EditorPrefs.SetBool(prefix + "AlphaThumbnail", r.AlphaThumbnail);
		}

		/// <summary>Read the draft back, falling to <see cref="Default"/> for any key not yet saved. ImagePath is left blank.</summary>
		public static MeshyRequest Load(string prefix)
		{
			var d = Default();
			return new MeshyRequest
			{
				AiModel = EditorPrefs.GetString(prefix + "AiModel", d.AiModel),
				Format = (ModelFormat)EditorPrefs.GetInt(prefix + "Format", (int)d.Format),
				GenerateTexture = EditorPrefs.GetBool(prefix + "GenerateTexture", d.GenerateTexture),
				EnablePbr = EditorPrefs.GetBool(prefix + "EnablePbr", d.EnablePbr),
				HdTexture = EditorPrefs.GetBool(prefix + "HdTexture", d.HdTexture),
				Remesh = EditorPrefs.GetBool(prefix + "Remesh", d.Remesh),
				Topology = (MeshyTopology)EditorPrefs.GetInt(prefix + "Topology", (int)d.Topology),
				Decimation = (DecimationMode)EditorPrefs.GetInt(prefix + "Decimation", (int)d.Decimation),
				TargetPolycount = EditorPrefs.GetInt(prefix + "TargetPolycount", d.TargetPolycount),
				SavePreRemeshedModel = EditorPrefs.GetBool(prefix + "SavePreRemeshedModel", d.SavePreRemeshedModel),
				RemoveLighting = EditorPrefs.GetBool(prefix + "RemoveLighting", d.RemoveLighting),
				Moderation = EditorPrefs.GetBool(prefix + "Moderation", d.Moderation),
				AutoSize = EditorPrefs.GetBool(prefix + "AutoSize", d.AutoSize),
				OriginAt = (ModelOrigin)EditorPrefs.GetInt(prefix + "OriginAt", (int)d.OriginAt),
				MultiViewThumbnails = EditorPrefs.GetBool(prefix + "MultiViewThumbnails", d.MultiViewThumbnails),
				AlphaThumbnail = EditorPrefs.GetBool(prefix + "AlphaThumbnail", d.AlphaThumbnail),
			};
		}
	}
}
