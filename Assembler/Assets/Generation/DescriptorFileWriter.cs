using System;
using System.IO;
using Assembler.Extensions;
using UnityEngine;

namespace Assembler.Generation
{
	public static class DescriptorFileWriter
	{
		/// <summary>Folder generated descriptors are written to (under persistent data).</summary>
		public static string FolderPath { get; } = Path.Combine(Application.persistentDataPath, "GeneratedGameDescriptors");

		public static string Write(string yaml, string? title)
		{
			Directory.CreateDirectory(FolderPath);
			var fileName = BuildFileName(title);
			var fullPath = Path.Combine(FolderPath, fileName);
			File.WriteAllText(fullPath, yaml);
			return fullPath;
		}

		public static void WriteTo(string yaml, string fullPath)
		{
			var dir = Path.GetDirectoryName(fullPath);

			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}

			File.WriteAllText(fullPath, yaml);
		}

		public static string BuildFileName(string? title)
		{
			var sanitised = FileNameSanitiser.Sanitise(title);

			if (string.IsNullOrEmpty(sanitised))
			{
				sanitised = "game-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
			}

			return sanitised + ".yaml";
		}
	}
}
