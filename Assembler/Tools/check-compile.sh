#!/usr/bin/env bash
#
# Recompiles the project's scripts headlessly and reports compiler errors and warnings — the same
# diagnostics you'd see in the Unity Console after a script change, without the editor UI. Boots
# Unity in batch mode, parses the compiler output (the "...: error CS####: ..." / "warning CS####"
# lines) out of the log, prints a short summary, and exits non-zero if there are errors (or warnings,
# with --warnings-as-errors) — so Claude can check a change compiles cleanly WITHOUT running the
# (slower) full test suite.
#
# Why parse the log rather than collect messages in C#: Unity's CompilationPipeline message callbacks
# do not reliably deliver warnings in batch mode, but csc always writes every error and warning to the
# log, so the log is the trustworthy source. The exit code and summary are derived here, not in Unity.
#
# Two modes:
#  - default (incremental): a plain `-batchmode -quit` boot recompiles whatever changed on disk since
#    the last compile and logs those diagnostics. This scopes the report to the code you just edited
#    (and its dependents) instead of dumping every pre-existing project warning — the usual "did my
#    change compile?" check. Errors anywhere still surface (any error aborts the build).
#  - --all: forces a clean recompile of EVERY assembly (via Editor.CompileCheckBatch.RecompileAll) so
#    all warnings across the whole project resurface. Slower; use for a full audit.
#
# Usage:
#   Assembler/Tools/check-compile.sh                       # report errors + warnings in changed code
#   Assembler/Tools/check-compile.sh --warnings-as-errors  # also exit non-zero on warnings
#   Assembler/Tools/check-compile.sh --all                 # recompile everything; every warning
# Flags can be combined; -w is shorthand for --warnings-as-errors.
#
# Notes:
#  - Output is grouped by CS code to stay compact: repeats of one diagnostic collapse to a single
#    message line plus its locations, rather than N near-identical full lines.
#  - Errors in your own code (Assets/, excluding Assets/Plugins) are shown; errors in Library/PackageCache
#    are collapsed to a one-line count (those are the cold-import/license races a fresh worktree throws,
#    not anything you fix — re-run to clear). Any error still fails the build. Warnings are filtered to
#    your own code under Assets/ (excluding Assets/Plugins) to avoid third-party noise.
#  - The first run in a fresh worktree triggers a full asset import and is slow (minutes). Subsequent
#    runs reuse the Library/ cache and are fast.
#  - Like generate-docs.sh / validate-yaml.sh, this runs fine alongside an editor open on a DIFFERENT
#    path, but refuses if an editor already has THIS project path open (two Unity processes on one
#    path corrupt the Library).
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

WARNINGS_AS_ERRORS=0
ALL=0
while [[ $# -gt 0 ]]; do
	case "$1" in
		-w|--warnings-as-errors)
			WARNINGS_AS_ERRORS=1
			shift
			;;
		--all)
			ALL=1
			shift
			;;
		*)
			echo "error: unknown argument '$1' (expected --warnings-as-errors/-w or --all)" >&2
			exit 1
			;;
	esac
done

LOG="$(mktemp -t assembler-compile-check.XXXXXX.log)"

if [[ "$ALL" == "1" ]]; then
	echo "Checking compilation (full recompile) with Unity $VERSION (project: $PROJECT)..."
	UNITY_ARGS=(-batchmode -nographics -projectPath "$PROJECT"
		-executeMethod Editor.CompileCheckBatch.RecompileAll)
else
	echo "Checking compilation (changed code) with Unity $VERSION (project: $PROJECT)..."
	# -quit: the boot-time script compile is synchronous, so by the time Unity processes -quit every
	# diagnostic is already in the log. (--all can't use -quit — its recompile is async; see the C#.)
	UNITY_ARGS=(-batchmode -quit -nographics -projectPath "$PROJECT")
fi

# Capture the (very noisy) Unity log to a temp file rather than streaming it — only the extracted
# summary below is printed. Don't let a non-zero Unity exit abort the script before we summarise.
set +e
"$UNITY" "${UNITY_ARGS[@]}" -logFile - > "$LOG" 2>&1
set -e

# Pull the compiler diagnostics straight out of the log. Unity logs each one a few times, so awk
# de-dupes while preserving first-seen order. The output is grouped by CS code (see GROUP_AWK) so
# that N repeats of the same warning collapse to one message + N locations instead of N near-identical
# full lines — the project's ~50 pre-existing nullable warnings otherwise dominate the context budget.
#
# Errors are split: those in your own code (Assets/, excluding Assets/Plugins/) are shown grouped;
# errors anywhere else (Library/PackageCache — the Unity packages) are collapsed to a one-line count,
# because in practice those are the spurious cold-import / license-handshake races a fresh worktree
# throws on its first run, not anything you can fix (re-running clears them). Warnings are likewise
# restricted to your own code so the summary isn't drowned in third-party / package noise.
ALL_ERRORS="$(grep -E ': error CS[0-9]+:' "$LOG" | awk '!seen[$0]++' || true)"
WARNINGS="$(grep -E '^Assets/.*: warning CS[0-9]+:' "$LOG" | grep -v '^Assets/Plugins/' | awk '!seen[$0]++' || true)"

ASSETS_ERRORS="$(printf '%s\n' "$ALL_ERRORS" | grep -E '^Assets/' | grep -v '^Assets/Plugins/' || true)"
EXTERNAL_ERRORS="$(printf '%s\n' "$ALL_ERRORS" | grep -vE '^Assets/' || true)"

# Count only non-empty lines (a blank string still printf's one empty line, which grep -c . excludes).
count_lines() { [[ -z "$1" ]] && echo 0 || printf '%s\n' "$1" | grep -c .; }
ASSETS_ERROR_COUNT="$(count_lines "$ASSETS_ERRORS")"
EXTERNAL_ERROR_COUNT="$(count_lines "$EXTERNAL_ERRORS")"
WARNING_COUNT="$(count_lines "$WARNINGS")"
ERROR_COUNT=$((ASSETS_ERROR_COUNT + EXTERNAL_ERROR_COUNT))

# Group "path(L,C): <sev> CSxxxx: message" lines by CS code. A code with one occurrence prints on a
# single line; a code with many prints the message once followed by its indented locations. Written
# for BSD (macOS) awk — no gensub / 3-arg match / gawk extensions.
GROUP_AWK='
{
  line = $0
  if (!match(line, /: (error|warning) CS[0-9]+: /)) next
  loc = substr(line, 1, RSTART - 1)
  rest = substr(line, RSTART + 2)
  sp = index(rest, " ")
  rest2 = substr(rest, sp + 1)
  colon = index(rest2, ":")
  code = substr(rest2, 1, colon - 1)
  msg = substr(rest2, colon + 2)
  if (!(code in seen)) { order[++n] = code; seen[code] = 1; firstmsg[code] = msg }
  count[code]++
  locs[code] = locs[code] loc "\n"
}
END {
  for (i = 1; i <= n; i++) {
    c = order[i]
    if (count[c] == 1) {
      l = locs[c]; sub(/\n$/, "", l)
      printf "  %s  %s: %s\n", c, l, firstmsg[c]
    } else {
      printf "  %s ×%d  %s\n", c, count[c], firstmsg[c]
      cnt = split(locs[c], arr, "\n")
      for (j = 1; j <= cnt; j++) if (arr[j] != "") printf "    %s\n", arr[j]
    }
  }
}'

ERR_SUMMARY="Errors: $ASSETS_ERROR_COUNT"
[[ "$EXTERNAL_ERROR_COUNT" -gt 0 ]] && ERR_SUMMARY="$ERR_SUMMARY (+$EXTERNAL_ERROR_COUNT external)"

echo
echo "================== Compile check =================="
echo "$ERR_SUMMARY   Warnings: $WARNING_COUNT$([[ "$WARNINGS_AS_ERRORS" == "1" ]] && echo '  (warnings treated as errors)')"
if [[ "$ASSETS_ERROR_COUNT" -gt 0 ]]; then
	echo
	echo "Errors in your code ($ASSETS_ERROR_COUNT):"
	printf '%s\n' "$ASSETS_ERRORS" | awk "$GROUP_AWK"
fi
if [[ "$EXTERNAL_ERROR_COUNT" -gt 0 ]]; then
	echo
	echo "$EXTERNAL_ERROR_COUNT external compiler error(s) in Library/PackageCache (not your code) —"
	echo "  usually a transient cold-import / license race on a fresh worktree; re-run to clear."
fi
if [[ "$WARNING_COUNT" -gt 0 ]]; then
	echo
	echo "Warnings ($WARNING_COUNT):"
	printf '%s\n' "$WARNINGS" | awk "$GROUP_AWK"
fi
echo "==================================================="
echo
echo "Full Unity log: $LOG"

# Verdict: fail on any error (your code OR external — both genuinely break the build; an external-only
# failure is reported as likely cold-import noise, and re-running is the fix), or on any warning when
# --warnings-as-errors is set.
if [[ "$ERROR_COUNT" -gt 0 ]]; then
	exit 1
fi
if [[ "$WARNINGS_AS_ERRORS" == "1" && "$WARNING_COUNT" -gt 0 ]]; then
	exit 1
fi
exit 0
