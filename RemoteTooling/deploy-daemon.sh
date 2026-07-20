#!/usr/bin/env bash
#
# deploy-daemon.sh — build the Release binary and (re)install the generation
# daemon as a macOS LaunchAgent. This automates the manual "Background
# (recommended)" steps in README.md: dotnet build → write the plist with real
# paths → launchctl reload. Safe to re-run; it always rebuilds and reloads.
#
# Usage:   ./deploy-daemon.sh
# From Rider: run the "Deploy Generation Daemon" run configuration.
#
# Config (all optional — auto-detected if unset). Override in the environment
# or in the Rider run configuration's "Environment variables":
#   ASSEMBLER_STORE_REPO    e.g. yourname/assembler-games  (default: <gh login>/assembler-games)
#   ASSEMBLER_STORE_DIR     default: ~/Developer/assembler-games
#   ASSEMBLER_ENGINE_DIR    default: <repo>/Assembler
#   ASSEMBLER_POLL_SECONDS  default: 30
#
set -euo pipefail

if [[ "$(uname)" != "Darwin" ]]; then
  echo "error: LaunchAgents are macOS-only; this script must run on a Mac." >&2
  exit 1
fi

# --- Resolve repo layout from this script's own location -------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"   # .../RemoteTooling
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CSPROJ="$SCRIPT_DIR/Assembler.RemoteTooling.csproj"
BIN="$SCRIPT_DIR/bin/Release/net10.0/assembler-remote"

LABEL="com.assembler.generation-daemon"
DEST="$HOME/Library/LaunchAgents/$LABEL.plist"
LOG_OUT="$HOME/Library/Logs/assembler-generation-daemon.out.log"
LOG_ERR="$HOME/Library/Logs/assembler-generation-daemon.err.log"

# --- Config (env overrides, else sensible defaults) ------------------------
STORE_DIR="${ASSEMBLER_STORE_DIR:-$HOME/Developer/assembler-games}"
ENGINE_DIR="${ASSEMBLER_ENGINE_DIR:-$REPO_ROOT/Assembler}"
POLL="${ASSEMBLER_POLL_SECONDS:-30}"

STORE_REPO="${ASSEMBLER_STORE_REPO:-}"
if [[ -z "$STORE_REPO" ]]; then
  if login="$(gh api user -q .login 2>/dev/null)" && [[ -n "$login" ]]; then
    STORE_REPO="$login/assembler-games"
  else
    echo "error: ASSEMBLER_STORE_REPO is unset and could not be derived from 'gh api user'." >&2
    echo "       Set it, e.g.:  ASSEMBLER_STORE_REPO=yourname/assembler-games $0" >&2
    exit 1
  fi
fi

# PATH the daemon runs with — must reach claude, gh, git, dotnet, Unity.
DAEMON_PATH="/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:/usr/bin:/bin:$HOME/.claude/local"

# --- Build -----------------------------------------------------------------
echo "==> Building Release: $CSPROJ"
dotnet build -c Release "$CSPROJ"

if [[ ! -x "$BIN" ]]; then
  echo "error: expected launcher not found or not executable: $BIN" >&2
  exit 1
fi

# --- Write the LaunchAgent plist -------------------------------------------
echo "==> Writing $DEST"
mkdir -p "$HOME/Library/LaunchAgents" "$HOME/Library/Logs"

# Emit via PlistBuddy-free heredoc; values are all path/string literals with no
# XML-special characters, so no escaping is needed here.
cat > "$DEST" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>Label</key>
	<string>$LABEL</string>

	<key>ProgramArguments</key>
	<array>
		<string>$BIN</string>
		<string>daemon</string>
	</array>

	<key>EnvironmentVariables</key>
	<dict>
		<key>ASSEMBLER_STORE_REPO</key>
		<string>$STORE_REPO</string>
		<key>ASSEMBLER_STORE_DIR</key>
		<string>$STORE_DIR</string>
		<key>ASSEMBLER_ENGINE_DIR</key>
		<string>$ENGINE_DIR</string>
		<key>ASSEMBLER_POLL_SECONDS</key>
		<string>$POLL</string>
		<key>PATH</key>
		<string>$DAEMON_PATH</string>
	</dict>

	<key>RunAtLoad</key>
	<true/>
	<key>KeepAlive</key>
	<true/>

	<key>StandardOutPath</key>
	<string>$LOG_OUT</string>
	<key>StandardErrorPath</key>
	<string>$LOG_ERR</string>
</dict>
</plist>
PLIST

# --- Reload launchd --------------------------------------------------------
echo "==> Reloading launchd ($LABEL)"
launchctl unload -w "$DEST" 2>/dev/null || true
launchctl load -w "$DEST"

echo
echo "Deployed. Daemon binary:  $BIN"
echo "  store repo:  $STORE_REPO"
echo "  store dir:   $STORE_DIR"
echo "  engine dir:  $ENGINE_DIR"
echo "  logs:        $LOG_OUT"
echo "               $LOG_ERR"
echo
echo "Status:  launchctl list | grep $LABEL"
echo "Stop:    launchctl unload -w $DEST"
