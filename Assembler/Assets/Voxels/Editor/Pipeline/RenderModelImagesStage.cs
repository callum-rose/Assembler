#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Pipeline;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxels.Editor.Pipeline
{
	/// <summary>
	/// Options for the vision-feedback loop: how many critique passes to run and
	/// from which camera directions to render the model each pass.
	/// </summary>
	public sealed class VisionRefinementOptions
	{
		public int Iterations { get; set; } = 2;
		public int ImageSize { get; set; } = 256;

		/// <summary>
		/// Camera directions (the vector points FROM the subject TOWARD the
		/// camera, in the imported model's Y-up space). Defaults to 3/4-front,
		/// side, and top.
		/// </summary>
		public IReadOnlyList<Vector3> Angles { get; set; } = RenderModelImagesStage.DefaultAngles;
	}

	/// <summary>
	/// Renders the freshly-imported model (written to the scratch path by the
	/// preceding WriteScratchPreview + RefreshAssetDatabase stages) from several
	/// angles via <see cref="PreviewRenderUtility"/> and stores the PNGs on
	/// <c>RenderedImages</c>. Loads the imported GameObject (faithful colours/AO)
	/// and falls back to the Mesh sub-asset. All Unity calls run on the main
	/// thread. Best-effort: a render failure logs and yields no image rather than
	/// throwing, so the loop survives a flaky import.
	/// </summary>
	public sealed class RenderModelImagesStage : IVoxelStage
	{
		public static readonly IReadOnlyList<Vector3> DefaultAngles = new[]
		{
			new Vector3(1f, 0.6f, 1f),    // 3/4-front (right-above-front)
			new Vector3(1f, 0.25f, 0f),   // side
			new Vector3(0f, 1f, 0.05f),   // top
		};

		private readonly string _path;
		private readonly VisionRefinementOptions _options;

		public RenderModelImagesStage(string path, VisionRefinementOptions options)
		{
			_path = path;
			_options = options;
		}

		public string Name => "RenderModelImages";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			List<byte[]>? images = null;
			var path = _path;
			var options = _options;
			var observer = ctx.Observer;

			await ctx.MainThread.RunAsync(() =>
			{
				try
				{
					images = RenderAll(path, options, observer);
				}
				catch (Exception ex)
				{
					observer.OnLog("RenderModelImages failed: " + ex.Message);
				}
			}).ConfigureAwait(false);

			return images is { Count: > 0 } ? ctx with { RenderedImages = images } : ctx;
		}

		private static List<byte[]> RenderAll(string path, VisionRefinementOptions options, IVoxelPipelineObserver observer)
		{
			// Force a synchronous import so the asset is available — sidesteps the
			// async-import race the interactive preview otherwise retries through.
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

			var drawables = LoadDrawables(path);
			if (drawables.Count == 0)
			{
				observer.OnLog("RenderModelImages: no mesh found at " + path + " — skipping renders.");
				return new List<byte[]>();
			}

			var bounds = CombinedBounds(drawables);
			var size = Mathf.Max(64, options.ImageSize);

			var pru = new PreviewRenderUtility();
			var results = new List<byte[]>(options.Angles.Count);
			try
			{
				foreach (var dir in options.Angles)
				{
					var png = RenderOne(pru, drawables, bounds, dir, size);
					if (png != null)
					{
						results.Add(png);
					}
				}
			}
			finally
			{
				pru.Cleanup();
			}

			return results;
		}

		private static byte[]? RenderOne(
			PreviewRenderUtility pru, IReadOnlyList<Drawable> drawables, Bounds bounds, Vector3 direction, int size)
		{
			var center = bounds.center;
			var radius = Mathf.Max(0.01f, bounds.extents.magnitude);
			var dir = direction.sqrMagnitude < 1e-6f ? Vector3.one : direction.normalized;
			var camPos = center + dir * radius * 4f;

			var cam = pru.camera;
			cam.transform.position = camPos;
			cam.transform.rotation = Quaternion.LookRotation(center - camPos, Vector3.up);
			cam.orthographic = true;
			cam.orthographicSize = radius * 1.15f;
			cam.nearClipPlane = 0.01f;
			cam.farClipPlane = radius * 12f + 20f;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);

			pru.lights[0].intensity = 1.1f;
			pru.lights[0].transform.rotation = Quaternion.Euler(35f, 40f, 0f);
			if (pru.lights.Length > 1)
			{
				pru.lights[1].intensity = 0.7f;
				pru.lights[1].transform.rotation = Quaternion.Euler(-20f, -130f, 0f);
			}

			pru.BeginStaticPreview(new Rect(0, 0, size, size));
			foreach (var d in drawables)
			{
				for (var sub = 0; sub < d.SubMeshCount; sub++)
				{
					var material = sub < d.Materials.Count ? d.Materials[sub] : null;
					material ??= DefaultMaterial();
					pru.DrawMesh(d.Mesh, d.Matrix, material, sub);
				}
			}

			pru.camera.Render();
			var tex = pru.EndStaticPreview();
			if (tex == null)
			{
				return null;
			}

			var png = tex.EncodeToPNG();
			UnityEngine.Object.DestroyImmediate(tex);
			return png;
		}

		private static List<Drawable> LoadDrawables(string path)
		{
			var drawables = new List<Drawable>();

			var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (go != null)
			{
				foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
				{
					var mesh = filter.sharedMesh;
					if (mesh == null)
					{
						continue;
					}

					var renderer = filter.GetComponent<MeshRenderer>();
					var materials = renderer != null
						? new List<Material>(renderer.sharedMaterials)
						: new List<Material>();
					drawables.Add(new Drawable(mesh, filter.transform.localToWorldMatrix, materials));
				}

				if (drawables.Count > 0)
				{
					return drawables;
				}
			}

			// Fallback: a bare Mesh sub-asset (EssentialOnly imports).
			var loose = AssetDatabase.LoadAssetAtPath<Mesh>(path);
			if (loose != null)
			{
				drawables.Add(new Drawable(loose, Matrix4x4.identity, new List<Material>()));
			}

			return drawables;
		}

		private static Bounds CombinedBounds(IReadOnlyList<Drawable> drawables)
		{
			var bounds = new Bounds(drawables[0].Matrix.MultiplyPoint3x4(drawables[0].Mesh.bounds.center), Vector3.zero);
			foreach (var d in drawables)
			{
				var b = d.Mesh.bounds;
				var corners = new[]
				{
					new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z),
					new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z),
					new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.max.z),
					new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z),
				};
				foreach (var corner in corners)
				{
					bounds.Encapsulate(d.Matrix.MultiplyPoint3x4(corner));
				}
			}

			return bounds;
		}

		private static Material? s_defaultMaterial;

		private static Material DefaultMaterial()
		{
			if (s_defaultMaterial != null)
			{
				return s_defaultMaterial;
			}

			var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
			s_defaultMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			return s_defaultMaterial;
		}

		private readonly struct Drawable
		{
			public Drawable(Mesh mesh, Matrix4x4 matrix, List<Material> materials)
			{
				Mesh = mesh;
				Matrix = matrix;
				Materials = materials;
			}

			public Mesh Mesh { get; }
			public Matrix4x4 Matrix { get; }
			public List<Material> Materials { get; }
			public int SubMeshCount => Mathf.Max(1, Mesh.subMeshCount);
		}
	}
}
