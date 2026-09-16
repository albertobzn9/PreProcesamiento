#!/usr/bin/env bash
set -euo pipefail

PACKAGE_PATH="${1:?Indica la ruta del paquete a verificar.}"
RID="${2:?Indica el RID del paquete.}"

if [[ "$RID" == osx-* ]]; then
  BASE="$PACKAGE_PATH/Contents/MacOS"
  REQUIRED=(
    "$PACKAGE_PATH/Contents/Info.plist"
    "$PACKAGE_PATH/Contents/Resources/VideoBatchProcessor.icns"
    "$BASE/VideoBatchProcessor"
    "$BASE/WebUi/index.html"
    "$BASE/tools/ffmpeg"
    "$BASE/tools/ffprobe"
  )
else
  BASE="$PACKAGE_PATH"
  REQUIRED=(
    "$BASE/VideoBatchProcessor.exe"
    "$BASE/WebUi/index.html"
    "$BASE/tools/ffmpeg.exe"
    "$BASE/tools/ffprobe.exe"
  )
fi

for path in "${REQUIRED[@]}"; do
  if [[ ! -e "$path" ]]; then
    echo "Paquete incompleto: falta $path" >&2
    exit 1
  fi
done

echo "Estructura verificada para $RID."
