#!/bin/bash
#
# watch_test.sh - build the mod and play the test scenario in a visible
# window on the test farm. The scenario exits on its own without saving.
#
# Usage:
#   ./watch_test.sh                  # Tim's farm (default)
#   ./watch_test.sh pickles_434696114  # a different save
#
# Results go to /tmp/fogmod-test-results.txt. HEADLESS_TIMEOUT and other
# run_headless_test.sh env overrides pass through untouched.

set -u

cd "$(dirname "$0")" || exit 1

./build_debug.sh || exit 1

if [ $# -gt 0 ]; then
    HEADLESS_SAVE="$1" ./run_headless_test.sh --watch
else
    ./run_headless_test.sh --watch
fi
