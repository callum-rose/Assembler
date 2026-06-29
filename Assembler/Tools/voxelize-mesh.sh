#!/usr/bin/env bash
#
# Headless mesh -> VOX voxelizer: solid-fills a textured .obj/.fbx into a coloured MagicaVoxel .vox and
# runs the post-processing pipeline over it, WITHOUT opening the editor window. It boots Unity in batch
# mode and invokes Assembler.AssetGeneration.VoxelPipeline.MeshToVoxelsBatch.Run — the same VoxConversion.Run the
# "Window > Voxels > Mesh to Voxels" window drives — so an automated harness (or an AI) can produce
# a .vox and inspect the result. Exits non-zero on failure.
#
# Usage:
#   Assembler/Tools/voxelize-mesh.sh <mesh.obj|.fbx> [out.vox] [-- <extra batch flags>]
#
# Examples:
#   Assembler/Tools/voxelize-mesh.sh model.obj
#   Assembler/Tools/voxelize-mesh.sh model.fbx out/model.vox
#   Assembler/Tools/voxelize-mesh.sh model.obj -- -maxDim 48 -preset Prop -mirror true
#
# Batch flags (everything after `--`, forwarded verbatim to the editor command line):
#   -maxDim <int>            longest-axis voxel count (default 32)
#   -preset <name>           Creature | Prop | RawVoxelCleanup (default Creature)
#   -palettePath <Assets/…>  master-palette .asset for colour snapping (default: built-in starter palette)
#   -removeFloaters|-mirror|-revolve|-deLight|-snapToHistogramPeaks|-snapToPalette|-morphology  <true|false>  override a step
#   -histogramPeakVariety <float>  min Oklab distinctness a kept peak must add (primary control; default 0.10)
#   -histogramPeakCount <int>      safety cap on kept dominant colours when -snapToHistogramPeaks is on (default 8)
#
# Concurrency / cold-import caveats are identical to the other Tools/ scripts: runs alongside an editor on
# a DIFFERENT path, refuses if one already has THIS path open, and a fresh worktree's first run does a
# one-time cold import (~3 min) before it is fast.
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

# Refuse to run if an editor already has THIS exact project path open — two Unity processes on one path
# corrupt the Library. An editor on a different path (your main checkout) is fine and expected.
if ps -axww -o command= | awk -v u="$UNITY" -v p="$PROJECT" 'index($0, u) == 1 && index($0, p)' | grep -q .; then
	echo "error: a Unity editor already has this project path open:" >&2
	echo "         $PROJECT" >&2
	echo "       Unity cannot open the same path from two processes. Close that editor and re-run." >&2
	echo "       (Running alongside an editor open on a DIFFERENT path — e.g. your main checkout — is fine.)" >&2
	exit 1
fi

# Resolve a (possibly relative) path to absolute so it survives the editor's chdir to the project root.
abspath() {
	local p="$1"
	if [[ -e "$p" ]]; then
		echo "$(cd "$(dirname "$p")" && pwd)/$(basename "$p")"
	else
		# Output path may not exist yet: resolve its parent dir, keep the basename.
		local dir; dir="$(dirname "$p")"
		[[ -d "$dir" ]] && echo "$(cd "$dir" && pwd)/$(basename "$p")" || echo "$p"
	fi
}

if [[ $# -lt 1 ]]; then
	echo "usage: $(basename "$0") <mesh.obj|.fbx> [out.vox] [-- <extra batch flags>]" >&2
	exit 1
fi

MESH="$(abspath "$1")"; shift
BATCH_ARGS=(-meshPath "$MESH")

# Optional positional output path (anything before `--` that isn't a flag).
if [[ $# -gt 0 && "$1" != "--" && "$1" != -* ]]; then
	BATCH_ARGS+=(-voxPath "$(abspath "$1")")
	shift
fi

# Everything after `--` is forwarded verbatim, except path-bearing flags get absolutised.
if [[ $# -gt 0 && "$1" == "--" ]]; then
	shift
fi
while [[ $# -gt 0 ]]; do
	case "$1" in
		-voxPath|-palettePath|-meshPath)
			[[ $# -ge 2 ]] || { echo "error: $1 requires a value" >&2; exit 1; }
			BATCH_ARGS+=("$1" "$(abspath "$2")")
			shift 2
			;;
		*)
			BATCH_ARGS+=("$1")
			shift
			;;
	esac
done

LOG="$(mktemp -t assembler-voxelize.XXXXXX.log)"

echo "Voxelizing with Unity $VERSION (project: $PROJECT)..."
echo "  mesh: $MESH"

set +e
"$UNITY" \
	-batchmode -quit -nographics \
	-projectPath "$PROJECT" \
	-executeMethod Assembler.AssetGeneration.VoxelPipeline.MeshToVoxelsBatch.Run \
	"${BATCH_ARGS[@]}" \
	-logFile - > "$LOG" 2>&1
RC=$?
set -e

# Surface just the batch method's own lines from the (noisy) Unity log.
echo
grep -F "[MeshToVoxelsBatch]" "$LOG" || true
echo
echo "Full Unity log: $LOG"

exit "$RC"
