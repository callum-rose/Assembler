using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler.Voxelization
{
	public enum IssueCode
	{
		NotAuthored,
		LayersInvalid,
		ScriptFailed,
		MirrorInvalid,
		HierarchyInvalid,
		PaletteBreach,
		SizeExceeded,
		ScaleMismatch,
		FloatingChunk,
		DisconnectedPart,
		PaletteMismatch,
		SilhouetteMismatch,
		Asymmetric,
	}

	/// <summary>
	/// One problem found during assembly or validation. An empty
	/// <see cref="PartId"/> means the issue is model-level; otherwise the
	/// orchestrator re-authors just that part, feeding the message back.
	/// </summary>
	public sealed record ValidationIssue(string PartId, IssueCode Code, string Message)
	{
		public bool IsModelLevel => PartId.Length == 0;

		public override string ToString() =>
			IsModelLevel ? $"[{Code}] {Message}" : $"[{Code}] {PartId}: {Message}";
	}

	public sealed record ValidationReport(IReadOnlyList<ValidationIssue> Issues)
	{
		public static ValidationReport Clean { get; } = new(Array.Empty<ValidationIssue>());

		public bool IsValid => Issues.Count == 0;

		public IEnumerable<string> FailingPartIds => Issues
			.Where(i => !i.IsModelLevel)
			.Select(i => i.PartId)
			.Distinct();

		public ValidationReport Merge(ValidationReport other) =>
			new(Issues.Concat(other.Issues).ToList());
	}
}
