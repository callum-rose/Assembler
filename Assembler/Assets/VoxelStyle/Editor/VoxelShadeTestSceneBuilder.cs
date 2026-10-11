using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assembler.VoxelStyle.Editor
{
	/// <summary>
	/// Builds the voxel-shade test scene from the FBXs in <c>Assets/VoxelStyle/Models</c>: sets their
	/// import settings, remaps each embedded palette material (named by hex) to a shared
	/// <c>Assembler/Voxel Shade</c> material, and lays the models out with a <see cref="VoxelShadeSettings"/>.
	/// Safe to re-run; it rebuilds the scene and reuses existing materials.
	/// </summary>
	public static class VoxelShadeTestSceneBuilder
	{
		const string Root = "Assets/VoxelStyle";
		const string ModelsDir = Root + "/Models";
		const string MaterialsDir = Root + "/Materials";
		const string ScenePath = Root + "/Scenes/VoxelShadeTest.unity";
		const string ShaderName = "Assembler/Voxel Shade";

		[MenuItem("Assembler/Voxel Style/Build Shade Test Scene")]
		public static void Build()
		{
			var shader = Shader.Find(ShaderName);
			if (shader == null)
			{
				throw new FileNotFoundException($"Shader '{ShaderName}' not found");
			}

			var models = AssetDatabase.FindAssets("t:Model", new[] { ModelsDir })
				.Select(AssetDatabase.GUIDToAssetPath)
				.OrderBy(p => p)
				.ToList();
			models.ForEach(path => ConfigureModel(path, shader));

			var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
			var camera = Object.FindAnyObjectByType<Camera>();
			camera.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -4.6f), Quaternion.Euler(8f, 0f, 0f));
			camera.backgroundColor = new Color(0.86f, 0.87f, 0.89f);
			camera.clearFlags = CameraClearFlags.SolidColor;

			var light = Object.FindAnyObjectByType<Light>();
			light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
			light.shadows = LightShadows.Soft;

			RenderSettings.ambientMode = AmbientMode.Trilight;
			RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.88f);
			RenderSettings.ambientEquatorColor = new Color(0.62f, 0.63f, 0.66f);
			RenderSettings.ambientGroundColor = new Color(0.42f, 0.41f, 0.40f);

			var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.localScale = new Vector3(3f, 1f, 3f);
			ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

			var settings = new GameObject("Voxel Shade Settings").AddComponent<VoxelShadeSettings>();
			settings.Apply();

			var spacing = 2.6f;
			var start = -spacing * (models.Count - 1) / 2f;
			for (var i = 0; i < models.Count; i++)
			{
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(models[i]);
				var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
				var bounds = WorldBounds(instance);
				// Sit each model on the ground (a floater like the shark hovers a little above it).
				var lift = Path.GetFileNameWithoutExtension(models[i]) == "Shark" ? 0.8f : 0f;
				instance.transform.position = new Vector3(start + spacing * i, lift - bounds.min.y, 0f);
				instance.transform.rotation = Quaternion.Euler(0f, 240f, 0f);   // three-quarter, head toward the camera's left
			}

			Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
			EditorSceneManager.SaveScene(scene, ScenePath);
			Debug.Log($"[VoxelShade] Built {ScenePath} with {models.Count} model(s)");
		}

		static void ConfigureModel(string path, Shader shader)
		{
			var importer = (ModelImporter)AssetImporter.GetAtPath(path);
			importer.importAnimation = false;
			importer.importCameras = false;
			importer.importLights = false;
			importer.importBlendShapes = false;
			importer.importVisibility = false;
			importer.generateSecondaryUV = false;     // would overwrite UV1, which carries edge distances
			importer.optimizeMeshVertices = false;
			importer.meshCompression = ModelImporterMeshCompression.Off;
			importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
			importer.SaveAndReimport();

			var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).Distinct().ToList();
			foreach (var name in embedded)
			{
				importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), PaletteMaterial(name, shader));
			}
			importer.SaveAndReimport();
		}

		static Material PaletteMaterial(string hexName, Shader shader)
		{
			var hex = hexName.TrimStart('#');
			var path = $"{MaterialsDir}/Palette_{hex}.mat";
			var material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				Directory.CreateDirectory(MaterialsDir);
				material = new Material(shader);
				AssetDatabase.CreateAsset(material, path);
			}
			material.shader = shader;
			if (ColorUtility.TryParseHtmlString("#" + hex, out var colour))
			{
				material.SetColor("_BaseColor", colour);
			}
			EditorUtility.SetDirty(material);
			return material;
		}

		static Material GroundMaterial()
		{
			var path = $"{MaterialsDir}/Ground.mat";
			var material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material != null)
			{
				return material;
			}
			Directory.CreateDirectory(MaterialsDir);
			material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			material.SetColor("_BaseColor", new Color(0.86f, 0.87f, 0.89f));
			material.SetFloat("_Smoothness", 0f);
			AssetDatabase.CreateAsset(material, path);
			return material;
		}

		static Bounds WorldBounds(GameObject root)
		{
			var renderers = root.GetComponentsInChildren<Renderer>();
			return renderers.Skip(1).Aggregate(renderers[0].bounds, (b, r) =>
			{
				b.Encapsulate(r.bounds);
				return b;
			});
		}
	}
}
