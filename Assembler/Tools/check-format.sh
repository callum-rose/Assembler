#!/usr/bin/env bash
#
# C# format check — verifies that C# under Assets/ matches house style (the conventions in CLAUDE.md ›
# Code Conventions › C# Style, encoded in the repo .editorconfig). The formatter is `dotnet format`
# (built into the .NET SDK, no extra tooling), so the CLI and Rider apply the same Roslyn rules. It
# normalises whitespace/indentation and enforces the `:warning` rules (always-braces); it does NOT
# re-wrap lines, so your line breaks are preserved.
#
# `dotnet format` operates on a solution/project, and the Unity .sln/.csproj are gitignored and
# regenerated on demand, so this script boots Unity in batch mode to (re)generate them via Unity's
# built-in UnityEditor.SyncVS.SyncSolution when they're missing or stale, then runs dotnet format
# against Assembler.sln.
# That makes it the heaviest Tools script (a Unity boot + an MSBuild workspace load — a couple of
# minutes). Unity emits benign MSBuild workspace warnings; they don't affect formatting.
#
# Usage:
#   Tools/check-format.sh                 # check files changed vs master (default)
#   Tools/check-format.sh --all           # check every .cs under Assets/ (excluding vendored)
#   Tools/check-format.sh path [path...]  # check specific files / directories
#   Tools/check-format.sh --fix [...]     # format in place instead of checking
#
# Check mode uses `dotnet format --verify-no-changes` (read-only); --fix writes. Exits 0 if everything
# is formatted, non-zero if anything needs reformatting (or on a usage/IO error).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$PROJECT" && git rev-parse --show-toplevel)"
cd "$PROJECT"
SOLUTION="$PROJECT/Assembler.sln"

#### args ####
MODE="check"
ALL=0
PATHS=()
for arg in "$@"; do
	case "$arg" in
		--fix) MODE="fix" ;;
		--all) ALL=1 ;;
		-h|--help) sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//; $d'; exit 0 ;;
		*) PATHS+=("$arg") ;;
	esac
done

# True for vendored paths we don't own the style of.
ignored() { [[ "$1" == Assets/Plugins/* || "$1" == "Assets/TextMesh Pro/"* ]]; }

#### resolve the set of files to format (project-relative, .cs only) ####
TARGETS=()
if [[ "$ALL" -eq 1 ]]; then
	while IFS= read -r f; do ignored "$f" || TARGETS+=("$f"); done \
		< <(cd "$PROJECT" && find Assets -name '*.cs' | sort)
elif [[ "${#PATHS[@]}" -gt 0 ]]; then
	for p in "${PATHS[@]}"; do ignored "$p" || TARGETS+=("$p"); done
else
	BASE="$(git merge-base HEAD master 2>/dev/null || echo master)"
	# git paths are repo-relative (repo root is the parent of Assembler/); strip the Assembler/ prefix.
	while IFS= read -r f; do
		f="${f#Assembler/}"
		[[ "$f" == *.cs ]] || continue
		ignored "$f" && continue
		TARGETS+=("$f")
	done < <(
		{
			git -C "$REPO_ROOT" diff --name-only --diff-filter=ACMR "$BASE" -- 'Assembler/Assets/**/*.cs'
			git -C "$REPO_ROOT" ls-files --others --exclude-standard -- 'Assembler/Assets/**/*.cs'
		} | sort -u
	)
	if [[ "${#TARGETS[@]}" -eq 0 ]]; then
		echo "No changed C# files vs master — nothing to format-check. (Use --all to check everything.)"
		exit 0
	fi
fi
if [[ "${#TARGETS[@]}" -eq 0 ]]; then
	echo "No C# files to check (all targets were vendored/ignored paths)."
	exit 0
fi

#### locate Unity (same pattern as the other Tools scripts) ####
VERSION_FILE="$PROJECT/ProjectSettings/ProjectVersion.txt"
[[ -f "$VERSION_FILE" ]] || { echo "error: $VERSION_FILE not found — is this an Assembler Unity project?" >&2; exit 1; }
VERSION="$(awk '/^m_EditorVersion:/ { print $2; exit }' "$VERSION_FILE")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
[[ -x "$UNITY" ]] || { echo "error: Unity $VERSION not found at $UNITY" >&2; exit 1; }

#### (re)generate the solution if missing or older than any .cs we care about ####
need_solution_regen() {
	[[ -f "$SOLUTION" ]] || return 0
	[[ -n "$(find Assets -name '*.cs' -newer "$SOLUTION" -print -quit 2>/dev/null)" ]]
}
if need_solution_regen; then
	# Two Unity processes on one path corrupt the Library; refuse if an editor already has THIS path open.
	if ps -axww -o command= | awk -v u="$UNITY" -v p="$PROJECT" 'index($0, u) == 1 && index($0, p)' | grep -q .; then
		echo "error: a Unity editor already has this project path open ($PROJECT) — close it and re-run." >&2
		exit 1
	fi
	echo "Generating Unity solution with Unity $VERSION (one-time / when stale)..."
	# SyncVS.SyncSolution is Unity's built-in IDE project generator — invokable directly via
	# -executeMethod, so no custom editor script is needed in the project.
	"$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
		-executeMethod UnityEditor.SyncVS.SyncSolution -logFile - >/dev/null
	[[ -f "$SOLUTION" ]] || { echo "error: solution generation did not produce $SOLUTION" >&2; exit 1; }
fi

#### run dotnet format ####
VERIFY=()
[[ "$MODE" == "check" ]] && VERIFY=(--verify-no-changes)

echo "Running dotnet format over ${#TARGETS[@]} file(s) (MSBuild workspace load — this is slow)..."
# Unity projects emit benign MSBuild workspace warnings; --no-restore avoids a pointless NuGet restore.
if dotnet format "$SOLUTION" --no-restore ${VERIFY[@]+"${VERIFY[@]}"} --include "${TARGETS[@]}"; then
	[[ "$MODE" == "check" ]] && echo "✓ All checked C# files are correctly formatted." || echo "✓ Formatting applied."
	exit 0
else
	status=$?
	if [[ "$MODE" == "check" ]]; then
		echo "✗ Not formatted — run 'Tools/check-format.sh --fix' to apply." >&2
	fi
	exit "$status"
fi
