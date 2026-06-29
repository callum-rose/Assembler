#!/usr/bin/env bash
#
# setup-store.sh — one-time creation of the remote game store: a public GitHub repo that holds
# manifest.json + games/<id>/descriptor.yaml, served free over raw.githubusercontent.com. Also
# creates the "generate" issue label the daemon listens on.
#
# Usage:
#   setup-store.sh [repo-name]        # default repo-name: assembler-games
#
# Configuration (env):
#   ASSEMBLER_STORE_DIR    where to clone the store locally   (default: ~/Developer/assembler-games)
#   ASSEMBLER_GEN_LABEL    request label                      (default: generate)
#
# Requirements: gh (authenticated with repo + issue scopes), git.
set -euo pipefail

REPO_NAME="${1:-assembler-games}"
STORE_DIR="${ASSEMBLER_STORE_DIR:-$HOME/Developer/$REPO_NAME}"
LABEL="${ASSEMBLER_GEN_LABEL:-generate}"

err()  { printf '\033[31m%s\033[0m\n' "$*" >&2; }
info() { printf '\033[36m%s\033[0m\n' "$*" >&2; }

command -v gh  >/dev/null || { err "gh is required (brew install gh; gh auth login)"; exit 1; }
OWNER="$(gh api user --jq .login)"

if [ -d "$STORE_DIR/.git" ]; then
	info "store already exists at $STORE_DIR — skipping local init"
else
	info "creating local store at $STORE_DIR"
	mkdir -p "$STORE_DIR/games"
	cd "$STORE_DIR"
	git init -q -b main
	echo '{ "version": 1, "games": [] }' > manifest.json
	cat > README.md <<EOF
# $REPO_NAME

Remote game store for the Assembler player app. \`manifest.json\` is the shelf index; each game's
YAML descriptor lives under \`games/<id>/descriptor.yaml\`. Published by \`Tools/remote/publish-game.sh\`
in the engine repo and served to the app over raw.githubusercontent.com.

**Generate a game from anywhere:** open an issue labelled \`$LABEL\` — the title (or body) is the brief.
The generation daemon on the dev Mac picks it up, builds the game, publishes it here, and closes the issue.
EOF
	mkdir -p games && touch games/.gitkeep
	git add -A && git commit -q -m "Initialise game store"
fi

# Create the GitHub repo if it doesn't exist, then push.
if gh repo view "$OWNER/$REPO_NAME" >/dev/null 2>&1; then
	info "GitHub repo $OWNER/$REPO_NAME already exists"
	git -C "$STORE_DIR" remote get-url origin >/dev/null 2>&1 \
		|| git -C "$STORE_DIR" remote add origin "https://github.com/$OWNER/$REPO_NAME.git"
else
	info "creating public GitHub repo $OWNER/$REPO_NAME"
	gh repo create "$OWNER/$REPO_NAME" --public --source "$STORE_DIR" --remote origin --push
fi
git -C "$STORE_DIR" push -q -u origin main || true

# Ensure the request label exists (ignore "already exists").
gh label create "$LABEL" --repo "$OWNER/$REPO_NAME" --description "Assembler game generation request" --color 5319E7 2>/dev/null || true

MANIFEST_URL="https://raw.githubusercontent.com/$OWNER/$REPO_NAME/main/manifest.json"
info "Store ready."
echo
echo "  Local store : $STORE_DIR"
echo "  GitHub repo : https://github.com/$OWNER/$REPO_NAME"
echo "  Manifest URL: $MANIFEST_URL"
echo
echo "Next:"
echo "  1. Set GameShelf._manifestUrl (Bootstrap scene) to the Manifest URL above."
echo "  2. Publish a game:  Tools/remote/publish-game.sh \"a simple dodge-the-blocks game\""
echo "  3. Run the daemon:  ASSEMBLER_STORE_REPO=$OWNER/$REPO_NAME Tools/remote/generation-daemon.sh"
