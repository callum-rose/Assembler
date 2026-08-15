#!/usr/bin/env bash
#
# Runs the test suites headlessly. By default it boots the Unity editor in batch mode and invokes
# Editor.TestBatch.RunEditModeTests (the same tests you would run from Window > General > Test Runner).
# PlayMode tests (which need behaviour Update to run — issue #101) use --player:
#   --player    builds a StandaloneOSX player and runs the PlayMode tests inside it (Unity's -runTests).
#               This is the reliable headless path; it also doesn't hold the project path open, so it
#               runs alongside other worktrees. Requires the Mac Standalone build module.
# (There is no in-editor batch PlayMode path: entering play mode triggers a domain reload that wedges the
# batch run on the play->edit transition, so PlayMode is player-only here.)
# Prints a pass/fail summary and exits non-zero if anything fails, so Claude can run/verify without a UI.
#
# Usage:
#   Assembler/Tools/run-tests.sh                             # all EditMode tests
#   Assembler/Tools/run-tests.sh --player Tests.Determinism  # PlayMode tests in a built player
#   Assembler/Tools/run-tests.sh Tests.Compiler              # run only these assemblies (repeatable)
#   Assembler/Tools/run-tests.sh --filter '.*Lexer.*'        # run tests whose full name matches a regex
#   Assembler/Tools/run-tests.sh --category Slow             # run tests with a given [Category]
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
#  - Full NUnit XML is written to TestResults/EditMode-results.xml (or Player-results.xml with --player).
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

# Bare positional args are assembly names; --filter/--category are repeatable. Collected into neutral
# arrays and mapped to the right flag names per run mode below: TestBatch uses -testAssembly/-testFilter/
# -testCategory, while Unity's -runTests (player mode) uses -assemblyNames/-testFilter/-testCategory.
MODE="EditMode"
PLAYER=0
ASSEMBLIES=()
FILTERS=()
CATEGORIES=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--player)
			PLAYER=1
			MODE="Player"
			shift
			;;
		--filter)
			FILTERS+=("$2")
			shift 2
			;;
		--category)
			CATEGORIES+=("$2")
			shift 2
			;;
		-*)
			echo "error: unknown flag '$1' (expected --player, --filter or --category, or a bare assembly name)" >&2
			exit 1
			;;
		*)
			ASSEMBLIES+=("$1")
			shift
			;;
	esac
done

LOG="$(mktemp -t assembler-run-tests.XXXXXX.log)"

echo "Running $MODE tests with Unity $VERSION (project: $PROJECT)..."

# Capture the (very noisy) Unity log to a temp file rather than streaming it, then print only the
# relevant results so the summary isn't buried under licensing/asset-import noise. Don't let a non-zero
# Unity exit abort the script before we report. ("${arr[@]+...}" guards the empty-array case: under
# `set -u`, macOS's bash 3.2 errors on a bare "${arr[@]}" when arr is empty.)
set +e
if [[ "$PLAYER" -eq 1 ]]; then
	# Built-player run via Unity's -runTests: builds a StandaloneOSX player and runs the PlayMode tests
	# in it, writing NUnit XML to -testResults. Reliable headless — no editor play->edit transition.
	RESULTS="$PROJECT/TestResults/Player-results.xml"
	mkdir -p "$PROJECT/TestResults"
	rm -f "$RESULTS"
	# -runTests splits -assemblyNames/-testFilter/-testCategory on ';' (not ','), so join with ';' — a
	# comma-joined multi-value list matches nothing and the framework still exits 0 with total="0".
	PLAYER_ARGS=(-batchmode -runTests -projectPath "$PROJECT" -testPlatform StandaloneOSX -testResults "$RESULTS")
	if [[ ${#ASSEMBLIES[@]} -gt 0 ]]; then PLAYER_ARGS+=(-assemblyNames "$(IFS=';'; echo "${ASSEMBLIES[*]}")"); fi
	if [[ ${#FILTERS[@]} -gt 0 ]]; then PLAYER_ARGS+=(-testFilter "$(IFS=';'; echo "${FILTERS[*]}")"); fi
	if [[ ${#CATEGORIES[@]} -gt 0 ]]; then PLAYER_ARGS+=(-testCategory "$(IFS=';'; echo "${CATEGORIES[*]}")"); fi
	"$UNITY" "${PLAYER_ARGS[@]}" -logFile "$LOG" >/dev/null 2>&1
	RC=$?
else
	# In-editor EditMode run via TestBatch.
	FILTER_ARGS=()
	for a in ${ASSEMBLIES[@]+"${ASSEMBLIES[@]}"}; do FILTER_ARGS+=(-testAssembly "$a"); done
	for f in ${FILTERS[@]+"${FILTERS[@]}"}; do FILTER_ARGS+=(-testFilter "$f"); done
	for c in ${CATEGORIES[@]+"${CATEGORIES[@]}"}; do FILTER_ARGS+=(-testCategory "$c"); done
	"$UNITY" \
		-batchmode -nographics \
		-projectPath "$PROJECT" \
		-executeMethod Editor.TestBatch.RunEditModeTests \
		${FILTER_ARGS[@]+"${FILTER_ARGS[@]}"} \
		-logFile - > "$LOG" 2>&1
	RC=$?
fi
set -e

echo
if [[ "$PLAYER" -eq 1 ]]; then
	RESULTS="$PROJECT/TestResults/Player-results.xml"
	echo "================ Player test results ================"
	if [[ -f "$RESULTS" ]]; then
		RUN="$(grep -oE '<test-run [^>]*>' "$RESULTS" | head -1)"
		printf '%s\n' "$RUN" | grep -oE '(total|passed|failed|skipped|inconclusive)="[0-9]+"' | tr '\n' '  '
		echo
		# Best-effort list of any failed test-case full names (attribute order varies, so match loosely).
		grep -oE '<test-case[^>]*result="Failed"[^>]*>' "$RESULTS" \
			| grep -oE 'fullname="[^"]*"' | sed 's/fullname="/  ✗ /; s/"$//'
		# Guard the whole class of filter/assembly typos: -runTests exits 0 even when it ran nothing.
		TOTAL="$(printf '%s\n' "$RUN" | grep -oE 'total="[0-9]+"' | grep -oE '[0-9]+')"
		if [[ "${TOTAL:-0}" -eq 0 ]]; then
			echo "  error: no tests were run (0 total) — check the assembly/filter names." >&2
			RC=1
		fi
	else
		echo "  (no results XML — the player build or run failed; see the log)"
		RC=1
	fi
	echo "===================================================="
	echo "Full NUnit XML: $RESULTS"
else
	# Extract TestBatch's delimited block between its header and trailing all-equals footer. The header
	# carries text so it never matches the footer; the footer guard keys off `f` so stray all-equals
	# boot noise before the header can't trip an early exit.
	REPORT="$(awk '/================ TestBatch results ================/{f=1} f{print} f && /^=+$/{exit}' "$LOG")"
	if [[ -n "$REPORT" ]]; then
		printf '%s\n' "$REPORT"
	else
		echo "error: no test report found in the Unity log — the editor likely failed to start." >&2
		echo "       (A fresh worktree's first run does a one-time cold import; re-running usually fixes a" >&2
		echo "        spurious cold-import failure.)" >&2
	fi
	echo "Full NUnit XML: $PROJECT/TestResults/$MODE-results.xml"
fi
echo "Full Unity log: $LOG"

# Verdict comes from Unity's exit code (0 = all passed; non-zero = a failure/error).
exit "$RC"
