namespace Assembler.Voxels.Generation
{
	/// <summary>
	/// Augments a bare subject prompt with the art-direction that reconstructs
	/// best into a voxel model: an isometric / 3-4 view, a single centred subject,
	/// a clean readable silhouette, flat solid colours, and a neutral background.
	/// These cues make Claude read depth and proportion more reliably from the
	/// reference image.
	/// </summary>
	public static class ReferenceImageStyle
	{
		public static string Wrap(string subject)
		{
			var trimmed = (subject ?? string.Empty).Trim();
			return "Isometric 3/4-view voxel art of " + trimmed +
				   ". Single centred subject, clean readable silhouette, solid flat colours, " +
				   "even lighting, plain neutral background, no text, no shadows on the ground.";
		}
	}
}
