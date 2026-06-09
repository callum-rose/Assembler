using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Assembler.Anthropic
{
	/// <summary>
	/// A single image attached to a user turn. Sent to Claude as a base64
	/// content block. Defaults to PNG — what the voxel pipeline produces when it
	/// renders previews or pulls a reference image.
	/// </summary>
	public sealed record AnthropicImage(byte[] PngBytes, string MediaType = "image/png")
	{
		/// <summary>Base64 of the raw image bytes (no data-URI prefix).</summary>
		public string Base64() => Convert.ToBase64String(PngBytes);

		/// <summary>
		/// Builds the Anthropic image content-block wire shape as a
		/// <see cref="JsonElement"/> dictionary, ready to hand to
		/// <c>ImageBlockParam.FromRawUnchecked</c>:
		/// <code>{"type":"image","source":{"type":"base64","media_type":..,"data":..}}</code>
		/// Kept SDK-free so it can be unit-tested without a network client.
		/// </summary>
		public Dictionary<string, JsonElement> ToWireDictionary()
		{
			var json = JsonSerializer.Serialize(new Dictionary<string, object>
			{
				["type"] = "image",
				["source"] = new Dictionary<string, object>
				{
					["type"] = "base64",
					["media_type"] = MediaType,
					["data"] = Base64(),
				},
			});

			// Deserialising into JsonElement clones each element so it stays valid
			// independent of any backing document — same pattern the tool-schema
			// projection uses in AnthropicClient.BuildTool.
			return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
				   ?? new Dictionary<string, JsonElement>();
		}
	}
}
