#!/usr/bin/env bash
#
# Run the voxelization pipeline headlessly, billing the Claude *subscription* (plan) instead of API
# credits. Boots the Unity editor in batch mode and invokes Editor.VoxelizeBatch.Run, which funnels every
# pipeline LLM call through the `claude -p` CLI (ClaudeCliGateway) rather than the Anthropic API. From a
# brief it first generates a set manifest, then plans/authors/assembles/validates/exports each asset; from
# an existing manifest it skips straight to running the assets. Exports land under --output-folder.
#
# Usage:
#   Assembler/Tools/voxelize.sh --brief "a single red cube"
#   Assembler/Tools/voxelize.sh --brief "pirate cove props" --output-folder Assets/GeneratedVoxels
#   Assembler/Tools/voxelize.sh --manifest path/to/set.manifest.yaml --image-folder path/to/refs
#   Assembler/Tools/voxelize.sh --manifest set.yaml --only tree,rock --note "make them chunkier"
#
# Flags:
#   --brief <text>            Generate a manifest from this brief, then run it.
#   --manifest <path>         Run an existing *.manifest.yaml (mutually exclusive with --brief).
#   --image-folder <dir>      Folder the manifest's reference: files resolve against.
#   --output-folder <dir>     Where exported models are written (default: Assets/GeneratedVoxels).
#   --only <a,b,c>            Comma-separated asset ids to run (subset of the manifest).
#   --note <text>             Refinement note threaded into each asset run.
#   --manifest-model <id>     Override the manifest-stage model (mapped to a CLI alias).
#   --planning-model <id>     Override the planning/brief/review-stage model.
#   --authoring-model <id>    Override the part-authoring-stage model.
#   --concurrency <N>         Max concurrent `claude -p` processes (default: 3).
#
# Exits 0 if every requested asset produced a model, 1 if any asset failed (or the run threw).
#
# Auth: uses the plan via the `claude` CLI's OAuth — no ANTHROPIC_API_KEY is needed or used (the gateway
# strips it from the child env defensively). The CLI must be on PATH, or set CLAUDE_CLI_PATH to its location
# (a Unity editor launched from the Hub has a minimal PATH that may omit it; launching via this script from a
# normal shell passes the full PATH through).
#
# Concurrency: like the other Tools/*.sh, this runs fine alongside an editor open on a DIFFERENT path, but
# refuses to run if an editor already has THIS project path open (two Unity processes on one path corrupt the
# Library). The first run in a fresh worktree does a one-time cold import (~3 min); subsequent runs are fast.
set -euo pipefail

# Project = the Assembler/ directory (parent of this script's Tools/ dir), resolved absolutely so the
# script works from any worktree and any current directory.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION_FILE="$PROJECT/ProjectSettings/ProjectVersion.txt"
if [[ ! -f "$VERSION_FILE" ]]; then
	echo "error: $VERSION_FILE not found — is this an Assembler Unity project?" >&2
	exit 1
fi

VERSION="$(awk '/^m_EditorVersion:/ { print $2; exit }' "$VERSION_FILE")"
if [[ -z "$VERSION" ]]; then
	echo "error: could not read m_EditorVersion from $VERSION_FILE" >&2
	exit 1
fi

UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
if [[ ! -x "$UNITY" ]]; then
	echo "error: Unity $VERSION not found at $UNITY" >&2
	echo "       install it via Unity Hub, or update ProjectVersion.txt to an installed version." >&2
	exit 1
fi

# Refuse to run if an editor already has THIS exact project path open — two Unity processes on one path
# corrupt the Library. An editor on a different path (your main checkout) is fine and expected.
if ps -axww -o command= | awk -v u="$UNITY" -v p="$PROJECT" 'index($0, u) == 1 && index($0, p)' | grep -q .; then
	echo "error: a Unity editor already has this project path open:" >&2
	echo "         $PROJECT" >&2
	echo "       Unity cannot open the same path from two processes. Close that editor and re-run." >&2
	echo "       (Running alongside an editor open on a DIFFERENT path — e.g. your main checkout — is fine.)" >&2
	exit 1
fi

# Translate the user-facing long flags into the single-dash flags Editor.VoxelizeBatch.Run reads off the
# command line. Relative path-like values are resolved against the current directory so they survive the
# editor's chdir to the project root; free text (brief, note) is forwarded verbatim.
METHOD_ARGS=()

resolve_path() {
	# Best-effort absolutise: if the value names an existing path, anchor it; otherwise leave it.
	local v="$1"
	if [[ -e "$v" ]]; then
		echo "$(cd "$(dirname "$v")" && pwd)/$(basename "$v")"
	else
		echo "$v"
	fi
}

while [[ $# -gt 0 ]]; do
	case "$1" in
		--brief)           METHOD_ARGS+=(-brief "$2"); shift 2 ;;
		--manifest)        METHOD_ARGS+=(-manifest "$(resolve_path "$2")"); shift 2 ;;
		--image-folder)    METHOD_ARGS+=(-imageFolder "$(resolve_path "$2")"); shift 2 ;;
		--output-folder)   METHOD_ARGS+=(-outputFolder "$2"); shift 2 ;;
		--only)            METHOD_ARGS+=(-only "$2"); shift 2 ;;
		--note)            METHOD_ARGS+=(-note "$2"); shift 2 ;;
		--manifest-model)  METHOD_ARGS+=(-manifestModel "$2"); shift 2 ;;
		--planning-model)  METHOD_ARGS+=(-planningModel "$2"); shift 2 ;;
		--authoring-model) METHOD_ARGS+=(-authoringModel "$2"); shift 2 ;;
		--concurrency)     METHOD_ARGS+=(-concurrency "$2"); shift 2 ;;
		-h|--help)
			sed -n '2,40p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
			exit 0 ;;
		*)
			echo "error: unknown flag '$1' (see --help)" >&2
			exit 1 ;;
	esac
done

if [[ ${#METHOD_ARGS[@]} -eq 0 ]]; then
	echo "error: nothing to do — pass --brief \"...\" or --manifest <path> (see --help)." >&2
	exit 1
fi

LOG="$(mktemp -t assembler-voxelize.XXXXXX.log)"

echo "Running the voxelization pipeline with Unity $VERSION on the Claude plan (project: $PROJECT)..."

# Capture the (very noisy) Unity log to a temp file rather than streaming it — only the report block the
# batch method emits between its delimiter lines is printed, so the summary isn't buried under licensing /
# import noise. Don't let a non-zero Unity exit abort the script before we extract the report.
set +e
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.VoxelizeBatch.Run \
	"${METHOD_ARGS[@]}" \
	-logFile - > "$LOG" 2>&1
RC=$?
set -e

# Extract the report between the batch method's header line and its trailing all-equals footer.
REPORT="$(awk '/============== Voxelization run ==============/{f=1} f{print} f && /^=+$/{exit}' "$LOG")"

echo
if [[ -n "$REPORT" ]]; then
	printf '%s\n' "$REPORT"
else
	echo "error: no voxelization report found in the Unity log — the editor likely failed to start." >&2
	echo "       (A fresh worktree's first run does a one-time cold import; re-running usually fixes a" >&2
	echo "        spurious cold-import failure. Check the full log below for a claude-CLI auth/path error.)" >&2
fi
echo
echo "Full Unity log: $LOG"

# Verdict comes from the batch method's exit code (0 = every asset produced a model, 1 = a failure).
exit "$RC"
