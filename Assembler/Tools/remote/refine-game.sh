#!/usr/bin/env bash
#
# refine-game.sh — apply a natural-language change to an already-published game and republish it
# (bumping its version so clients re-download). Reuses publish-game.sh for validation + publishing.
#
# Usage:
#   refine-game.sh <game-id> "<change request>"
#   e.g. refine-game.sh neon-dodge "make the obstacles spawn twice as fast and add a score counter"
#
# Configuration: same env vars as publish-game.sh (ASSEMBLER_STORE_DIR, CLAUDE_CLI_PATH, …).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STORE_DIR="${ASSEMBLER_STORE_DIR:-$HOME/Developer/assembler-games}"
CLAUDE_BIN="${CLAUDE_CLI_PATH:-claude}"

err()  { printf '\033[31m%s\033[0m\n' "$*" >&2; }
info() { printf '\033[36m%s\033[0m\n' "$*" >&2; }

[ $# -ge 2 ] || { err "usage: refine-game.sh <game-id> \"<change request>\""; exit 2; }
ID="$1"; CHANGE="$2"

CURRENT="$STORE_DIR/games/$ID/descriptor.yaml"
[ -f "$CURRENT" ] || { err "no published game with id '$ID' (looked for $CURRENT)"; exit 1; }
command -v "$CLAUDE_BIN" >/dev/null || { err "claude CLI not found (set CLAUDE_CLI_PATH)"; exit 1; }

WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
NEW="$WORK/$ID.yaml"

info "Refining '$ID': $CHANGE"
env -u ANTHROPIC_API_KEY -u ANTHROPIC_AUTH_TOKEN "$CLAUDE_BIN" -p \
	--output-format text \
	"Use the generate-game-descriptor skill to revise an existing Assembler game descriptor. Apply this change: \"$CHANGE\". Keep everything else intact. Hard constraints unchanged: built-in primitive renderers only (no Assets: block), and a reachable !gameover path must remain. Output ONLY the full revised YAML document — no prose, no code fences.

Current descriptor:
$(cat "$CURRENT")" \
	> "$NEW"

[ -s "$NEW" ] || { err "refinement produced an empty descriptor"; exit 1; }

# Republish under the same id; publish-game.sh validates, bumps the version, commits and pushes.
exec "$HERE/publish-game.sh" "$NEW" "$ID"
