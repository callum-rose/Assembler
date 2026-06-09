using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>A resolved spawnable template and its selection weight (used in random mode).</summary>
	public sealed record SpawnTemplate(string TemplateId, IValueProvider<float> Weight);

	public sealed class SpawnerData : BehaviourData
	{
		public IValueProvider<string> TemplateId { get; }
		public IReadOnlyList<SpawnTemplate> Templates { get; }
		public IValueProvider<string> Selection { get; }
		public IValueProvider<Vector3> Position { get; }
		public IValueProvider<Vector3> Rotation { get; }
		public IReadOnlyDictionary<string, IValueProvider> Parameters { get; }

		/// <summary>TemplateId is optional; guard before reading it (NullValueProvider.Get throws).</summary>
		public bool HasTemplateId => TemplateId is not NullValueProvider<string>;

		public SpawnerData(
			string id,
			IValueProvider<string> templateId,
			IReadOnlyList<SpawnTemplate> templates,
			IValueProvider<string> selection,
			IValueProvider<Vector3> position,
			IValueProvider<Vector3> rotation,
			IReadOnlyDictionary<string, IValueProvider> parameters) : base(id) =>
			(TemplateId, Templates, Selection, Position, Rotation, Parameters) =
				(templateId, templates, selection, position, rotation, parameters);
	}
}
