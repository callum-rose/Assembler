#!/usr/bin/env bash
#
# Standalone ExpressionMethodCompiler check: feeds expressions straight through the project's custom
# expression compiler and reports compile errors (with the positions the compiler embeds in its messages)
# WITHOUT booting a game. This is the cheap, sub-second companion to validate-game.sh for exactly the
# failure class the expression-compiler authoring guidance warns about — bad expression syntax that
# otherwise only surfaces at *runtime*. It boots Unity in batch mode, invokes
# Editor.CheckExpressionBatch.Check, and exits non-zero on any compile failure, so Claude can verify a
# compiler snippet before committing it.
#
# Two input modes (combinable in one run):
#   - raw snippets:     compile one or more expression strings you pass directly.
#   - descriptor sweep: extract EVERY expression embedded in a descriptor (named + inline) and compile each.
# With no arguments it sweeps every descriptor under Assets/ExampleGameDescriptors as a batch audit.
#
# Usage:
#   Assembler/Tools/check-expression.sh                                  # audit all example descriptors
#   Assembler/Tools/check-expression.sh path/to/Game.yaml                # one or more specific descriptors
#   Assembler/Tools/check-expression.sh some/dir                         # every .yaml/.yml under a directory
#   Assembler/Tools/check-expression.sh -e 'RandomFloat(0f, 1f)'         # compile a raw snippet
#   Assembler/Tools/check-expression.sh -r vector -a 'vector:vel' -a 'float:dt' \
#                                       -e 'return AddVector(vel, ScaleVector(vel, dt));'
#
# Flags:
#   -e, --expr <code>       a raw expression snippet to compile (repeatable).
#   -r, --return-type <t>   return type for raw snippets (default: float; e.g. vector, bool, string).
#   -a, --arg <type>:<name> declare an argument for raw snippets (repeatable; e.g. 'vector:vel').
# Positional arguments are descriptor files/dirs to sweep. Raw-snippet and sweep inputs can be mixed.
#
# Exits 0 if every expression compiles, 1 if any fails.
#
# Concurrency: like the other Tools/ scripts, this runs fine alongside an editor open on a DIFFERENT path,
# but refuses if an editor already has THIS project path open (two Unity processes on one path corrupt the
# Library). The first run in a fresh worktree does a one-time cold import (~3 min); subsequent runs reuse
# the Library/ cache and are fast.
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

# Translate flags and positional descriptor paths into the batch method's argument flags. Relative paths
# are resolved against the current directory so they survive the editor's chdir to the project root.
BATCH_ARGS=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		-e|--expr)
			[[ $# -ge 2 ]] || { echo "error: $1 requires a value" >&2; exit 1; }
			BATCH_ARGS+=(-expr "$2")
			shift 2
			;;
		-r|--return-type)
			[[ $# -ge 2 ]] || { echo "error: $1 requires a value" >&2; exit 1; }
			BATCH_ARGS+=(-returnType "$2")
			shift 2
			;;
		-a|--arg)
			[[ $# -ge 2 ]] || { echo "error: $1 requires a value" >&2; exit 1; }
			BATCH_ARGS+=(-arg "$2")
			shift 2
			;;
		-*)
			echo "error: unknown flag '$1' (expected -e/--expr, -r/--return-type, -a/--arg, or a file/dir)" >&2
			exit 1
			;;
		*)
			arg="$1"
			if [[ -e "$arg" ]]; then
				arg="$(cd "$(dirname "$arg")" && pwd)/$(basename "$arg")"
			fi
			BATCH_ARGS+=(-descriptorPath "$arg")
			shift
			;;
	esac
done

LOG="$(mktemp -t assembler-expr-check.XXXXXX.log)"

echo "Checking expressions with Unity $VERSION (project: $PROJECT)..."

# Capture the (very noisy) Unity log to a temp file rather than streaming it — only the report block the
# batch method emits between its sentinels is printed. Don't let a non-zero Unity exit abort the script
# before we extract the report.
set +e
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.CheckExpressionBatch.Check \
	${BATCH_ARGS[@]+"${BATCH_ARGS[@]}"} \
	-logFile - > "$LOG" 2>&1
RC=$?
set -e

# Extract the report the batch method logged between its sentinel lines.
REPORT="$(awk '/===== check-expression report =====/{f=1} f{print} /===== end report =====/{exit}' "$LOG")"

echo
if [[ -n "$REPORT" ]]; then
	printf '%s\n' "$REPORT"
else
	echo "error: no expression report found in the Unity log — the editor likely failed to start." >&2
	echo "       (A fresh worktree's first run does a one-time cold import; re-running usually fixes a" >&2
	echo "        spurious cold-import failure.)" >&2
fi
echo
echo "Full Unity log: $LOG"

# Verdict comes from the batch method's exit code (0 = all compiled, 1 = a failure), which Unity returns.
exit "$RC"
