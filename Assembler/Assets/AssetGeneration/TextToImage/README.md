# AI Image Generation (editor module)

An editor window that turns a text prompt into an image file on disk.
Open it from **Assembler → Image Generation**.

## Usage

1. Get a free Gemini API key at <https://aistudio.google.com/apikey>.
2. Paste it into **API Key**, hit **Save**.
3. Type a **Prompt**, set an **Output Image Path**, hit **Generate**.

The generated image is written to the chosen path (a preview shows in the
window), and `AssetDatabase.Refresh()` runs if the path is under `Assets/`.

All inputs — provider, model, prompt, output path, and a per-provider API key —
are persisted in `EditorPrefs` (keys prefixed `Assembler.ImageGen.`).

## Swapping the provider

The window talks to an `IImageGenerator` ([IImageGenerator.cs](Editor/IImageGenerator.cs)),
so adding another backend (OpenAI, Stability, a local model, …) is:

1. Implement `IImageGenerator` (see [GeminiImageGenerator.cs](Editor/GeminiImageGenerator.cs)
   for the shape — one `GenerateAsync` returning bytes + MIME type).
2. Add a value to the `ImageProvider` enum and a case to
   `ImageGeneratorFactory.Create`.

No window changes are needed; it picks the provider from a dropdown.

## Notes

- Gemini's image models (e.g. `gemini-2.5-flash-image`) are on the free tier;
  leave **Model** blank to use the default.
- Minimal: single request, no retries/queuing, key lives in `EditorPrefs`.
