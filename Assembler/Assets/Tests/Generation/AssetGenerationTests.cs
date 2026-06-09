using System.Linq;
using Assembler.Generation.Verification.Editor;
using NUnit.Framework;

namespace Tests.Generation
{
	public class AssetManifestExtractorTests
	{
		[Test]
		public void Extract_parses_assets_block_into_requests()
		{
			const string reply =
				"Here is your game.\n" +
				"```yaml\nGame:\n  Title: Demo\n```\n" +
				"```assets\n" +
				"[\n" +
				"  { \"type\": \"mesh\", \"id\": \"player\", \"path\": \"Voxels/demo/player\", \"prompt\": \"a small blue ship\" },\n" +
				"  { \"type\": \"mesh\", \"id\": \"asteroid\", \"path\": \"Voxels/demo/asteroid\", \"prompt\": \"a grey rock\" }\n" +
				"]\n" +
				"```\n";

			var requests = AssetManifestExtractor.Extract(reply);

			Assert.AreEqual(2, requests.Count);
			Assert.AreEqual("mesh", requests[0].Type);
			Assert.AreEqual("player", requests[0].Id);
			Assert.AreEqual("Voxels/demo/player", requests[0].ResourcesPath);
			Assert.AreEqual("a small blue ship", requests[0].Prompt);
			Assert.AreEqual("asteroid", requests[1].Id);
		}

		[Test]
		public void Extract_returns_empty_when_block_absent()
		{
			Assert.IsEmpty(AssetManifestExtractor.Extract("```yaml\nGame: {}\n```"));
		}

		[Test]
		public void Extract_returns_empty_on_null_or_blank()
		{
			Assert.IsEmpty(AssetManifestExtractor.Extract(null));
			Assert.IsEmpty(AssetManifestExtractor.Extract("   "));
		}

		[Test]
		public void Extract_returns_empty_on_malformed_json()
		{
			const string reply = "```assets\n{ not valid json ]\n```";
			Assert.IsEmpty(AssetManifestExtractor.Extract(reply));
		}

		[Test]
		public void Extract_skips_entries_missing_required_fields()
		{
			const string reply =
				"```assets\n" +
				"[\n" +
				"  { \"type\": \"mesh\", \"id\": \"ok\", \"path\": \"Voxels/demo/ok\", \"prompt\": \"fine\" },\n" +
				"  { \"type\": \"mesh\", \"id\": \"\", \"path\": \"x\", \"prompt\": \"no id\" },\n" +
				"  { \"type\": \"mesh\", \"id\": \"noprompt\", \"path\": \"x\" }\n" +
				"]\n" +
				"```";

			var requests = AssetManifestExtractor.Extract(reply);

			Assert.AreEqual(1, requests.Count);
			Assert.AreEqual("ok", requests[0].Id);
		}

		[Test]
		public void Extract_defaults_path_to_id_when_path_missing()
		{
			const string reply =
				"```assets\n[ { \"type\": \"mesh\", \"id\": \"player\", \"prompt\": \"a ship\" } ]\n```";

			var requests = AssetManifestExtractor.Extract(reply);

			Assert.AreEqual(1, requests.Count);
			Assert.AreEqual("player", requests[0].ResourcesPath);
		}

		[Test]
		public void Extract_preserves_path_separators_while_sanitising_segments()
		{
			const string reply =
				"```assets\n[ { \"type\": \"mesh\", \"id\": \"p\", \"path\": \"Voxels/My Game/p\", \"prompt\": \"x\" } ]\n```";

			var requests = AssetManifestExtractor.Extract(reply);

			Assert.AreEqual("Voxels/My-Game/p", requests[0].ResourcesPath);
		}
	}

	public class AssetAugmentedPromptTests
	{
		[Test]
		public void Build_includes_user_prompt_slug_and_supported_types()
		{
			var result = AssetAugmentedPrompt.Build("make a shooter", "my-slug", new[] { "mesh" });

			StringAssert.Contains("make a shooter", result);
			StringAssert.Contains("my-slug", result);
			StringAssert.Contains("mesh", result);
			StringAssert.Contains("```assets", result);
			StringAssert.Contains("Source: resources", result);
		}

		[Test]
		public void Build_lists_multiple_supported_types()
		{
			var result = AssetAugmentedPrompt.Build("idea", "slug", new[] { "mesh", "sprite" });

			StringAssert.Contains("mesh, sprite", result);
		}

		[Test]
		public void Build_handles_no_supported_types()
		{
			var result = AssetAugmentedPrompt.Build("idea", "slug", new string[0]);

			StringAssert.Contains("(none)", result);
		}

		[Test]
		public void BuildRevision_includes_instruction_slug_and_protocol()
		{
			var result = AssetAugmentedPrompt.BuildRevision("make the ship red", "my-slug", new[] { "mesh" });

			StringAssert.Contains("make the ship red", result);
			StringAssert.Contains("my-slug", result);
			StringAssert.Contains("Revise", result);
			StringAssert.Contains("```assets", result);
			StringAssert.Contains("mesh", result);
		}
	}

	public class AssetGeneratorRegistryTests
	{
		[Test]
		public void Default_resolves_mesh_to_voxel_generator()
		{
			var registry = AssetGeneratorRegistry.Default;

			var generator = registry.Get("mesh");

			Assert.IsNotNull(generator);
			Assert.AreEqual("mesh", generator!.AssetType);
		}

		[Test]
		public void Get_is_case_insensitive()
		{
			Assert.IsNotNull(AssetGeneratorRegistry.Default.Get("MESH"));
		}

		[Test]
		public void Get_unknown_type_returns_null()
		{
			Assert.IsNull(AssetGeneratorRegistry.Default.Get("sprite"));
			Assert.IsNull(AssetGeneratorRegistry.Default.Get(null));
			Assert.IsNull(AssetGeneratorRegistry.Default.Get(""));
		}

		[Test]
		public void SupportedTypes_contains_mesh()
		{
			Assert.IsTrue(AssetGeneratorRegistry.Default.SupportedTypes.Contains("mesh"));
		}
	}
}
