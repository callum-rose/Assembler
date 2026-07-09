using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.TextToImage;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.PaletteExtraction.Editor
{
    /// <summary>
    /// One-off dev tool: generate the palette-extraction tuning corpus by driving
    /// <see cref="ImageGenerationCore"/> headlessly for a fixed list of crossy-road-style prompts.
    /// Reuses the provider/model/API-key the <c>Image Generation</c> window saved to
    /// <see cref="EditorPrefs"/>, so nothing needs to be pasted. Run from the menu, or in batch mode:
    /// <c>-executeMethod Assembler.AssetGeneration.PaletteExtraction.Editor.CorpusGeneratorBatch.Run</c>.
    /// </summary>
    public static class CorpusGeneratorBatch
    {
        // Mirrors ImageGenerationWindow's EditorPrefs keys so we pick up the same saved config.
        private const string ProviderPref = "Assembler.ImageGen.Provider";
        private const string ModelPref = "Assembler.ImageGen.Model";

        private const string OutputDir = "Assets/AssetGeneration/PaletteExtraction/TuningCorpus";

        // (slug, prompt) — the deliberate stressors are documented in the module README.
        private static readonly (string Slug, string Prompt)[] Corpus =
        {
            ("turtle", "A very simple turtle with a light green head and legs and a dark green shell and small black eyes. A very boxy crossy road style. No shadows. A plain light blue background."),
            ("fox", "A very simple fox with an orange body, a white chest and tail tip, and black paws and a black nose. A very boxy crossy road style. No shadows. A plain pale green background."),
            ("snowman", "A very simple white snowman with an orange carrot nose, black coal eyes and buttons, and brown twig arms. A very boxy crossy road style. No shadows. A plain sky blue background."),
            ("crate", "A very simple plain brown cardboard box. A very boxy crossy road style. No shadows. A plain light grey background."),
            ("tree", "A very simple tree with green leaves and a brown trunk. A very boxy crossy road style. No shadows. A plain pale blue background."),
            ("ladybug", "A very simple red ladybug with black spots and a black head. A very boxy crossy road style. No shadows. A plain green background."),
            ("penguin", "A very simple penguin with a black back, a white belly, and an orange beak and feet. A very boxy crossy road style. No shadows. A plain pale blue background."),
            ("chicken", "A very simple white chicken with a red comb, a yellow beak, and small black eyes. A very boxy crossy road style. No shadows. A plain light brown background."),
            ("panda", "A very simple panda with a white body and black ears, patches, arms and legs. A very boxy crossy road style. No shadows. A plain green background."),
            ("robot", "A very simple grey robot with a boxy metal body, a glowing blue eye, and a dark grey visor. A very boxy crossy road style. No shadows. A plain dark navy background."),
            ("cat", "A very simple grey cat with darker grey stripes, a pink nose, and green eyes. A very boxy crossy road style. No shadows. A plain cream background."),
            ("traffic-cone", "A very simple orange traffic cone with a white stripe. A very boxy crossy road style. No shadows. A plain light grey background."),
            ("parrot", "A very simple parrot with a red head, a yellow chest, blue wings, green tail feathers, and a black beak. A very boxy crossy road style. No shadows. A plain white background."),
            ("blue-whale", "A very simple blue whale with a light blue belly and small black eyes. A very boxy crossy road style. No shadows. A plain blue background."),
            ("bee", "A very simple bee with yellow and black stripes and white wings. A very boxy crossy road style. No shadows. A plain light blue background."),
            ("gem", "A very simple purple crystal gem. A very boxy crossy road style. No shadows. A plain dark grey background."),
        };

        [MenuItem("Assembler/Palette Extraction/Generate Tuning Corpus")]
        public static void Run()
        {
            // Unity API (EditorPrefs) must be read on the main thread before we go async.
            var provider = (ImageProvider)EditorPrefs.GetInt(ProviderPref, (int)ImageProvider.GoogleGemini);
            // CORPUS_MODEL env var overrides the saved model, so a batch run can pin a specific model
            // (e.g. gemini-3-pro-image) without touching the window's EditorPrefs.
            var modelOverride = Environment.GetEnvironmentVariable("CORPUS_MODEL");
            var model = string.IsNullOrWhiteSpace(modelOverride)
                ? EditorPrefs.GetString(ModelPref + "." + provider, ImageGeneratorFactory.DefaultModelFor(provider))
                : modelOverride;
            var apiKey = EditorPrefs.GetString("Assembler.ImageGen.ApiKey." + provider, "");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Fail($"No saved API key for provider '{provider}'. Generate once via " +
                     "Assembler > Image Generation to store a key, or switch the saved provider.");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            Debug.Log($"[Corpus] Generating {Corpus.Length} images with {provider} / {model} → {OutputDir}");

            int done = 0;
            try
            {
                // Run the whole async loop on a thread-pool thread (null SynchronizationContext) so the
                // awaits inside GenerateAsync never try to resume on the blocked main thread. Nothing in
                // GenerateAsync touches the Unity API, so this is safe off the main thread.
                Task.Run(async () =>
                {
                    foreach (var (slug, prompt) in Corpus)
                    {
                        var result = await ImageGenerationCore.GenerateAsync(
                            provider, apiKey, model, prompt, OutputDir, slug, CancellationToken.None);
                        Debug.Log($"[Corpus] {++done}/{Corpus.Length} {slug} → {result.OutputPath}");
                    }
                }).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                AssetDatabase.Refresh();
                Fail($"Corpus generation failed after {done}/{Corpus.Length}: {e.Message}");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Corpus] Done — {done}/{Corpus.Length} images in {OutputDir}");
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Corpus] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
