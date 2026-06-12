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
#  - Like the other scripts, the raw Unity log is captured to a temp file and only TestBatch's
#    delimited results block is printed, so the summary isn't buried under boot noise. The temp log
#    path is printed at the end for when you need the full detail.
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

LOG="$(mktemp -t assembler-run-tests.XXXXXX.log)"

echo "Running EditMode tests with Unity $VERSION (project: $PROJECT)..."

# Capture the (very noisy) Unity log to a temp file rather than streaming it — only the delimited
# "TestBatch results" block (pass/fail counts + any failures) is printed, so the summary isn't buried
# under licensing-handshake errors, asset-import spam, etc. Don't let a non-zero Unity exit abort the
# script before we extract the report.
# Note: "${FILTER_ARGS[@]+...}" guards the empty-array case — under `set -u`, macOS's bash 3.2
# treats a bare "${FILTER_ARGS[@]}" on an empty array as an unbound-variable error.
set +e
"$UNITY" \
	-batchmode -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.TestBatch.RunEditModeTests \
	${FILTER_ARGS[@]+"${FILTER_ARGS[@]}"} \
	-logFile - > "$LOG" 2>&1
RC=$?
set -e

# Extract the report between TestBatch's header line and its trailing all-equals footer. The header
# carries text so it never matches the footer pattern; the footer guard keys off `f` so a stray
# all-equals line in the boot noise (before the header) can't trip an early exit.
REPORT="$(awk '/================ TestBatch results ================/{f=1} f{print} f && /^=+$/{exit}' "$LOG")"

echo
if [[ -n "$REPORT" ]]; then
	printf '%s\n' "$REPORT"
else
	echo "error: no test report found in the Unity log — the editor likely failed to start." >&2
	echo "       (A fresh worktree's first run does a one-time cold import; re-running usually fixes a" >&2
	echo "        spurious cold-import failure.)" >&2
fi
echo
echo "Full NUnit XML: $PROJECT/TestResults/EditMode-results.xml"
echo "Full Unity log: $LOG"

# Verdict comes from TestBatch's exit code (0 = all passed, 1 = a failure/error), which Unity returns.
exit "$RC"
