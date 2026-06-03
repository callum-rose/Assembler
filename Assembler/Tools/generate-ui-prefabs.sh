#!/usr/bin/env bash
#
# Generates the baseline UI prefabs (button, label, slider) and the UiPrefabLibrary asset under
# Assets/Resources/UI/ headlessly, by booting the Unity editor in batch mode and invoking
# Editor.UiPrefabGenerator.GenerateBatch (the same code path as the
# "Assembler > UI > Generate UI Prefabs" menu item).
#
# Usage: Assembler/Tools/generate-ui-prefabs.sh
#
# Prerequisite: TextMeshPro "Essentials" must be imported once (Window > TextMeshPro > Import TMP
# Essential Resources) so generated text has a default font. The generator logs a warning if missing.
#
# Concurrency: like generate-docs.sh, this runs fine alongside an editor open on a DIFFERENT project
# path, but refuses to run if an editor already has THIS path open (two Unity processes on one path
# corrupt the Library).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION_FILE="$PROJECT/ProjectSettings/ProjectVersion.txt"
if [[ ! -f "$VERSION_FILE" ]]; then
	echo "error: $VERSION_FILE not found — is this an Assembler Unity project?" >&2
	exit 1
fi

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

if ps -axww -o command= | awk -v u="$UNITY" -v p="$PROJECT" 'index($0, u) == 1 && index($0, p)' | grep -q .; then
	echo "error: a Unity editor already has this project path open:" >&2
	echo "         $PROJECT" >&2
	echo "       Unity cannot open the same path from two processes. Close that editor and re-run." >&2
	exit 1
fi

echo "Generating UI prefabs with Unity $VERSION (project: $PROJECT)..."
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Editor.UiPrefabGenerator.GenerateBatch \
	-logFile -
