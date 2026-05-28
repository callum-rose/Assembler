using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit.MagicaVoxel
{
	public class ShapeElement : HierarchyElement
	{
		public ModelReference[] Models;

		public ShapeElement(int id, Dictionary<string, string> attributes, ModelReference[] models) : base(id, attributes)
		{
			Models = models;
		}

		public override HierarchyNode AddToAsset(VoxelAsset asset, List<Model> models)
		{
			var shape = ScriptableObject.CreateInstance<Shape>();
			shape.name = Attributes.TryGetValue("_name", out string shapeName) ? shapeName : $"Shape {Id}";
			shape.Animation.Looped = Attributes.TryGetValue("_loop", out var isLooped) && isLooped == "1";

			foreach (var modelReference in Models)
			{
				var model = models.Find(x => x.ID == modelReference.Id);
				var attributes = modelReference.Attributes;
				var frameId = attributes.TryGetValue("_f", out var id) ? int.Parse(id) : 0;
				shape.AddAnimationFrame(new ModelAnimationFrame(frameId, model));
			}

			return shape;
		}
	}
}