using System;
using System.Collections.Generic;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Deserialisation.Dtos;
using Assembler.Parsing;
using UnityEngine;

namespace Assembler.Generation.Verification
{
	public sealed class BuildResult
	{
		public bool Success { get; }
		public IReadOnlyList<string> Errors { get; }
		public GameDto? ParsedDto { get; }

		public BuildResult(bool success, IReadOnlyList<string> errors, GameDto? parsedDto)
		{
			Success = success;
			Errors = errors;
			ParsedDto = parsedDto;
		}
	}

	public static class BuildHarness
	{
		public static BuildResult TryBuild(string yaml)
		{
			var errors = new List<string>();
			GameDto? dto = null;

			void OnLog(string condition, string stackTrace, LogType type)
			{
				if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
				{
					errors.Add(string.IsNullOrEmpty(stackTrace)
						? condition
						: condition + "\n" + stackTrace);
				}
			}

			Application.logMessageReceivedThreaded += OnLog;
			try
			{
				try
				{
					dto = new GameFileParser().Parse(yaml);
				}
				catch (Exception ex)
				{
					errors.Add("YAML parse failed: " + ex);
					return new BuildResult(false, errors, null);
				}

				try
				{
					var gameInfo = Transformer.Transform(dto);
					Builder.Build(gameInfo);
				}
				catch (Exception ex)
				{
					errors.Add("Build failed: " + ex);
					return new BuildResult(false, errors, dto);
				}
			}
			finally
			{
				Application.logMessageReceivedThreaded -= OnLog;
			}

			return new BuildResult(errors.Count == 0, errors, dto);
		}
	}
}
