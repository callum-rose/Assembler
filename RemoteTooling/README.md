# Remote loading & remote generation (Phase 2)

Dev-side tooling for the remote game pipeline: a standalone .NET console app (`assembler-remote`)
that lives **outside** the Unity project. It generates, validates, and publishes games to a separate
public GitHub repo (`assembler-games`), served over `raw.githubusercontent.com`. The phone app
downloads from there at runtime.

```
phone app  ──GET manifest.json──►  assembler-games repo  ◄──push──  assembler-remote (this Mac)
   shelf                            manifest.json + games/<id>/descriptor.yaml
   tap ────GET descriptor.yaml────►
```

Generation is **private dev tooling**, never exposed in the app (v1). This replaces the old
`Tools/remote/*.sh` scripts (`setup`, `publish`, `refine`, `daemon`); behaviour and env vars are unchanged.

## Build

From the **repo root**:

```sh
dotnet build -c Release RemoteTooling/Assembler.RemoteTooling.csproj
```

This produces `RemoteTooling/bin/Release/net10.0/assembler-remote`. Put it on your PATH, use the full
path, or run `dotnet run --project RemoteTooling -- <command>` from the repo root.

## One-time setup

1. **Install deps:** `brew install gh dotnet` and `gh auth login`. The `claude` CLI must be on PATH
   (generation is billed to your Claude subscription — no API key needed).
2. **Create the store repo:** `assembler-remote setup` (add a name to use a different repo). This creates
   `~/Developer/assembler-games`, the public GitHub repo, and the `generate` label, then prints your
   **Manifest URL**. (Don't append a trailing `# comment` — zsh passes the `#` as an argument.)
3. **Point the app at it:** open the `Bootstrap` scene, select the boot GameObject, set
   **GameShelf → Manifest Url** to that URL (see [Wiring the app](#wiring-the-app)).

## Daily use

```sh
# Generate from a brief, validate, and publish:
assembler-remote publish "a top-down game where you dodge falling rocks"

# Publish/refresh an existing local descriptor:
assembler-remote publish Assembler/Assets/ExampleGameDescriptors/Pong.yaml

# Refine a published game and bump its version (clients re-download):
assembler-remote refine dodge-falling-rocks "make the rocks faster and add a score"
```

Each publish commits + pushes to the store; the app picks up new/updated games on its next shelf
refresh (it re-fetches the manifest every time you exit a game).

## Generate from your phone (the always-on daemon)

Run the daemon to queue games from anywhere: open a GitHub issue labelled `generate` (via the GitHub
mobile app or an iOS Shortcut). The title/body is the brief.

Foreground (testing):

```sh
ASSEMBLER_STORE_REPO=<you>/assembler-games assembler-remote daemon
```

Background (recommended) — install the LaunchAgent so it runs at login and restarts on crash.
`deploy-daemon.sh` does the whole install: builds Release, writes the plist to `~/Library/LaunchAgents`
with real paths filled in, and reloads launchd. Re-run any time to redeploy.

- **From Rider:** run the committed **"Deploy Generation Daemon"** run configuration.
- **From a shell:** `RemoteTooling/deploy-daemon.sh` (from anywhere).

It auto-detects the store repo as `<your gh login>/assembler-games`; override `ASSEMBLER_STORE_REPO` /
`ASSEMBLER_STORE_DIR` / `ASSEMBLER_ENGINE_DIR` / `ASSEMBLER_POLL_SECONDS` via the environment if needed.

<details>
<summary>Manual install (equivalent steps)</summary>

1. Build once (see [Build](#build)).
2. `cp com.assembler.generation-daemon.plist ~/Library/LaunchAgents/`
3. Edit the **copy**, replacing `REPLACE_ME` / `REPLACE_OWNER` and checking `PATH` (edit the copy, not
   the tracked template, so your home path and username aren't committed).
4. `launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.assembler.generation-daemon.plist`
   (if already loaded, `launchctl bootout gui/$(id -u) …` first — a bare `bootstrap` of a loaded label
   fails with `5: Input/output error`).
</details>

The daemon comments on pick-up → generates → validates → publishes → comments the result (with any
generator feedback) → closes the issue. Failures leave the issue open (label removed) with a reason. It
holds a single-flight lock (a second daemon exits immediately) and releases it on SIGTERM.

### Comment as a bot (optional)

By default the daemon talks to GitHub through your `gh auth login`, so its comments post under **your**
account. To give it a distinct identity (e.g. *Game Generator Bot* with its own avatar and the `[bot]`
badge), register a GitHub App and point the daemon at it:

1. **Create the app:** GitHub → *Settings → Developer settings → GitHub Apps → New GitHub App*. Give it a
   name and avatar, set *Repository permissions → Issues: Read and write*, uncheck *Webhook → Active*, and
   create it. Note the **App ID**.
2. **Generate a private key** (bottom of the app's settings page) and save the downloaded `.pem` somewhere
   the daemon can read, e.g. `~/.config/assembler/gh-app.pem`.
3. **Install it on the store repo:** the app's *Install App* tab → install on the account that owns
   `assembler-games`, scoped to that repo. The install URL ends in `/installations/<ID>` — that number is
   the **installation ID** (or read it from `gh api /users/<owner>/installation` once installed).
4. **Point the daemon at it** by setting all three env vars (redeploy afterward — `deploy-daemon.sh` writes
   them into the LaunchAgent when present):

   ```sh
   ASSEMBLER_GH_APP_ID=123456 \
   ASSEMBLER_GH_APP_INSTALLATION_ID=7891011 \
   ASSEMBLER_GH_APP_KEY=~/.config/assembler/gh-app.pem \
   RemoteTooling/deploy-daemon.sh
   ```

The daemon mints a short-lived installation token from the key and uses it for every GitHub call
(comment/close/label as well as polling). All three vars must be set or it silently stays on the `gh`
login; a set-but-broken key (missing file / unparseable) fails loudly at startup. If minting ever fails at
runtime (e.g. a network blip) it logs a warning and falls back to the `gh` login for that call rather than
stalling. The startup log line shows which identity is active (`commenter=…`).

### Check what it's doing

```sh
assembler-remote status         # add --json for a machine-readable snapshot
```

Reports whether the daemon is alive, the in-flight job (issue, brief, phase, elapsed), the queue of open
`generate` issues, the last finished job, and running totals. It reads a heartbeat file next to its lock
and queries GitHub for the queue (needs `ASSEMBLER_STORE_REPO`). A crashed daemon leaves a stale file
that `status` detects (dead pid / missed heartbeats) and reports as *not running*.

**Which build is running?** The daemon logs its version on startup, and `assembler-remote version` prints
it. The version is `<Version>` in `Assembler.RemoteTooling.csproj`; bump it when you ship a change worth
telling apart.

## Wiring the app

The `Assembler.Remote` assembly (`Assembler/Assets/Remote/`) adds the runtime shelf. To switch a build
from single-game `GameBootstrap` to the remote shelf, in the **Bootstrap** scene replace the boot
GameObject's `GameBootstrap` component with **`GameShelf`** and set its **Manifest Url**. (One-click
editor change — it can't be scripted here because the component's GUID only exists after Unity imports
the new script.) `GameBootstrap` stays as a single-descriptor dev launcher.

## Configuration (env vars)

| Variable | Default | Used by |
|---|---|---|
| `ASSEMBLER_STORE_DIR` | `~/Developer/assembler-games` (setup: `~/Developer/<repo-name>`) | all |
| `ASSEMBLER_STORE_REPO` | — (required for daemon) | daemon |
| `ASSEMBLER_ENGINE_DIR` | auto-detected (the `Assembler/` Unity project) | publish |
| `ASSEMBLER_STORE_BRANCH` | `main` | publish |
| `ASSEMBLER_STORE_REMOTE` | `origin` | publish |
| `ASSEMBLER_POLL_SECONDS` | `30` | daemon |
| `ASSEMBLER_MAX_CONCURRENT` | `3` | daemon |
| `ASSEMBLER_GEN_LABEL` | `generate` | daemon, setup |
| `ASSEMBLER_GH_APP_ID` | — (all three enable bot commenter) | daemon |
| `ASSEMBLER_GH_APP_INSTALLATION_ID` | — | daemon |
| `ASSEMBLER_GH_APP_KEY` | — (path to the app's private-key `.pem`) | daemon |
| `CLAUDE_CLI_PATH` | `claude` | publish, refine |

## v1 limits & notes

- **Primitive assets only.** Games must not declare a top-level `Assets:` block. `RemoteGameGuard` rejects
  asset-bearing descriptors with a clean message; voxel-asset remote loading is a later phase.
- **Generation prompt may need tuning.** `publish` asks the `generate-game-descriptor` skill to emit YAML
  on stdout. If your skill version writes to a file instead, adjust the prompt in `GameGenerator.cs`.
- **`validate-game.sh` baseline:** some example descriptors already fail the sandbox validator on a clean
  tree; treat a hard failure (parse/instantiate error) as the publish gate.
- **Validation fix loop:** `publish` validates the descriptor and, on failure, feeds the validator's
  per-stage report back to the `generate-game-descriptor` skill to fix, then re-validates — looping until
  the descriptor builds cleanly (no attempt cap; only a generator that emits nothing at all is treated as a
  hard failure). The fix run happens outside the build gate, so it still overlaps other daemon workers'
  generation.
- **CDN freshness:** raw `githubusercontent.com` is always fresh (prefer it for the fast refine loop);
  jsDelivr caches `@latest` ~12h — switch to a pinned-SHA jsDelivr URL only for CDN scale.
- **iOS ATS:** manifest/descriptor URLs must be `https://` (raw is) — no `Info.plist` exception needed.
