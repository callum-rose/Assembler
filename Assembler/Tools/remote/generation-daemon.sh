#!/usr/bin/env bash
#
# generation-daemon.sh — poll the store repo's GitHub issues for generation requests and fulfil them.
# Queue a game from anywhere (the GitHub mobile app, an iOS Shortcut, the web) by opening an issue
# labelled "generate"; the issue title (or body) is the brief. The daemon generates → validates →
# publishes → comments the result → closes the issue. Runs on your always-on Mac, typically under a
# launchd LaunchAgent (see com.assembler.generation-daemon.plist).
#
# Configuration (env):
#   ASSEMBLER_STORE_REPO   owner/repo of the store on GitHub (e.g. callumrose/assembler-games)  [required]
#   ASSEMBLER_POLL_SECONDS polling interval in seconds                                          [default 30]
#   ASSEMBLER_GEN_LABEL    issue label that marks a request                                     [default generate]
#   …plus every var publish-game.sh reads (ASSEMBLER_STORE_DIR, CLAUDE_CLI_PATH, ASSEMBLER_ENGINE_DIR).
#
# Requirements: gh (authenticated), jq, and everything publish-game.sh needs.
set -uo pipefail   # NOTE: no -e; one bad request must not kill the daemon.

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${ASSEMBLER_STORE_REPO:-}"
POLL_SECONDS="${ASSEMBLER_POLL_SECONDS:-30}"
LABEL="${ASSEMBLER_GEN_LABEL:-generate}"
LOCK="${TMPDIR:-/tmp}/assembler-generation-daemon.lock"

log() { printf '%s %s\n' "$(date '+%Y-%m-%dT%H:%M:%S')" "$*"; }

[ -n "$REPO" ] || { log "ERROR: set ASSEMBLER_STORE_REPO=owner/repo"; exit 1; }
command -v gh >/dev/null || { log "ERROR: gh not found"; exit 1; }
command -v jq >/dev/null || { log "ERROR: jq not found"; exit 1; }

# Single-flight: refuse to start a second daemon.
if ! mkdir "$LOCK" 2>/dev/null; then
	log "another daemon holds $LOCK — exiting"; exit 0
fi
trap 'rmdir "$LOCK" 2>/dev/null' EXIT

log "generation daemon started — repo=$REPO label=$LABEL poll=${POLL_SECONDS}s"

fulfil() {
	local number="$1" brief="$2"
	log "fulfilling #$number: $brief"

	local id output rc
	output="$("$HERE/publish-game.sh" "$brief" 2>&1)"; rc=$?
	id="$(printf '%s' "$output" | tail -n1)"

	if [ $rc -eq 0 ]; then
		log "published '$id' for #$number"
		# Use the REST endpoints directly (PRs/issues share them); avoids the gh GraphQL path.
		gh api "repos/$REPO/issues/$number/comments" -f body="✅ Published \`$id\`. It should appear on the shelf shortly." >/dev/null 2>&1
		gh api -X PATCH "repos/$REPO/issues/$number" -f state=closed >/dev/null 2>&1
	else
		log "FAILED #$number (rc=$rc)"
		gh api "repos/$REPO/issues/$number/comments" \
			-f body="❌ Generation failed:
\`\`\`
$(printf '%s' "$output" | tail -n 20)
\`\`\`" >/dev/null 2>&1
		# Leave the issue open (drop the label) so it isn't retried every poll.
		gh api -X DELETE "repos/$REPO/issues/$number/labels/$LABEL" >/dev/null 2>&1
	fi
}

while true; do
	# REST list of open issues carrying the label; .pull_request filters out PRs (which are also "issues").
	issues="$(gh api "repos/$REPO/issues?state=open&labels=$LABEL&per_page=20" 2>/dev/null \
		| jq -c '.[] | select(.pull_request == null) | {number, title, body}' 2>/dev/null)"

	if [ -n "$issues" ]; then
		while IFS= read -r row; do
			[ -n "$row" ] || continue
			num="$(printf '%s' "$row" | jq -r '.number')"
			title="$(printf '%s' "$row" | jq -r '.title')"
			body="$(printf '%s' "$row" | jq -r '.body // ""')"
			# Prefer a non-empty body as the brief; fall back to the title.
			brief="$title"
			[ -n "${body//[[:space:]]/}" ] && brief="$body"
			fulfil "$num" "$brief"
		done <<< "$issues"
	fi

	sleep "$POLL_SECONDS"
done
