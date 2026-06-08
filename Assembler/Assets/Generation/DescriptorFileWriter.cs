using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assembler.Generation
{
	public static class DescriptorFileWriter
	{
		private readonly static string FolderPath = Path.Combine(Application.persistentDataPath, "GeneratedGameDescriptors");

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
			var sanitised = Sanitise(title);

			if (string.IsNullOrEmpty(sanitised))
			{
				sanitised = "game-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
			}

			return sanitised + ".yaml";
		}

		public static string Sanitise(string? title)
		{
			if (string.IsNullOrWhiteSpace(title))
			{
				return string.Empty;
			}

			var invalid = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder(title.Length);
			var prevWasSeparator = false;

			foreach (var c in title)
			{
				if (Array.IndexOf(invalid, c) >= 0 || c == '/' || c == '\\' || c == ':' || char.IsWhiteSpace(c))
				{
					if (!prevWasSeparator && sb.Length > 0)
					{
						sb.Append('-');
						prevWasSeparator = true;
					}

					continue;
				}

				sb.Append(c);
				prevWasSeparator = false;
			}

			var result = sb.ToString().Trim('-', '.', ' ');
			return result;
		}
	}
}
