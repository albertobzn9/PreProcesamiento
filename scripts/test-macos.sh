#!/usr/bin/env zsh
set -euo pipefail

repo_root=$(cd -- "$(dirname -- "$0")/.." && pwd)
test_project="$repo_root/tests/VideoBatchProcessor.Tests/VideoBatchProcessor.Tests.csproj"

arch=$(uname -m)
case "$arch" in
  arm64)
    runtime_pattern='*/runtimes/osx-arm64/native'
    ;;
  x86_64)
    runtime_pattern='*/runtimes/osx*/native'
    ;;
  *)
    echo "Unsupported macOS architecture: $arch" >&2
    exit 1
    ;;
esac

dotnet build "$test_project" >/dev/null

native_dir=$(find "$repo_root/tests/VideoBatchProcessor.Tests/bin" \
  -path "$runtime_pattern" \
  -type d \
  | sort \
  | tail -n 1)

if [[ -z "${native_dir:-}" ]]; then
  echo "Could not locate OpenCV native runtime directory for macOS ($arch)." >&2
  exit 1
fi

export DYLD_FALLBACK_LIBRARY_PATH="$native_dir"
dotnet test "$test_project" "$@"
