#!/usr/bin/env bash
#
# Regenerates Assets/docs/Behaviours.md and Assets/docs/Libraries.md headlessly by booting the
# Unity editor in batch mode and invoking Editor.DocsBatch.GenerateAll (the same code path as the
# "Assembler > Generate Behaviour/Library Docs" menu items).
#
# Usage: Assembler/Tools/generate-docs.sh
#
# Concurrency:
#  - This runs fine WHILE a normal editor is open, as long as that editor is on a DIFFERENT
#    project path (e.g. your main checkout). Unity shares the licence across instances via the
#    licensing daemon, and a git worktree is a separate path with its own Temp/UnityLockfile, so
#    there is no conflict. This is the whole point: generate a branch's docs in its worktree
#    without checking the branch out in your main repo.
#  - The ONLY hard conflict is pointing batch mode at the SAME path an editor already has open.
#    The guard below detects that and refuses to run rather than corrupt the project.
#
# First-run speed (Library seeding — OPT-IN):
#  - A fresh worktree has no Library/, so the first run does a full asset import. Measured here at
#    ~3 min, and it coexists fine with an open editor because import is CPU-bound with targeted
#    writes. This is the default and is the right choice while an editor is running.
#  - Set SEED_LIBRARY=1 to instead seed Library/ from the main worktree before running, via a
#    single recursive APFS clonefile() (copy-on-write: atomic, near-instant, no extra disk on the
#    same volume). Unity revalidates the cloned artifacts on launch, so a different branch just
#    reimports whatever actually changed. CAVEAT: only worthwhile when the editor is idle/closed —
#    the Library is 3GB / ~44k files, and while a live editor saturates the disk a bulk clone is
#    slower than the cold import it was meant to replace (it blocks on I/O contention). Seeding is
#    best-effort: any failure falls through to a normal import rather than aborting.
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
# Match only processes whose argv[0] IS the Unity binary (index==1) and whose args mention this
# project path; this ignores unrelated shells/greps that merely contain those strings as text.
if ps -axww -o command= | awk -v u="$UNITY" -v p="$PROJECT" 'index($0, u) == 1 && index($0, p)' | grep -q .; then
	echo "error: a Unity editor already has this project path open:" >&2
	echo "         $PROJECT" >&2
	echo "       Unity cannot open the same path from two processes. Close that editor and re-run." >&2
	echo "       (Running alongside an editor open on a DIFFERENT path — e.g. your main checkout — is fine.)" >&2
	exit 1
fi

# Opt-in Library/ seeding from the main worktree (see header). Off by default; best-effort.
if [[ "${SEED_LIBRARY:-0}" == "1" && ! -d "$PROJECT/Library" ]]; then
	# git-common-dir resolves to "<main-worktree>/.git" from any linked worktree; its parent is the
	# main worktree root. Map this worktree's Assembler/ to the same relative path under main.
	COMMON_GIT="$(git -C "$PROJECT" rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
	WT_ROOT="$(git -C "$PROJECT" rev-parse --show-toplevel 2>/dev/null || true)"
	if [[ -n "$COMMON_GIT" && -n "$WT_ROOT" ]]; then
		MAIN_ROOT="$(dirname "$COMMON_GIT")"
		REL="${PROJECT#"$WT_ROOT"/}"
		SRC_LIBRARY="$MAIN_ROOT/$REL/Library"
		# Skip if we ARE the main worktree (src == dest) or main has no Library to seed from.
		if [[ "$SRC_LIBRARY" != "$PROJECT/Library" && -d "$SRC_LIBRARY" ]]; then
			echo "Seeding Library/ from main worktree via recursive clonefile()..."
			# A single recursive clonefile() syscall clones the whole tree copy-on-write (instant
			# when the disk is quiet). Falls back to a per-file APFS clone, then a plain copy.
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

echo "Generating docs with Unity $VERSION (project: $PROJECT)..."
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.DocsBatch.GenerateAll \
	-logFile -
