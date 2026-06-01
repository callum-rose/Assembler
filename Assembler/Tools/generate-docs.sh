#!/usr/bin/env bash
#
# Regenerates Assets/docs/Behaviours.md and Assets/docs/Libraries.md headlessly by booting the
# Unity editor in batch mode and invoking Editor.DocsBatch.GenerateAll (the same code path as the
# "Assembler > Generate Behaviour/Library Docs" menu items).
#
# Usage: Assembler/Tools/generate-docs.sh
#
# Notes:
#  - The first run in a fresh worktree triggers a full asset import and is slow (minutes).
#    Subsequent runs reuse the Library/ cache and are fast.
#  - Unity cannot open the same project path from two processes at once; close any editor already
#    open on this worktree before running.
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

# csc's -doc flag writes the XML doc files here; ensure the dir exists (it's normally committed,
# but be defensive in case it was cleaned).
mkdir -p "$PROJECT/DocGen"

echo "Generating docs with Unity $VERSION (project: $PROJECT)..."
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.DocsBatch.GenerateAll \
	-logFile -
