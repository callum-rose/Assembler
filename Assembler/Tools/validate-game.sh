#!/usr/bin/env bash
#
# Semantic ("does it boot?") validation of game descriptor YAML, run headlessly by booting the Unity editor
# in batch mode and invoking Editor.GameSandboxValidatorBatch.Validate. Each descriptor is run through the
# full load pipeline in a throwaway sandbox — YAML structure → deserialisation → parsing/transforming →
# resolving → entity instantiation — and the result is reported per stage, so a failure pinpoints exactly
# which stage broke and why (thrown exceptions and Debug.LogErrors alike). The sandbox destroys everything
# it instantiates before moving on, so multiple files validate cleanly in one run.
#
# This is the deeper companion to validate-yaml.sh: that one checks YAML *structure* only and is fast; this
# one actually builds the game to catch unknown behaviours, bad expressions, missing assets, unbound
# controls, instantiation errors, etc. It validates that the game *starts* error-free; it does not run the
# per-frame game loop.
#
# Usage:
#   Assembler/Tools/validate-game.sh                         # sandbox-build all example descriptors
#   Assembler/Tools/validate-game.sh path/to/File.yaml       # one or more specific files
#   Assembler/Tools/validate-game.sh some/dir                # every .yaml/.yml under a directory
#   (files and directories can be mixed and repeated)
#
# Exits 0 if every file boots cleanly, 1 if any file fails to build, so Claude can run and verify.
#
# Concurrency: like validate-yaml.sh, this runs fine alongside an editor open on a DIFFERENT path, but
# refuses to run if an editor already has THIS project path open (two Unity processes on one path corrupt
# the Library). The first run in a fresh worktree does a one-time cold import (~3 min); subsequent runs
# reuse the Library/ cache and are fast.
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

# e.g. "m_EditorVersion: 6000.4.5f1" -> "6000.4.5f1"
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

# Translate each positional file/dir argument into a "-yamlPath <path>" pair for the batch method.
# Relative paths are resolved against the current directory so they survive the editor's chdir to the
# project root. With no arguments the batch method defaults to Assets/ExampleGameDescriptors.
PATH_ARGS=()
for arg in "$@"; do
	if [[ -e "$arg" ]]; then
		arg="$(cd "$(dirname "$arg")" && pwd)/$(basename "$arg")"
	fi
	PATH_ARGS+=(-yamlPath "$arg")
done

LOG="$(mktemp -t assembler-validate-game.XXXXXX.log)"

echo "Sandbox-building descriptor YAML with Unity $VERSION (project: $PROJECT)..."

# Capture the (very noisy) Unity log to a temp file rather than streaming it — only the report block
# the batch method emits between its delimiter lines is printed, so the OK/FAIL summary isn't buried
# under licensing-handshake errors, "assembly not valid" notices, curl timeouts, etc. Don't let a
# non-zero Unity exit abort the script before we extract the report.
set +e
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.GameSandboxValidatorBatch.Validate \
	${PATH_ARGS[@]+"${PATH_ARGS[@]}"} \
	-logFile - > "$LOG" 2>&1
RC=$?
set -e

# Extract the report between the batch method's header line and its trailing all-equals footer. The
# header carries text so it never matches the footer pattern; the footer guard keys off `f` so a
# stray all-equals line in the boot noise (before the header) can't trip an early exit.
REPORT="$(awk '/============== Game sandbox validation ==============/{f=1} f{print} f && /^=+$/{exit}' "$LOG")"

echo
if [[ -n "$REPORT" ]]; then
	printf '%s\n' "$REPORT"
else
	echo "error: no validation report found in the Unity log — the editor likely failed to start." >&2
	echo "       (A fresh worktree's first run does a one-time cold import; re-running usually fixes a" >&2
	echo "        spurious cold-import failure.)" >&2
fi
echo
echo "Full Unity log: $LOG"

# Verdict comes from the batch method's exit code (0 = all boot cleanly, 1 = a failure), which Unity returns.
exit "$RC"
