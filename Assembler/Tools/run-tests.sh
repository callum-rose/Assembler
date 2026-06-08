#!/usr/bin/env bash
#
# Runs the EditMode test suites headlessly by booting the Unity editor in batch mode and invoking
# Editor.TestBatch.RunEditModeTests (the same tests you would run from Window > General > Test
# Runner). Prints a pass/fail summary to the log and exits non-zero if anything fails, so Claude
# can run and verify tests without the UI.
#
# Usage:
#   Assembler/Tools/run-tests.sh                       # run all EditMode tests
#   Assembler/Tools/run-tests.sh Tests.Compiler        # run only these assemblies (repeatable)
#   Assembler/Tools/run-tests.sh --filter '.*Lexer.*'  # run tests whose full name matches a regex
#   Assembler/Tools/run-tests.sh --category Slow        # run tests with a given [Category]
# Flags and assembly names can be combined; --filter/--category are repeatable.
#
# Notes:
#  - The first run in a fresh worktree triggers a full asset import and is slow (minutes).
#    Subsequent runs reuse the Library/ cache and are fast.
#  - Unity cannot open the same project path from two processes at once; close any editor already
#    open on this worktree before running.
#  - Unlike generate-docs.sh this does NOT pass -quit: the test run is asynchronous and TestBatch
#    exits the editor itself once tests finish.
#  - Full NUnit XML is written to TestResults/EditMode-results.xml.
set -euo pipefail

# Project = the Assembler/ directory (parent of this script's Tools/ dir), resolved absolutely so
# the script works from any worktree and any current directory.
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

# Translate friendly CLI args into the -testAssembly/-testFilter/-testCategory flags that
# TestBatch reads from the command line. Bare positional args are treated as assembly names.
FILTER_ARGS=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--filter)
			FILTER_ARGS+=(-testFilter "$2")
			shift 2
			;;
		--category)
			FILTER_ARGS+=(-testCategory "$2")
			shift 2
			;;
		-*)
			echo "error: unknown flag '$1' (expected --filter or --category, or a bare assembly name)" >&2
			exit 1
			;;
		*)
			FILTER_ARGS+=(-testAssembly "$1")
			shift
			;;
	esac
done

echo "Running EditMode tests with Unity $VERSION (project: $PROJECT)..."
# Note: "${FILTER_ARGS[@]+...}" guards the empty-array case — under `set -u`, macOS's bash 3.2
# treats a bare "${FILTER_ARGS[@]}" on an empty array as an unbound-variable error.
"$UNITY" \
	-batchmode -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.TestBatch.RunEditModeTests \
	${FILTER_ARGS[@]+"${FILTER_ARGS[@]}"} \
	-logFile -
