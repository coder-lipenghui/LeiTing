#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LUBAN_DLL="$SCRIPT_DIR/Luban/Luban.dll"
CONF_ROOT="$SCRIPT_DIR"
OUTPUT_DIR="$PROJECT_ROOT/Assets/Resources/Luban"
CODE_DIR="$PROJECT_ROOT/Assets/Scripts/Config/LubanGenerated"

dotnet "$LUBAN_DLL" \
    -t all \
    -c cs-simple-json \
    -d json \
    --conf "$CONF_ROOT/luban.conf" \
    -x outputCodeDir="$CODE_DIR" \
    -x outputDataDir="$OUTPUT_DIR"
