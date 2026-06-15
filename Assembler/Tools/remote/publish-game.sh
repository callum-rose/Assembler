#!/usr/bin/env bash
#
# publish-game.sh — generate (or take) a primitive-asset game descriptor, validate that it builds,
# and publish it to the remote store repo (manifest.json + games/<id>/descriptor.yaml), then push.
#
# Usage:
#   publish-game.sh "<brief>"                 # generate a new game from a one-line brief
#   publish-game.sh path/to/descriptor.yaml   # publish/refresh an existing descriptor
#   publish-game.sh "<brief>" my-game-id      # force the game id (slug)
#
# Configuration (override via environment — see Tools/remote/README.md):
#   ASSEMBLER_ENGINE_DIR   Unity project root that holds Tools/validate-game.sh   (default: auto-detected)
#   ASSEMBLER_STORE_DIR    Local clone of the assembler-games store repo          (default: ~/Developer/assembler-games)
#   ASSEMBLER_STORE_BRANCH Branch to publish to                                   (default: main)
#   CLAUDE_CLI_PATH        Path to the `claude` binary                            (default: claude on PATH)
#
# Requirements: bash, git, jq, shasum, gh (authenticated), and the `claude` CLI (for generation).
set -euo pipefail

# --- configuration -----------------------------------------------------------------------------
ENGINE_DIR="${ASSEMBLER_ENGINE_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
STORE_DIR="${ASSEMBLER_STORE_DIR:-$HOME/Developer/assembler-games}"
STORE_BRANCH="${ASSEMBLER_STORE_BRANCH:-main}"
CLAUDE_BIN="${CLAUDE_CLI_PATH:-claude}"

err()  { printf '\033[31m%s\033[0m\n' "$*" >&2; }
info() { printf '\033[36m%s\033[0m\n' "$*" >&2; }

[ $# -ge 1 ] || { err "usage: publish-game.sh \"<brief>\" | path/to/descriptor.yaml [game-id]"; exit 2; }
INPUT="$1"
FORCED_ID="${2:-}"

command -v jq   >/dev/null || { err "jq is required (brew install jq)"; exit 1; }
command -v gh   >/dev/null || { err "gh is required (brew install gh; gh auth login)"; exit 1; }
[ -d "$STORE_DIR/.git" ] || { err "store repo not found at $STORE_DIR — run Tools/remote/setup-store.sh first"; exit 1; }
[ -x "$ENGINE_DIR/Tools/validate-game.sh" ] || { err "validate-game.sh not found under $ENGINE_DIR/Tools"; exit 1; }

slugify() { echo "$1" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+|-+$//g' | cut -c1-48; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
DESC="$WORK/descriptor.yaml"

# --- 1. obtain the descriptor YAML -------------------------------------------------------------
if [ -f "$INPUT" ]; then
	cp "$INPUT" "$DESC"
	TITLE="$(basename "$INPUT" .yaml)"
	info "Using existing descriptor: $INPUT"
else
	command -v "$CLAUDE_BIN" >/dev/null || { err "claude CLI not found (set CLAUDE_CLI_PATH)"; exit 1; }
	TITLE="$INPUT"
	info "Generating descriptor for: $INPUT"
	# Plan-billed: no ANTHROPIC_API_KEY is passed, so the CLI uses your Claude subscription.
	# The generate-game-descriptor skill authors the YAML; we constrain it to primitive assets (v1)
	# and ask for the YAML on stdout only. If your skill writes to a file instead, see README.md.
	env -u ANTHROPIC_API_KEY -u ANTHROPIC_AUTH_TOKEN "$CLAUDE_BIN" -p \
		--output-format text \
		"Use the generate-game-descriptor skill to author a complete, runnable Assembler game descriptor for this idea: \"$INPUT\". Hard constraint: the game must use ONLY built-in primitive renderers — it must NOT declare a top-level Assets: block or reference any voxel/sprite/audio assets. It must declare a reachable !gameover path. Output ONLY the final YAML document, with no prose, no code fences, and nothing before or after it." \
		> "$DESC"
	[ -s "$DESC" ] || { err "generation produced an empty descriptor"; exit 1; }
fi

ID="${FORCED_ID:-$(slugify "$TITLE")}"
[ -n "$ID" ] || { err "could not derive a game id from '$TITLE' — pass one explicitly"; exit 1; }

# --- 2. validate it actually builds ------------------------------------------------------------
info "Validating '$ID' (booting Unity sandbox — this is slow)…"
if ! "$ENGINE_DIR/Tools/validate-game.sh" "$DESC"; then
	err "validation failed — not publishing. Descriptor left at: $DESC"
	trap - EXIT   # keep the temp dir so you can inspect/refine it
	exit 1
fi

# --- 3. publish to the store + push ------------------------------------------------------------
OWNER="$(gh repo view "$(git -C "$STORE_DIR" remote get-url "${ASSEMBLER_STORE_REMOTE:-origin}")" --json owner --jq .owner.login 2>/dev/null \
	|| gh api user --jq .login)"
REPO_NAME="$(basename -s .git "$(git -C "$STORE_DIR" remote get-url "${ASSEMBLER_STORE_REMOTE:-origin}")")"
RAW_BASE="https://raw.githubusercontent.com/$OWNER/$REPO_NAME/$STORE_BRANCH"

VER="$(shasum -a 256 "$DESC" | cut -c1-8)"
URL="$RAW_BASE/games/$ID/descriptor.yaml"

mkdir -p "$STORE_DIR/games/$ID"
cp "$DESC" "$STORE_DIR/games/$ID/descriptor.yaml"

MANIFEST="$STORE_DIR/manifest.json"
[ -f "$MANIFEST" ] || echo '{ "version": 1, "games": [] }' > "$MANIFEST"

jq --arg id "$ID" --arg title "$TITLE" --arg url "$URL" --arg ver "$VER" '
	.version = (.version // 1)
	| .games = ((.games // []) | map(select(.id != $id)) + [{id:$id, title:$title, descriptorUrl:$url, version:$ver}])
' "$MANIFEST" > "$MANIFEST.tmp" && mv "$MANIFEST.tmp" "$MANIFEST"

git -C "$STORE_DIR" add -A
git -C "$STORE_DIR" commit -q -m "Publish $ID ($VER)" || { info "nothing changed"; exit 0; }
git -C "$STORE_DIR" push -q "${ASSEMBLER_STORE_REMOTE:-origin}" "HEAD:$STORE_BRANCH"

info "Published '$ID' v$VER → $URL"
echo "$ID"
