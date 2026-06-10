#!/usr/bin/env bash
#
# Basic structural validation of game descriptor YAML, run headlessly by booting the Unity editor in
# batch mode and invoking Editor.YamlValidatorBatch.Validate. The validation itself lives in the
# runtime Assembler.Validation assembly (YamlStructureValidator), so the SAME code also runs inside a
# player build on any platform — this script is just the command-line front-end for authoring.
#
# It checks that each file is well-formed and free of common structural mistakes (duplicate keys,
# bad indentation, unterminated quotes/flows, tabs-as-indentation, ...) and reports each problem with
# line/column and a source snippet. It validates YAML *structure* only, not the descriptor schema.
#
# Usage:
#   Assembler/Tools/validate-yaml.sh                         # validate all example descriptors
#   Assembler/Tools/validate-yaml.sh path/to/File.yaml       # one or more specific files
#   Assembler/Tools/validate-yaml.sh some/dir                # every .yaml/.yml under a directory
#   (files and directories can be mixed and repeated)
#
# Exits 0 if every file is structurally valid, 1 if any file has errors, so Claude can run and verify.
#
# Concurrency: like generate-docs.sh, this runs fine alongside an editor open on a DIFFERENT path,
# but refuses to run if an editor already has THIS project path open (two Unity processes on one path
# corrupt the Library). The first run in a fresh worktree does a one-time cold import (~3 min);
# subsequent runs reuse the Library/ cache and are fast.
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

# Refuse to run if an editor already has THIS exact project path open — two Unity processes on one
# path corrupt the Library. An editor on a different path (your main checkout) is fine and expected.
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

LOG="$(mktemp -t assembler-validate-yaml.XXXXXX.log)"

echo "Validating descriptor YAML with Unity $VERSION (project: $PROJECT)..."

# Capture the (very noisy) Unity log to a temp file rather than streaming it — only the report block
# the batch method emits between its delimiter lines is printed, so the OK/FAIL summary isn't buried
# under licensing-handshake errors, "assembly not valid" notices, curl timeouts, etc. Don't let a
# non-zero Unity exit abort the script before we extract the report.
set +e
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.YamlValidatorBatch.Validate \
	${PATH_ARGS[@]+"${PATH_ARGS[@]}"} \
	-logFile - > "$LOG" 2>&1
RC=$?
set -e

# Extract the report between the batch method's header line and its trailing all-equals footer. The
# header carries text so it never matches the footer pattern; the footer guard keys off `f` so a
# stray all-equals line in the boot noise (before the header) can't trip an early exit.
REPORT="$(awk '/================ YAML validation ================/{f=1} f{print} f && /^=+$/{exit}' "$LOG")"

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

# Verdict comes from the batch method's exit code (0 = all valid, 1 = a failure), which Unity returns.
exit "$RC"
