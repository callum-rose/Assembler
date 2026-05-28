using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using Assembler.Anthropic;

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
		public IVoxelFileSink FileSink { get; init; } = new SystemVoxelFileSink();
		public IAssetDatabaseService AssetDb { get; init; } = new NoOpAssetDatabaseService();
		public IVoxelPipelineObserver Observer { get; init; } = NullVoxelPipelineObserver.Instance;
		public IVoxelClock Clock { get; init; } = SystemVoxelClock.Instance;

		public string? SystemPrompt { get; init; }
		public string? PersistentInstructions { get; init; }
		public string? UserPrompt { get; init; }
		public string? RefinementInstruction { get; init; }
		public ImmutableList<AnthropicMessage> ChatHistory { get; init; } = ImmutableList<AnthropicMessage>.Empty;
		public bool UseChatHistory { get; init; }

		public string? RawAssistantText { get; init; }
		public string? GoxelTextZUp { get; init; }
		public VoxelModel? Model { get; init; }
		public byte[]? VoxBytes { get; init; }

		public string? SavedVoxPath { get; init; }
		public string? SavedProjectPath { get; init; }
		public Mesh? PreviewMesh { get; init; }

		public VoxelProject Project { get; init; } = new();
	}

	public sealed class VoxelPipelineResult
	{
		public VoxelPipelineContext Context { get; }
		public VoxelPipelineResult(VoxelPipelineContext context) => Context = context;

		public string? GoxelTextZUp => Context.GoxelTextZUp;
		public byte[]? VoxBytes => Context.VoxBytes;
		public VoxelModel? Model => Context.Model;
		public string? SavedPath => Context.SavedVoxPath;
		public string? SavedProjectPath => Context.SavedProjectPath;
		public Mesh? PreviewMesh => Context.PreviewMesh;
		public IReadOnlyList<AnthropicMessage> ChatHistory => Context.ChatHistory;
		public VoxelProject Project => Context.Project;
	}
}
