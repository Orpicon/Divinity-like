#!/usr/bin/env bash
set -euo pipefail

UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe}"
PROJECT="$(cd "$(dirname "$0")" && pwd)"
RESULTS="$PROJECT/TestResults"

mkdir -p "$RESULTS"
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode \
    -testResults "$RESULTS/editmode.xml" -logFile "$RESULTS/editmode.log" || true

summary=$(grep -o -E '<test-run [^>]*' "$RESULTS/editmode.xml" | grep -o -E 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"')
echo "$summary"
grep -E '<test-case .*result="Failed"' "$RESULTS/editmode.xml" | grep -o 'fullname="[^"]*"' || true
[[ "$summary" == *'failed="0"'* ]]
