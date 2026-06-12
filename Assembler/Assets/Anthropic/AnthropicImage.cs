using System;

namespace Assembler.Anthropic
{
	/// <summary>
	/// An image attached to an <see cref="AnthropicMessage"/>, sent to the API as
	/// a base64 image content block ahead of the message text. Media type is the
	/// MIME type the API accepts (image/png, image/jpeg, image/gif, image/webp).
	/// </summary>
	public sealed record AnthropicImage(string MediaType, byte[] Data)
	{
		public static AnthropicImage None { get; } = new(string.Empty, Array.Empty<byte>());

		public bool IsEmpty => Data.Length == 0;

		/// <summary>Guesses the MIME type from a file extension, defaulting to PNG.</summary>
		public static string MediaTypeFromExtension(string extension) => extension.TrimStart('.').ToLowerInvariant() switch
		{
			"jpg" or "jpeg" => "image/jpeg",
			"gif" => "image/gif",
			"webp" => "image/webp",
			_ => "image/png",
		};
	}
}
