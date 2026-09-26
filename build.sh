#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
BUILD="${PKHEX_BUILD_DIR:-$ROOT/.build}"
DOTNET_BIN="${DOTNET:-dotnet}"
ARCH="${KEEPSAKE_ARCH:-$(uname -m)}"
case "$ARCH" in
  arm64) RUNTIME="osx-arm64" ;;
  x86_64) RUNTIME="osx-x64" ;;
  *) echo "KEEPSAKE_ARCH must be arm64 or x86_64" >&2; exit 1 ;;
esac
BUILD="$BUILD/$ARCH"
APP="${KEEPSAKE_APP_PATH:-$ROOT/KeepSake.app}"
mkdir -p "$BUILD" "$APP/Contents/MacOS" "$APP/Contents/Resources"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_CLI_HOME="$BUILD/dotnet-home"
export CLANG_MODULE_CACHE_PATH="$BUILD/clang-module-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$BUILD/swift-module-cache"
"$DOTNET_BIN" publish "$ROOT/Source/Engine" -c Release -r "$RUNTIME" --self-contained true \
  -m:1 -p:UseSharedCompilation=false -nodeReuse:false \
  -p:SourceRevisionId=08c27668d28a83ad4b04140436a384d4155ed134 \
  --artifacts-path "$BUILD/dotnet-artifacts" -o "$APP/Contents/Helpers" --nologo
cp "$ROOT/Source/Packaging/RuntimeNotices/"* "$APP/Contents/Helpers/"
swift build --package-path "$ROOT/Source/Native" --scratch-path "$BUILD/swift" --triple "$ARCH-apple-macosx14.0" -c release --disable-sandbox --build-system "${PKHEX_SWIFT_BUILD_SYSTEM:-native}"
cp "$BUILD/swift/release/PKHeXSwift" "$APP/Contents/MacOS/PKHeXSwift"
cp "$ROOT/Source/Packaging/Info.plist" "$APP/Contents/Info.plist"
cp "$ROOT/Source/Assets/AppIcon.icns" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Badges" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Donuts" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Sprites" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/GamePortraits" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Portraits" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/PaldeaItems" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/HisuiItems" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Items" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Balls" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/MoveTypes" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/GameLogos" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Ribbons" "$APP/Contents/Resources/"
cp -R "$ROOT/Source/Assets/Wallpapers" "$APP/Contents/Resources/"
cp "$ROOT/LICENSE" "$ROOT/THIRD-PARTY-NOTICES.md" "$APP/Contents/Resources/"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
printf '\nBuilt %s\n' "$APP"
