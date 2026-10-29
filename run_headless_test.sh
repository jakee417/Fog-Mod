#!/bin/bash
#
# run_headless_test.sh - boot the game headless (Xvfb + Proton), auto-load a
# test save, run the in-mod critterz scenario, report PASS/FAIL.
#
# The harness kills the process without saving, so the save is never mutated.
# Requires a built mod installed in Mods/ (run ./build_debug.sh first), unless
# HEADLESS_MODS_PATH points at a staged mods folder instead.
#
# Examples:
#   ./run_headless_test.sh                     # default save, 7 min timeout
#   HEADLESS_SAVE=pickles_434696114 ./run_headless_test.sh
#   HEADLESS_TIMEOUT=600 ./run_headless_test.sh
#   ./run_headless_test.sh --watch             # visible window, watch it play
#   ./run_headless_test.sh --record            # headless, but save the run as video
#   ./run_headless_test.sh --record /path/out.mp4
#
# Env overrides: STEAM_ROOT, GAME_DIR, PROTON_VERSION, PROTON_EXE (full path
# to a proton script, wins over PROTON_VERSION), HEADLESS_PREFIX,
# HEADLESS_MODS_PATH (Windows-style, e.g. 'Z:\tmp\headless-mods'),
# HEADLESS_SAVE, HEADLESS_TIMEOUT (seconds), HEADLESS_RESULTS_LIN (results
# file, Linux path), HEADLESS_WATCH=1 (same as --watch),
# HEADLESS_RECORD=/path/out.mp4 (same as --record),
# HEADLESS_XVFB_DISPLAY=:99 (display used for a recorded run).

set -u

STEAM_ROOT="${STEAM_ROOT:-$HOME/.local/share/Steam}"
GAME_DIR="${GAME_DIR:-$STEAM_ROOT/steamapps/common/Stardew Valley}"
APP_ID="413150"
PROTON_VERSION="${PROTON_VERSION:-Proton 10.0}"
PREFIX="${HEADLESS_PREFIX:-$STEAM_ROOT/steamapps/compatdata/$APP_ID}"
MODS_PATH="${HEADLESS_MODS_PATH:-}"
SAVE="${HEADLESS_SAVE:-Tims_452916884}"
TIMEOUT="${HEADLESS_TIMEOUT:-420}"
RESULTS_LIN="${HEADLESS_RESULTS_LIN:-/tmp/fogmod-test-results.txt}"
RESULTS_WIN="Z:$(echo "$RESULTS_LIN" | sed 's|/|\\|g')"
WATCH="${HEADLESS_WATCH:-0}"
[ "${1:-}" = "--watch" ] && WATCH=1
RECORD="${HEADLESS_RECORD:-}"
if [ "${1:-}" = "--record" ]; then
    RECORD="${2:-/tmp/fogmod-test-recording.mp4}"
fi

PROTON="${PROTON_EXE:-$STEAM_ROOT/steamapps/common/$PROTON_VERSION/proton}"
EXE="$GAME_DIR/Stardew Valley.exe"
SMAPI_LOG="$PREFIX/pfx/drive_c/users/steamuser/AppData/Roaming/StardewValley/ErrorLogs/SMAPI-latest.txt"

fail=0
[ -x "$PROTON" ] || { echo "Proton not found: $PROTON" >&2; fail=1; }
[ -f "$EXE" ] || { echo "Game exe not found: $EXE" >&2; fail=1; }
[ -d "$PREFIX" ] || { echo "Proton prefix not found: $PREFIX" >&2; fail=1; }
command -v xvfb-run >/dev/null 2>&1 || { echo "xvfb-run not found (install Xvfb)" >&2; fail=1; }
[ "$fail" -eq 0 ] || exit 1

rm -f "$RESULTS_LIN"

export STEAM_COMPAT_DATA_PATH="$PREFIX"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAM_ROOT"
export STEAM_COMPAT_APP_ID="$APP_ID"
export SDL_AUDIODRIVER=dummy
export FOGMOD_TEST=1
export FOGMOD_TEST_SAVE="$SAVE"
export FOGMOD_TEST_RESULTS="$RESULTS_WIN"

echo "Save:    $SAVE"
echo "Proton:  $PROTON_VERSION"
echo "Prefix:  $PREFIX"
[ -n "$MODS_PATH" ] && echo "Mods:    $MODS_PATH"
echo "Timeout: ${TIMEOUT}s"
echo "---"

cd "$GAME_DIR" || exit 1
if [ -n "$MODS_PATH" ]; then
    set -- "$PROTON" run "$EXE" --mods-path "$MODS_PATH"
else
    set -- "$PROTON" run "$EXE"
fi
if [ "$WATCH" = "1" ]; then
    echo "(visible window - sit back)"
    timeout "$TIMEOUT" "$@" > /tmp/fogmod-headless-console.log 2>&1
    game_exit=$?
elif [ -n "$RECORD" ]; then
    # Recorded headless run: xvfb-run picks a display we cannot address,
    # so start our own Xvfb on a fixed display and point ffmpeg at it.
    XDISP="${HEADLESS_XVFB_DISPLAY:-:99}"
    XSOCK="/tmp/.X${XDISP#:}-unix/X${XDISP#:}"
    XLOCK="/tmp/.X${XDISP#:}-lock"
    if [ -S "$XSOCK" ]; then
        echo "Display $XDISP is in use; set HEADLESS_XVFB_DISPLAY to a free one." >&2
        exit 1
    fi
    rm -f "$XLOCK"  # stale lock with no server behind it
    echo "Recording to $RECORD (display $XDISP)"
    Xvfb "$XDISP" -screen 0 1280x720x24 > /tmp/fogmod-xvfb.log 2>&1 &
    XVFB_PID=$!
    for _ in $(seq 1 100); do
        [ -S "$XSOCK" ] && break
        sleep 0.1
    done
    ffmpeg -f x11grab -video_size 1280x720 -framerate 30 -i "$XDISP" \
        -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p -an -y \
        "$RECORD" > /tmp/fogmod-ffmpeg.log 2>&1 &
    FF_PID=$!
    trap 'kill "$FF_PID" "$XVFB_PID" 2>/dev/null' EXIT
    sleep 1
    if ! kill -0 "$FF_PID" 2>/dev/null; then
        echo "ffmpeg failed to start; see /tmp/fogmod-ffmpeg.log:" >&2
        tail -10 /tmp/fogmod-ffmpeg.log >&2
        kill "$XVFB_PID" 2>/dev/null
        exit 1
    fi
    DISPLAY="$XDISP" timeout "$TIMEOUT" "$@" > /tmp/fogmod-headless-console.log 2>&1
    game_exit=$?
    kill "$FF_PID" 2>/dev/null; wait "$FF_PID" 2>/dev/null
    kill "$XVFB_PID" 2>/dev/null; wait "$XVFB_PID" 2>/dev/null
    if [ -f "$RECORD" ]; then
        echo "Video: $RECORD ($(du -h "$RECORD" | cut -f1))"
    else
        echo "WARNING: no video at $RECORD (see /tmp/fogmod-ffmpeg.log)"
    fi
else
    timeout "$TIMEOUT" xvfb-run -a -s "-screen 0 1280x720x24" \
        "$@" > /tmp/fogmod-headless-console.log 2>&1
    game_exit=$?
fi

echo "---"
if [ ! -f "$RESULTS_LIN" ]; then
    if [ "$game_exit" -eq 124 ]; then
        echo "TIMEOUT after ${TIMEOUT}s with no results file."
    else
        echo "NO RESULTS FILE (game exit=$game_exit)."
    fi
    echo "SMAPI log tail:"
    tail -30 "$SMAPI_LOG" 2>/dev/null || echo "(no SMAPI log at $SMAPI_LOG)"
    exit 1
fi

tr -d '\r' < "$RESULTS_LIN"
# Note: the file has Windows line endings; strip CR before parsing.
result=$(tail -1 "$RESULTS_LIN" | tr -d '\r')
fails=$(echo "$result" | sed -n 's/^RESULT: PASS [0-9][0-9]* FAIL //p')
if [ "$fails" = "0" ]; then
    echo "HEADLESS TEST PASSED"
    exit 0
fi
echo "HEADLESS TEST FAILED"
echo "SMAPI errors:"
grep -a "TEST FAIL" "$SMAPI_LOG" 2>/dev/null | head -20
exit 1
