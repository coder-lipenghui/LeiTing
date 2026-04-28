#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "${LUBAN_DLL:-}" ]]; then
  echo "Please set LUBAN_DLL to your Luban.ClientServer.dll path."
  echo "Example: LUBAN_DLL=/path/to/Luban.ClientServer.dll bash Luban/gen_client_json.sh"
  exit 1
fi

dotnet "$LUBAN_DLL" -j cfg -- \
  -d "$ROOT_DIR/Luban/Defines/__root__.xml" \
  --input_data_dir "$ROOT_DIR/Luban/Datas" \
  --output_data_dir "$ROOT_DIR/Assets/Resources/Luban" \
  --gen_types data_json \
  --data_file_extension json \
  -s client
