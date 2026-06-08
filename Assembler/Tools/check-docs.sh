#!/usr/bin/env bash
#
# Doc-drift guard: verifies the committed Assets/docs/Behaviours.md and Assets/docs/Libraries.md are
# up to date with the code. It boots the Unity editor in batch mode and invokes the SAME generator as
# generate-docs.sh (Editor.DocsBatch.GenerateAll) but with "-outputDir <scratch>", so the freshly
# generated markdown lands in a throwaway temp directory instead of overwriting the committed files.
# It then diffs the regenerated output against the committed copies and exits non-zero (printing the
# diff) if they differ — making it a CI / pre-commit guard, and letting Claude verify it kept the docs
# in sync after adding or changing a behaviour or library.
#
# Usage: Assembler/Tools/check-docs.sh
#
# Exit codes:
#   0 — committed docs match freshly generated output (in sync)
#   1 — docs are stale (drift detected; the diff is printed) OR a setup/generation error occurred
#
# To fix a reported drift, run Tools/generate-docs.sh and commit the updated Assets/docs/*.md.
#
# Concurrency / first-run speed: identical to generate-docs.sh — it runs fine alongside an editor open
# on a DIFFERENT path, refuses if an editor already has THIS path open, and the first run in a fresh
# worktree does a one-time cold import (~3 min). Set SEED_LIBRARY=1 to clone the main worktree's
# Library/ first (only faster when the editor is idle — see generate-docs.sh's header for the caveats).
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

# Opt-in Library/ seeding from the main worktree (see generate-docs.sh's header). Off by default;
# best-effort — any failure falls through to a normal cold import rather than aborting.
if [[ "${SEED_LIBRARY:-0}" == "1" && ! -d "$PROJECT/Library" ]]; then
	COMMON_GIT="$(git -C "$PROJECT" rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
	WT_ROOT="$(git -C "$PROJECT" rev-parse --show-toplevel 2>/dev/null || true)"
	if [[ -n "$COMMON_GIT" && -n "$WT_ROOT" ]]; then
		MAIN_ROOT="$(dirname "$COMMON_GIT")"
		REL="${PROJECT#"$WT_ROOT"/}"
		SRC_LIBRARY="$MAIN_ROOT/$REL/Library"
		if [[ "$SRC_LIBRARY" != "$PROJECT/Library" && -d "$SRC_LIBRARY" ]]; then
			echo "Seeding Library/ from main worktree via recursive clonefile()..."
			if ! /usr/bin/python3 - "$SRC_LIBRARY" "$PROJECT/Library" <<-'PY'
				import ctypes, os, sys
				libc = ctypes.CDLL("libc.dylib", use_errno=True)
				rc = libc.clonefile(sys.argv[1].encode(), sys.argv[2].encode(), 0)
				if rc != 0:
				    e = ctypes.get_errno(); sys.exit("clonefile: %s" % os.strerror(e))
			PY
			then
				echo "  clonefile() unavailable/failed; falling back to a file copy..." >&2
				rm -rf "$PROJECT/Library"
				cp -Rc "$SRC_LIBRARY" "$PROJECT/Library" 2>/dev/null \
					|| { rm -rf "$PROJECT/Library"; cp -R "$SRC_LIBRARY" "$PROJECT/Library" || rm -rf "$PROJECT/Library"; }
			fi
		fi
	fi
fi

# csc's -doc flag writes the XML doc files here; ensure the dir exists (it's normally committed,
# but be defensive in case it was cleaned).
mkdir -p "$PROJECT/DocGen"

# Scratch dir for the freshly generated docs; removed on exit however the script ends.
SCRATCH="$(mktemp -d "${TMPDIR:-/tmp}/assembler-check-docs.XXXXXX")"
trap 'rm -rf "$SCRATCH"' EXIT

echo "Regenerating docs with Unity $VERSION into a scratch dir (project: $PROJECT)..."
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.DocsBatch.GenerateAll \
	-outputDir "$SCRATCH" \
	-logFile -

# Diff each freshly generated file against its committed copy. A missing committed file (never
# generated) counts as drift too. Collect all diffs so the report shows everything at once.
DOCS_DIR="$PROJECT/Assets/docs"
STALE=0
for name in Behaviours.md Libraries.md; do
	fresh="$SCRATCH/$name"
	committed="$DOCS_DIR/$name"
	if [[ ! -f "$fresh" ]]; then
		echo "error: generator did not produce $name in the scratch dir." >&2
		STALE=1
		continue
	fi
	if ! diff -u "$committed" "$fresh" \
		--label "committed/$name" --label "generated/$name"; then
		STALE=1
	fi
done

if [[ "$STALE" -ne 0 ]]; then
	echo "" >&2
	echo "Doc drift detected: committed Assets/docs/*.md is out of date." >&2
	echo "Run Tools/generate-docs.sh and commit the updated docs." >&2
	exit 1
fi

echo "Docs are in sync: committed Assets/docs/Behaviours.md and Libraries.md match generated output."
