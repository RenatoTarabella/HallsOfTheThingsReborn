#!/bin/sh
# Builds riscos/!Halls from src/halls.bas: the character set from the 1983
# source and the tokenised BASIC (TEXTLOAD + SAVE in ArchieEmu's BBC BASIC
# machine). Usage: sh tools/build.sh [path to armbasic]
set -e
cd "$(dirname "$0")/.."
B=${1:-../ARM/build/Release/armbasic}
A="riscos/!Halls"
python tools/mkchars.py "$A/Chars,ffd" >/dev/null
cp src/halls.bas "$A/hallstxt"
printf 'TEXTLOAD "hallstxt"\nSAVE "!RunImage"\n' | "$B" --disc "$A" > /tmp/halls_build.txt 2>&1 || true
rm -f "$A/hallstxt"
if grep -qi "error\|mistake\|not found" /tmp/halls_build.txt; then cat /tmp/halls_build.txt; exit 1; fi
ls -l "$A"
