using System;
using System.Collections.Generic;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Deserialisation.Dtos;
using Assembler.Parsing;
using Assembler.Parsing.Validation;
using UnityEngine;

namespace Assembler.Generation.Verification
{
	public sealed record BuildResult(bool Success, IReadOnlyList<string> Errors, GameDto? ParsedDto);

	public static class BuildHarness
	{
		public static BuildResult TryBuild(string yaml)
		{
			var errors = new List<string>();
			GameDto? dto;

			void OnLog(string condition, string stackTrace, LogType type)
			{
				if (type is LogType.Error or LogType.Exception or LogType.Assert)
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

					var validation = GameInfoValidator.Validate(gameInfo);
					if (!validation.IsValid)
					{
						foreach (var error in validation.Errors)
						{
							errors.Add(error.ToString());
						}

						return new BuildResult(false, errors, dto);
					}

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
