using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Assembler.Anthropic;
using Assembler.Voxels.Generation;
using Assembler.Voxels.Scripting;
using UnityEngine;

namespace Assembler.Voxels.Pipeline
{
	/// <summary>
	/// Immutable state carrier threaded through pipeline stages. Stages return a
	/// new context via <c>ctx with { ... }</c>; the runner threads it through.
	/// Stages produce fresh instances for output fields (byte[], VoxelModel,
	/// etc.) — never mutate values received via <c>ctx</c>.
	/// </summary>
	public sealed record VoxelPipelineContext
	{
		public AnthropicClient? AnthropicClient { get; init; }
		public IVoxelScriptExecutor? ScriptExecutor { get; init; }
		public VoxelScriptLimits Limits { get; init; } = VoxelScriptLimits.Default;
		public IVoxelFileSink FileSink { get; init; } = new SystemVoxelFileSink();
		public IAssetDatabaseService AssetDb { get; init; } = new NoOpAssetDatabaseService();
		public IVoxelPipelineObserver Observer { get; init; } = NullVoxelPipelineObserver.Instance;
		public IVoxelClock Clock { get; init; } = SystemVoxelClock.Instance;
		public IMainThreadDispatcher MainThread { get; init; } = InlineMainThreadDispatcher.Instance;

		public string? SystemPrompt { get; init; }
		public string? PersistentInstructions { get; init; }
		public string? UserPrompt { get; init; }
		public string? RefinementInstruction { get; init; }
		public ImmutableList<AnthropicMessage> ChatHistory { get; init; } = ImmutableList<AnthropicMessage>.Empty;
		public bool UseChatHistory { get; init; }

		public string? RawAssistantText { get; init; }
		public string? LastScript { get; init; }
		public string? GoxelTextZUp { get; init; }
		public VoxelModel? Model { get; init; }
		public byte[]? VoxBytes { get; init; }

		public string? SavedVoxPath { get; init; }
		public string? SavedProjectPath { get; init; }
		public Mesh? PreviewMesh { get; init; }

		// Reference-image hybrid (Phases 1–4). All transient like PreviewMesh —
		// they describe the current run, not durable model state.
		public IImageGenerator? ImageGenerator { get; init; }
		public int ReferenceVariations { get; init; } = 1;

		/// <summary>PNG variations from the text-to-image provider (Phase 1).</summary>
		public IReadOnlyList<byte[]>? ReferenceImages { get; init; }

		/// <summary>PNG renders of the current meshed model from several angles (Phase 3).</summary>
		public IReadOnlyList<byte[]>? RenderedImages { get; init; }

		/// <summary>Latest deterministic geometry validation result (Phase 4).</summary>
		public GeometryReport? Geometry { get; init; }

		public VoxelProject Project { get; init; } = new();
	}

	public sealed class VoxelPipelineResult
	{
		public VoxelPipelineContext Context { get; }
		public VoxelPipelineResult(VoxelPipelineContext context) => Context = context;

		public string? GoxelTextZUp => Context.GoxelTextZUp;
		public string? LastScript => Context.LastScript;
		public byte[]? VoxBytes => Context.VoxBytes;
		public VoxelModel? Model => Context.Model;
		public string? SavedPath => Context.SavedVoxPath;
		public string? SavedProjectPath => Context.SavedProjectPath;
		public Mesh? PreviewMesh => Context.PreviewMesh;
		public IReadOnlyList<byte[]>? ReferenceImages => Context.ReferenceImages;
		public IReadOnlyList<byte[]>? RenderedImages => Context.RenderedImages;
		public GeometryReport? Geometry => Context.Geometry;
		public IReadOnlyList<AnthropicMessage> ChatHistory => Context.ChatHistory;
		public VoxelProject Project => Context.Project;
	}
}
