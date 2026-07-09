#nullable enable

using Assembler.AssetGeneration.ImageToMesh;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// The Meshy image-to-3D generation controls (model, format, texturing, geometry/remesh, output
	/// toggles) shared by the standalone Image → Mesh window and stage 2 of the Text → Voxels
	/// pipeline. Edits an immutable <see cref="MeshyRequest"/>; the reference image path and the API
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

		/// <summary>
		/// Draw the settings and return an updated copy. <see cref="MeshyRequest"/> is immutable, so the
		/// widgets edit locals and a fresh request is built; <see cref="MeshyRequest.ImagePath"/> is carried
		/// through unchanged (the owning window injects it via <see cref="WithImagePath"/>).
		/// </summary>
		public static MeshyRequest Draw(MeshyRequest r)
		{
			var aiModel = ModelPopup.Draw("Meshy Model", r.AiModel, Models, "Meshy generation model.");
			var format = (ModelFormat)EditorGUILayout.EnumPopup(
				new GUIContent("Output Format", "Model format to generate and download (sent as target_formats)."), r.Format);

			EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
			var generateTexture = EditorGUILayout.Toggle(
				new GUIContent("Generate Texture", "Generate a texture for the model."), r.GenerateTexture);
			var enablePbr = r.EnablePbr;
			var hdTexture = r.HdTexture;
			using (new EditorGUI.DisabledScope(!generateTexture))
			{
				enablePbr = EditorGUILayout.Toggle(
					new GUIContent("Enable PBR Maps", "Also generate metallic/roughness/normal maps."), enablePbr);
				hdTexture = EditorGUILayout.Toggle(
					new GUIContent("HD Texture", "Generate a higher-resolution texture (hd_texture)."), hdTexture);
			}

			EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
			var remesh = EditorGUILayout.Toggle(
				new GUIContent("Remesh", "Let Meshy clean up the topology (should_remesh)."), r.Remesh);
			var topology = r.Topology;
			var decimation = r.Decimation;
			var targetPolycount = r.TargetPolycount;
			var savePreRemeshedModel = r.SavePreRemeshedModel;
			using (new EditorGUI.DisabledScope(!remesh))
			{
				topology = (MeshyTopology)EditorGUILayout.EnumPopup(
					new GUIContent("Topology", "Target face topology when remeshing (topology)."), topology);
				decimation = (DecimationMode)EditorGUILayout.EnumPopup(
					new GUIContent("Decimation", "Remesh decimation preset (decimation_mode). 'None' lets Meshy decide, or set a target polycount instead."),
					decimation);
				// target_polycount is the alternative to a decimation preset, so only offer it when no preset is chosen.
				using (new EditorGUI.DisabledScope(decimation != DecimationMode.None))
				{
					targetPolycount = EditorGUILayout.IntSlider(
						new GUIContent("Target Polycount", "Target triangle count when remeshing (target_polycount, 100–300000)."),
						targetPolycount, 100, 300000);
				}
				savePreRemeshedModel = EditorGUILayout.Toggle(
					new GUIContent("Save Pre-Remeshed Model", "Also keep the model before remeshing (save_pre_remeshed_model)."),
					savePreRemeshedModel);
			}

			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			// remove_lighting is only supported on meshy-6; grey it out (and force false) for other models.
			var supportsRemoveLighting = aiModel == "meshy-6";
			var removeLighting = supportsRemoveLighting && r.RemoveLighting;
			using (new EditorGUI.DisabledScope(!supportsRemoveLighting))
			{
				removeLighting = EditorGUILayout.Toggle(
					new GUIContent("Remove Lighting", "Bake out baked-in lighting from the source image (remove_lighting). Only available on meshy-6."),
					removeLighting);
			}
			var autoSize = EditorGUILayout.Toggle(
				new GUIContent("Auto Size", "Auto-scale the model to a realistic size (auto_size)."), r.AutoSize);
			var originAt = (ModelOrigin)EditorGUILayout.EnumPopup(
				new GUIContent("Origin At", "Where the model's pivot sits (origin_at)."), r.OriginAt);
			var moderation = EditorGUILayout.Toggle(
				new GUIContent("Moderation", "Run content moderation on the input (moderation)."), r.Moderation);
			var multiViewThumbnails = EditorGUILayout.Toggle(
				new GUIContent("Multi-View Thumbnails", "Generate thumbnails from several angles (multi_view_thumbnails)."), r.MultiViewThumbnails);
			var alphaThumbnail = EditorGUILayout.Toggle(
				new GUIContent("Alpha Thumbnail", "Generate a thumbnail with a transparent background (alpha_thumbnail)."), r.AlphaThumbnail);

			return new MeshyRequest
			{
				ImagePath = r.ImagePath,
				AiModel = aiModel,
				Format = format,
				GenerateTexture = generateTexture,
				EnablePbr = enablePbr,
				HdTexture = hdTexture,
				Remesh = remesh,
				Topology = topology,
				Decimation = decimation,
				TargetPolycount = targetPolycount,
				SavePreRemeshedModel = savePreRemeshedModel,
				RemoveLighting = removeLighting,
				Moderation = moderation,
				AutoSize = autoSize,
				OriginAt = originAt,
				MultiViewThumbnails = multiViewThumbnails,
				AlphaThumbnail = alphaThumbnail,
			};
		}

		/// <summary>A copy of <paramref name="r"/> with <see cref="MeshyRequest.ImagePath"/> set — the request a run consumes.</summary>
		public static MeshyRequest WithImagePath(MeshyRequest r, string imagePath) => new()
		{
			ImagePath = imagePath,
			AiModel = r.AiModel,
			Format = r.Format,
			GenerateTexture = r.GenerateTexture,
			EnablePbr = r.EnablePbr,
			HdTexture = r.HdTexture,
			Remesh = r.Remesh,
			Topology = r.Topology,
			Decimation = r.Decimation,
			TargetPolycount = r.TargetPolycount,
			SavePreRemeshedModel = r.SavePreRemeshedModel,
			RemoveLighting = r.RemoveLighting,
			Moderation = r.Moderation,
			AutoSize = r.AutoSize,
			OriginAt = r.OriginAt,
			MultiViewThumbnails = r.MultiViewThumbnails,
			AlphaThumbnail = r.AlphaThumbnail,
		};

		/// <summary>
		/// Persist the draft (minus <see cref="MeshyRequest.ImagePath"/>) under keys prefixed with
		/// <paramref name="prefix"/>. A struct of properties can't go through <see cref="JsonUtility"/>
		/// (it serializes fields, not properties), so the values are written individually.
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
