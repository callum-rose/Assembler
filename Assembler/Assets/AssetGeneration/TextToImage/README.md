# AI Image Generation (editor module)

An editor window that turns a text prompt into an image file on disk.
Open it from **Assembler → Image Generation**.

## Usage

1. Get a free Gemini API key at <https://aistudio.google.com/apikey>.
2. Paste it into **API Key**, hit **Save**.
3. Type a **Prompt**, set an **Output Image Path**, hit **Generate**.

Optionally set a **Reference Image** to condition generation on an existing
image (style reference or image edit) — leave it blank for pure text-to-image.
It's part of the `IImageGenerator` contract (`ImageGenerationRequest.ReferenceImage`),
so every provider receives it; a backend with no image-input support may ignore it.

The generated image is written to the chosen path (a preview shows in the
window), and `AssetDatabase.Refresh()` runs if the path is under `Assets/`.

All inputs — provider, model, prompt, output path, and a per-provider API key —
are persisted in `EditorPrefs` (keys prefixed `Assembler.ImageGen.`).

## Providers

Pick one from the **Provider** dropdown; each has its own API key (stored per
provider) and model list. All honour the optional **Reference Image**.

| Provider | Backend | Notes |
|---|---|---|
| **Google Gemini** | [GeminiImageGenerator.cs](Editor/GeminiImageGenerator.cs) | `gemini-2.5-flash-image` is free-tier; `gemini-3-pro-image` (Nano Banana Pro) is paid and best at prompt adherence. |
| **OpenAI** | [OpenAiImageGenerator.cs](Editor/OpenAiImageGenerator.cs) | `gpt-image-2` family; strongest literal instruction-following. A reference image routes to the multipart `/images/edits` endpoint. Paid. |
| **Black Forest Labs** | [FluxImageGenerator.cs](Editor/FluxImageGenerator.cs) | FLUX.2 / Kontext. Async: submit → poll `polling_url` → download `result.sample`. Reference passed as `input_image`. Paid. |
| **Recraft** | [RecraftImageGenerator.cs](Editor/RecraftImageGenerator.cs) | Returns a URL that's downloaded. Look is driven by a `style` (hard-coded `digital_illustration` default — see the file). Reference uses image-to-image. Paid. |

## Swapping/adding a provider

The window talks to an `IImageGenerator` ([IImageGenerator.cs](Editor/IImageGenerator.cs)),
so adding another backend (Stability, a local model, …) is:

1. Implement `IImageGenerator` (one `GenerateAsync` returning bytes + MIME type;
   honour `ImageGenerationRequest.ReferenceImage` if the backend supports image input).
2. Add a value to the `ImageProvider` enum, a case to `ImageGeneratorFactory.Create`,
   and a model list to `AvailableModelsFor`.

No window changes are needed; it picks the provider from a dropdown.

## Notes

- Leave **Model** blank to use a provider's default (the first in its list).
- Minimal: single request (BFL polls its async job), no retries/queuing, key lives in `EditorPrefs`.
- The non-Gemini backends are implemented from each provider's REST docs but
  unverified against a live key — see the per-file caveats (e.g. Recraft's `recraftv4`
  id and image-to-image `strength`, FLUX's `input_image` reference field).
