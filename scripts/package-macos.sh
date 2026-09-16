#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/VideoBatchProcessor.App/VideoBatchProcessor.App.csproj"
RID="${1:-osx-arm64}"
VERSION="${VBP_VERSION:-0.1.0}"
BUILD_NUMBER="${VBP_BUILD_NUMBER:-1}"
FFMPEG_DIR="${VBP_FFMPEG_DIR:-$ROOT/vendor/ffmpeg/$RID}"
OUTPUT_ROOT="$ROOT/artifacts/release/$VERSION/$RID"
PUBLISH_DIR="$OUTPUT_ROOT/publish"
APP_DIR="$OUTPUT_ROOT/Video Batch Processor.app"
ZIP_PATH="$OUTPUT_ROOT/VideoBatchProcessor-$VERSION-$RID.zip"

if [[ "$RID" != "osx-arm64" && "$RID" != "osx-x64" ]]; then
  echo "Uso: $0 [osx-arm64|osx-x64]" >&2
  exit 2
fi

for tool in ffmpeg ffprobe; do
  if [[ ! -x "$FFMPEG_DIR/$tool" ]]; then
    echo "Falta $FFMPEG_DIR/$tool" >&2
    echo "Coloca una distribución portable revisada de FFmpeg en esa carpeta." >&2
    exit 3
  fi
done

rm -rf "$OUTPUT_ROOT"
mkdir -p "$PUBLISH_DIR"

dotnet publish "$PROJECT" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:Version="$VERSION" \
  --output "$PUBLISH_DIR"

mkdir -p "$PUBLISH_DIR/tools"
cp "$FFMPEG_DIR/ffmpeg" "$PUBLISH_DIR/tools/ffmpeg"
cp "$FFMPEG_DIR/ffprobe" "$PUBLISH_DIR/tools/ffprobe"
chmod 755 "$PUBLISH_DIR/tools/ffmpeg" "$PUBLISH_DIR/tools/ffprobe"

mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
cp "$ROOT/src/VideoBatchProcessor.App/Assets/Packaging/VideoBatchProcessor.icns" \
  "$APP_DIR/Contents/Resources/VideoBatchProcessor.icns"
sed \
  -e "s/__VERSION__/$VERSION/g" \
  -e "s/__BUILD_NUMBER__/$BUILD_NUMBER/g" \
  "$ROOT/packaging/macos/Info.plist" > "$APP_DIR/Contents/Info.plist"

chmod 755 "$APP_DIR/Contents/MacOS/VideoBatchProcessor"
plutil -lint "$APP_DIR/Contents/Info.plist"

if [[ -n "${APPLE_SIGNING_IDENTITY:-}" ]]; then
  codesign --force --deep --options runtime --timestamp \
    --sign "$APPLE_SIGNING_IDENTITY" "$APP_DIR"
else
  codesign --force --deep --sign - "$APP_DIR"
  echo "Aviso: paquete firmado localmente. La distribución pública requiere Developer ID y notarización."
fi

codesign --verify --deep --strict "$APP_DIR"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIR" "$ZIP_PATH"

"$ROOT/scripts/verify-package.sh" "$APP_DIR" "$RID"
echo "Paquete creado: $ZIP_PATH"
