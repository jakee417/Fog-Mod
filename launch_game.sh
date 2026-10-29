#!/bin/bash
#
# launch_game.sh - launch Stardew Valley (SMAPI) from the CLI.
#
# Steam only runs one copy of the game, so use this to start extra
# instances for local multiplayer testing: launch instance 1 normally
# through Steam (Co-op -> Host, needs a cabin for player 2), then run
# this for instance 2 (Co-op -> Join LAN Game, leave the IP box empty).
#
# Examples:
#   ./launch_game.sh                        # 2nd instance, same Mods folder
#   ./launch_game.sh --mods-path Mods-P2    # 2nd instance, separate mods folder
#   ./launch_game.sh --proton "Proton 11.0" # override Proton version
#   ./launch_game.sh --dry-run              # print the command without running
#
# Env overrides: STEAM_ROOT, GAME_DIR, PROTON_VERSION.

set -u

STEAM_ROOT="${STEAM_ROOT:-$HOME/.local/share/Steam}"
GAME_DIR="${GAME_DIR:-$STEAM_ROOT/steamapps/common/Stardew Valley}"
APP_ID="413150"
PROTON_VERSION="${PROTON_VERSION:-Proton 10.0}"
MODS_PATH=""
DRY_RUN=0
BACKGROUND=0

usage() {
    sed -n '3,/^$/p' "$0" | sed 's/^# \?//'
    echo "Options:"
    echo "  --mods-path DIR     SMAPI mods folder (relative to game dir, or absolute)"
    echo "  --proton NAME       Proton build dir name (default: $PROTON_VERSION)"
    echo "  -b, --background    launch detached instead of in the foreground"
    echo "  --dry-run           print the launch command without running it"
    echo "  -h, --help          show this help"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --mods-path) MODS_PATH="${2:?--mods-path needs a value}"; shift 2 ;;
        --mods-path=*) MODS_PATH="${1#*=}"; shift ;;
        --proton) PROTON_VERSION="${2:?--proton needs a value}"; shift 2 ;;
        --proton=*) PROTON_VERSION="${1#*=}"; shift ;;
        -b | --background) BACKGROUND=1; shift ;;
        --dry-run) DRY_RUN=1; shift ;;
        -h | --help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 1 ;;
    esac
done

EXE="$GAME_DIR/Stardew Valley.exe"
PROTON="$STEAM_ROOT/steamapps/common/$PROTON_VERSION/proton"
PREFIX="$STEAM_ROOT/steamapps/compatdata/$APP_ID"

fail=0
[ -x "$PROTON" ] || { echo "Proton not found: $PROTON" >&2; fail=1; }
[ -f "$EXE" ] || { echo "Game exe not found: $EXE" >&2; fail=1; }
[ -d "$PREFIX" ] || { echo "Proton prefix not found: $PREFIX" >&2; fail=1; }
[ "$fail" -eq 0 ] || exit 1

if [ -n "$MODS_PATH" ]; then
    case "$MODS_PATH" in
        /*) MODS_DIR="$MODS_PATH" ;;
        *) MODS_DIR="$GAME_DIR/$MODS_PATH" ;;
    esac
    [ -d "$MODS_DIR" ] || echo "Note: mods folder does not exist yet: $MODS_DIR" >&2
fi

pgrep -x steam >/dev/null 2>&1 || echo "Note: Steam client does not appear to be running." >&2

export STEAM_COMPAT_DATA_PATH="$PREFIX"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAM_ROOT"
export STEAM_COMPAT_APP_ID="$APP_ID"

echo "Game:   $EXE"
echo "Proton: $PROTON_VERSION"
[ -n "$MODS_PATH" ] && echo "Mods:   $MODS_PATH"
echo "Logs:   $PREFIX/pfx/drive_c/users/steamuser/AppData/Roaming/StardewValley/ErrorLogs/"

if [ "$DRY_RUN" -eq 1 ]; then
    echo "+ cd \"$GAME_DIR\""
    if [ -n "$MODS_PATH" ]; then
        echo "+ \"$PROTON\" run \"$EXE\" --mods-path \"$MODS_PATH\""
    else
        echo "+ \"$PROTON\" run \"$EXE\""
    fi
    exit 0
fi

cd "$GAME_DIR" || exit 1
if [ "$BACKGROUND" -eq 1 ]; then
    LOG=/tmp/stardew-cli.log
    if [ -n "$MODS_PATH" ]; then
        "$PROTON" run "$EXE" --mods-path "$MODS_PATH" >"$LOG" 2>&1 &
    else
        "$PROTON" run "$EXE" >"$LOG" 2>&1 &
    fi
    echo "Launched in background (pid $!, output: $LOG)"
else
    if [ -n "$MODS_PATH" ]; then
        exec "$PROTON" run "$EXE" --mods-path "$MODS_PATH"
    else
        exec "$PROTON" run "$EXE"
    fi
fi
