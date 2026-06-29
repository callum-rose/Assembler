# Remote loading & remote generation (Phase 2)

This folder is the **dev-side tooling** for the remote game pipeline. Game *data* lives in a separate,
free, public GitHub repo (`assembler-games`) served over `raw.githubusercontent.com`; these scripts
generate games, validate them, and publish them there. The app downloads from there at runtime.

```
phone app  ──GET manifest.json──►  assembler-games repo  ◄──push──  publish-game.sh / daemon (this Mac)
   shelf                            manifest.json + games/<id>/descriptor.yaml
   tap ────GET descriptor.yaml────►
```

Everything here is **private dev tooling** — generation is never exposed in the app (v1).

## One-time setup

1. **Install deps:** `brew install gh jq` and `gh auth login`. The `claude` CLI must be on PATH
   (generation is billed to your Claude subscription — no API key needed).
2. **Create the store repo:**
   ```sh
   Tools/remote/setup-store.sh            # creates ~/Developer/assembler-games + the GitHub repo + the "generate" label
   ```
   It prints your **Manifest URL** (e.g. `https://raw.githubusercontent.com/<you>/assembler-games/main/manifest.json`).
3. **Point the app at it:** open the `Bootstrap` scene in Unity, select the boot GameObject, and set
   **GameShelf → Manifest Url** to that URL. (See "Wiring the app" below — the `GameShelf` component
   replaces `GameBootstrap`.)

## Daily use

```sh
# Generate a new game from a brief, validate it, publish it:
Tools/remote/publish-game.sh "a top-down game where you dodge falling rocks"

# Publish/refresh an existing local descriptor:
Tools/remote/publish-game.sh Assets/ExampleGameDescriptors/Pong.yaml

# Refine a published game and bump its version (clients re-download):
Tools/remote/refine-game.sh dodge-falling-rocks "make the rocks faster and add a score"
```

Each publish commits + pushes to the store; the app shows the new/updated game on its next shelf refresh
(the shelf re-fetches the manifest every time you exit a game).

## Generate from your phone (the always-on daemon)

Run the daemon so you can queue games from anywhere by opening a GitHub issue labelled `generate`
(via the GitHub mobile app or an iOS Shortcut). The issue title/body is the brief.

```sh
# Foreground (testing):
ASSEMBLER_STORE_REPO=<you>/assembler-games Tools/remote/generation-daemon.sh

# Background (recommended) — install the LaunchAgent so it runs at login and restarts on crash:
#   1. Edit com.assembler.generation-daemon.plist  (replace REPLACE_ME / REPLACE_OWNER and check PATH)
#   2. cp com.assembler.generation-daemon.plist ~/Library/LaunchAgents/
#   3. launchctl load -w ~/Library/LaunchAgents/com.assembler.generation-daemon.plist
```

The daemon generates → validates → publishes → comments the result on the issue → closes it. Failures
leave the issue open (with the label removed) and a comment explaining why.

## Wiring the app

The `Assembler.Remote` assembly (`Assets/Remote/`) adds the runtime shelf. To switch a build from the
single-game `GameBootstrap` to the remote shelf, in the **Bootstrap** scene replace the boot GameObject's
`GameBootstrap` component with **`GameShelf`** and set its **Manifest Url**. (This is a one-click editor
change; it can't be scripted here because the component's GUID only exists after Unity imports the new
script.) `GameBootstrap` stays in the project as a single-descriptor dev launcher.

## Configuration (env vars)

| Variable | Default | Used by |
|---|---|---|
| `ASSEMBLER_STORE_DIR` | `~/Developer/assembler-games` | all |
| `ASSEMBLER_STORE_REPO` | — (required for daemon) | daemon |
| `ASSEMBLER_ENGINE_DIR` | auto-detected (`…/Assembler`) | publish |
| `ASSEMBLER_STORE_BRANCH` | `main` | publish |
| `ASSEMBLER_POLL_SECONDS` | `30` | daemon |
| `ASSEMBLER_GEN_LABEL` | `generate` | daemon, setup |
| `CLAUDE_CLI_PATH` | `claude` | publish, refine |

## v1 limits & notes

- **Primitive assets only.** Generated games must not declare a top-level `Assets:` block (no custom
  voxel/sprite/audio). The app's `RemoteGameGuard` rejects asset-bearing descriptors so they fail with a
  clean message instead of crashing mid-build. Voxel-asset remote loading is a later phase.
- **Generation prompt may need tuning.** `publish-game.sh` asks the `generate-game-descriptor` skill to
  emit YAML on stdout. If your skill version writes to a file under `Assets/ExampleGameDescriptors/`
  instead, adjust the generation block to copy that file to `$DESC`.
- **`validate-game.sh` baseline:** on a clean tree some example descriptors already fail the sandbox
  validator; treat a hard failure (parse/instantiate error) as the publish gate.
- **CDN freshness:** we serve the manifest from `raw.githubusercontent.com` (always fresh). jsDelivr
  caches `@latest` ~12h, so prefer raw for the fast refine loop; switch to a pinned-SHA jsDelivr URL only
  when you want CDN scale.
- **iOS ATS:** the manifest/descriptor URLs must be `https://` (raw is) — no `Info.plist` exception needed.
