# Remote loading & remote generation (Phase 2)

This folder is the **dev-side tooling** for the remote game pipeline, as a standalone .NET console app
(`assembler-remote`) that lives **outside** the Unity project. Game *data* lives in a separate, free,
public GitHub repo (`assembler-games`) served over `raw.githubusercontent.com`; this tool generates
games, validates them, and publishes them there. The app downloads from there at runtime.

```
phone app  ──GET manifest.json──►  assembler-games repo  ◄──push──  assembler-remote (this Mac)
   shelf                            manifest.json + games/<id>/descriptor.yaml
   tap ────GET descriptor.yaml────►
```

Everything here is **private dev tooling** — generation is never exposed in the app (v1). It replaces
the old `Tools/remote/*.sh` bash scripts (`setup-store.sh` → `setup`, `publish-game.sh` → `publish`,
`refine-game.sh` → `refine`, `generation-daemon.sh` → `daemon`); behaviour and env-var surface are unchanged.

## Build

From the **repo root**, build the console app (pointing at the `.csproj` so it works regardless of your
current directory):

```sh
dotnet build -c Release RemoteTooling/Assembler.RemoteTooling.csproj
```

This produces a native launcher at `RemoteTooling/bin/Release/net10.0/assembler-remote`. The examples
below call `assembler-remote`; either put that on your PATH, use the full path, or run via
`dotnet run --project RemoteTooling -- <command>` from the repo root.

## One-time setup

1. **Install deps:** `brew install gh` and `gh auth login`, plus the .NET SDK (`brew install dotnet`).
   The `claude` CLI must be on PATH (generation is billed to your Claude subscription — no API key needed).
2. **Create the store repo** — run `setup` with no arguments:
   ```sh
   assembler-remote setup
   ```
   This creates `~/Developer/assembler-games`, the public GitHub repo behind it, and the `generate`
   label, then prints your **Manifest URL** (e.g.
   `https://raw.githubusercontent.com/<you>/assembler-games/main/manifest.json`). Pass a name —
   `assembler-remote setup my-store` — to use a different repo. (Don't paste a trailing `# comment`
   after the command: an interactive zsh prompt passes the `#` as an argument.)
3. **Point the app at it:** open the `Bootstrap` scene in Unity, select the boot GameObject, and set
   **GameShelf → Manifest Url** to that URL. (See "Wiring the app" below — the `GameShelf` component
   replaces `GameBootstrap`.)

## Daily use

Generate a new game from a brief, validate it, and publish it:

```sh
assembler-remote publish "a top-down game where you dodge falling rocks"
```

Publish/refresh an existing local descriptor:

```sh
assembler-remote publish Assembler/Assets/ExampleGameDescriptors/Pong.yaml
```

Refine a published game and bump its version (clients re-download):

```sh
assembler-remote refine dodge-falling-rocks "make the rocks faster and add a score"
```

Each publish commits + pushes to the store; the app shows the new/updated game on its next shelf refresh
(the shelf re-fetches the manifest every time you exit a game).

## Generate from your phone (the always-on daemon)

Run the daemon so you can queue games from anywhere by opening a GitHub issue labelled `generate`
(via the GitHub mobile app or an iOS Shortcut). The issue title/body is the brief.

Foreground (testing):

```sh
ASSEMBLER_STORE_REPO=<you>/assembler-games assembler-remote daemon
```

Background (recommended) — install the LaunchAgent so it runs at login and restarts on crash.

**One command (Rider or shell).** `deploy-daemon.sh` does the whole install: builds Release, writes the
plist to `~/Library/LaunchAgents` with your real paths filled in (resolved from the script's own location —
no `REPLACE_ME` editing), and reloads launchd. Re-run it any time to redeploy after a code change.

- **From Rider:** run the committed **"Deploy Generation Daemon"** run configuration (top-right run-config
  dropdown; it's in `.run/` at the repo root, which is the folder you open as the `RemoteTooling` project).
- **From a shell:** `RemoteTooling/deploy-daemon.sh` (from anywhere).

It auto-detects the store repo as `<your gh login>/assembler-games`; override any of
`ASSEMBLER_STORE_REPO` / `ASSEMBLER_STORE_DIR` / `ASSEMBLER_ENGINE_DIR` / `ASSEMBLER_POLL_SECONDS` via the
environment (or the run configuration's *Environment variables* field) if your setup differs.

**Manual (equivalent steps)** if you'd rather not use the script:

1. Build once (see [Build](#build)) so the launcher exists.
2. `cp com.assembler.generation-daemon.plist ~/Library/LaunchAgents/`
3. Edit the **copy** — `~/Library/LaunchAgents/com.assembler.generation-daemon.plist` — replacing
   `REPLACE_ME` / `REPLACE_OWNER` and checking `PATH`. (Edit the copy, not the tracked template, so your
   home path and username never get committed.)
4. `launchctl load -w ~/Library/LaunchAgents/com.assembler.generation-daemon.plist`

The daemon generates → validates → publishes → comments the result on the issue → closes it. Failures
leave the issue open (with the label removed) and a comment explaining why. It holds a single-flight lock
so a second daemon exits immediately, and releases the lock on SIGTERM so `launchctl unload` / a KeepAlive
restart isn't locked out.

## Wiring the app

The `Assembler.Remote` assembly (`Assembler/Assets/Remote/`) adds the runtime shelf. To switch a build
from the single-game `GameBootstrap` to the remote shelf, in the **Bootstrap** scene replace the boot
GameObject's `GameBootstrap` component with **`GameShelf`** and set its **Manifest Url**. (This is a
one-click editor change; it can't be scripted here because the component's GUID only exists after Unity
imports the new script.) `GameBootstrap` stays in the project as a single-descriptor dev launcher.

## Configuration (env vars)

| Variable | Default | Used by |
|---|---|---|
| `ASSEMBLER_STORE_DIR` | `~/Developer/assembler-games` (setup: `~/Developer/<repo-name>`) | all |
| `ASSEMBLER_STORE_REPO` | — (required for daemon) | daemon |
| `ASSEMBLER_ENGINE_DIR` | auto-detected (the `Assembler/` Unity project) | publish |
| `ASSEMBLER_STORE_BRANCH` | `main` | publish |
| `ASSEMBLER_STORE_REMOTE` | `origin` | publish |
| `ASSEMBLER_POLL_SECONDS` | `30` | daemon |
| `ASSEMBLER_GEN_LABEL` | `generate` | daemon, setup |
| `CLAUDE_CLI_PATH` | `claude` | publish, refine |

## v1 limits & notes

- **Primitive assets only.** Generated games must not declare a top-level `Assets:` block (no custom
  voxel/sprite/audio). The app's `RemoteGameGuard` rejects asset-bearing descriptors so they fail with a
  clean message instead of crashing mid-build. Voxel-asset remote loading is a later phase.
- **Generation prompt may need tuning.** `publish` asks the `generate-game-descriptor` skill to emit YAML
  on stdout. If your skill version writes to a file under `Assets/ExampleGameDescriptors/` instead, adjust
  the prompt in `GameGenerator.cs` to copy that file to the descriptor path.
- **`validate-game.sh` baseline:** on a clean tree some example descriptors already fail the sandbox
  validator; treat a hard failure (parse/instantiate error) as the publish gate.
- **CDN freshness:** we serve the manifest from `raw.githubusercontent.com` (always fresh). jsDelivr
  caches `@latest` ~12h, so prefer raw for the fast refine loop; switch to a pinned-SHA jsDelivr URL only
  when you want CDN scale.
- **iOS ATS:** the manifest/descriptor URLs must be `https://` (raw is) — no `Info.plist` exception needed.
