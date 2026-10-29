#!/bin/bash
#
# run_unit_tests.sh - run the FogMod.Tests xUnit suite (one Tests/ dir per
# source area) with Cobertura line/branch coverage of the FogMod assembly.
#
# Usage:
#   ./run_unit_tests.sh              # full suite + coverage summary
#   ./run_unit_tests.sh -- --filter 'FullyQualifiedName~Utilities'
#
# Game-coupled code (anything touching Game1) can't run under dotnet test;
# that stays covered by the headless in-game harness (run_headless_test.sh).

set -u

cd "$(dirname "$0")" || exit 1

# Pick up a user-local .NET SDK install if dotnet isn't on PATH
if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
    export PATH="$HOME/.dotnet:$PATH"
fi

rm -rf FogMod/Tests/TestResults

dotnet test FogMod/Tests/FogMod.Tests.csproj -c Debug \
    --settings FogMod/Tests/Coverage.runsettings \
    --collect:"XPlat Code Coverage" "$@" || exit 1

python3 - <<'EOF'
import glob, os, xml.etree.ElementTree as ET

files = sorted(glob.glob("FogMod/Tests/TestResults/*/coverage.cobertura.xml"),
               key=os.path.getmtime)
if not files:
    print("(no coverage file found)")
    raise SystemExit
root = ET.parse(files[-1]).getroot()
lr = root.get("line-rate", "0"); br = root.get("branch-rate", "0")
print(f"TOTAL line {float(lr)*100:.1f}%  branch {float(br)*100:.1f}%")
print("--- covered files ---")
agg = {}
for cls in root.iter("class"):
    name = cls.get("filename", "?")
    lines = cls.find("lines")
    if lines is None:
        continue
    valid = len(lines); hit = sum(1 for ln in lines if int(ln.get("hits", "0")) > 0)
    v, h = agg.get(name, (0, 0))
    agg[name] = (v + valid, h + hit)
shown = 0
for name in sorted(agg):
    valid, hit = agg[name]
    if valid and hit:
        print(f"{hit/valid*100:5.1f}%  {hit:4d}/{valid:<4d}  {name}")
        shown += 1
print(f"({shown} files with hits; run_headless_test.sh covers the game-coupled rest)")
EOF
