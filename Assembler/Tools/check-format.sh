#!/usr/bin/env bash
#
# C# format check — verifies that C# under Assets/ matches house style (the conventions documented in
# CLAUDE.md › Code Conventions › C# Style, encoded in the repo .editorconfig). It wraps CSharpier,
# which is pinned in .config/dotnet-tools.json and owns mechanical formatting (indentation, brace
# placement, wrapping, spacing). The .editorconfig supplies the indent style, line endings, and print
# width; .csharpierignore excludes vendored code (Assets/Plugins, Assets/TextMesh Pro).
#
# Unlike the other Tools scripts this does NOT boot Unity — CSharpier runs standalone on the .cs files,
# so it's fast (~1s) and needs no editor, solution, or generated project. It only needs the .NET SDK.
#
# Usage:
#   Tools/check-format.sh                 # check files changed vs master (default — what you touched)
#   Tools/check-format.sh --all           # check every .cs under Assets/ (excluding vendored)
#   Tools/check-format.sh path [path...]  # check specific files / directories
#   Tools/check-format.sh --fix [...]     # format in place instead of checking (writes changes)
#
# Exits 0 if everything is formatted, non-zero if any file needs changes, so Claude can verify a change
# matches conventions before committing. Run with --fix (or `dotnet csharpier format <path>`) to fix.
set -euo pipefail

# Project = the Assembler/ directory (parent of this script's Tools/ dir), resolved absolutely so the
# script works from any worktree and any current directory. The dotnet tool manifest, .editorconfig and
# .csharpierignore all live here, and CSharpier must run with this as the working directory.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT"

if ! command -v dotnet >/dev/null 2>&1; then
	echo "error: 'dotnet' (the .NET SDK) is not on PATH — CSharpier runs as a dotnet tool." >&2
	echo "       install it from https://dotnet.microsoft.com/download and re-run." >&2
	exit 1
fi

# Restore the pinned CSharpier from .config/dotnet-tools.json (no-op once cached); keep it quiet unless
# it fails, in which case surface the full output.
if ! RESTORE_OUT="$(dotnet tool restore 2>&1)"; then
	echo "error: failed to restore the CSharpier dotnet tool:" >&2
	echo "$RESTORE_OUT" >&2
	exit 1
fi

# Parse arguments: --fix flips check -> format; --all checks everything; remaining args are explicit
# paths. With no paths and no --all we default to files changed vs master.
MODE="check"
ALL=0
PATHS=()
for arg in "$@"; do
	case "$arg" in
		--fix) MODE="format" ;;
		--all) ALL=1 ;;
		-h|--help) sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//; $d'; exit 0 ;;
		*) PATHS+=("$arg") ;;
	esac
done

# Drop vendored paths from an explicit/changed file list — CSharpier honours .csharpierignore when it
# traverses a directory, but not for files named explicitly on the command line.
ignored() { [[ "$1" == Assets/Plugins/* || "$1" == "Assets/TextMesh Pro/"* ]]; }

if [[ "$ALL" -eq 1 ]]; then
	TARGETS=("Assets")
elif [[ "${#PATHS[@]}" -gt 0 ]]; then
	TARGETS=()
	for p in "${PATHS[@]}"; do
		ignored "$p" || TARGETS+=("$p")
	done
else
	# Default: every .cs under Assets/ that differs from master — committed on this branch, staged,
	# unstaged, or untracked. Comparing the working tree to the merge-base catches all but untracked.
	BASE="$(git merge-base HEAD master 2>/dev/null || echo master)"
	TARGETS=()
	while IFS= read -r f; do
		[[ -n "$f" ]] || continue
		ignored "$f" && continue
		TARGETS+=("$f")
	done < <(
		{
			git diff --name-only --diff-filter=ACMR "$BASE" -- 'Assets/**/*.cs'
			git ls-files --others --exclude-standard -- 'Assets/**/*.cs'
		} | sort -u
	)
	if [[ "${#TARGETS[@]}" -eq 0 ]]; then
		echo "No changed C# files vs master — nothing to format-check. (Use --all to check everything.)"
		exit 0
	fi
	echo "Format-checking ${#TARGETS[@]} changed C# file(s) vs master ($BASE)..."
fi

if [[ "${#TARGETS[@]}" -eq 0 ]]; then
	echo "No C# files to check (all targets were vendored/ignored paths)."
	exit 0
fi

exec dotnet csharpier "$MODE" "${TARGETS[@]}"
