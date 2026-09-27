#!/usr/bin/env bash
# Build this complete Godot source tree with the same Android preset as CI.
set -euo pipefail
GODOT_BIN="${LORDWAR_GODOT_BIN:-godot}"
VERSION_CODE="$(sed -n -E 's/.*VersionCode=([0-9]+).*/\1/p' Scripts/LordWar/Core/BuildInfo.cs | head -1)"
if [[ ! "$VERSION_CODE" =~ ^[0-9]+$ ]]; then echo 'Missing BuildInfo.VersionCode' >&2; exit 1; fi
OUT_DIR="${LORDWAR_APK_OUTPUT_DIR:-Builds/Android/n01-${VERSION_CODE}-$(date -u +%Y%m%dT%H%M%SZ)}"
mkdir -p "$OUT_DIR" Evidence
OUT_FILE="$OUT_DIR/LordWar-N01-${VERSION_CODE}.apk"
rm -f "$OUT_FILE"
dotnet restore LordWar.csproj 2>&1 | tee Evidence/N01_SOURCE_RESTORE.log
dotnet build LordWar.csproj -c Debug --no-restore 2>&1 | tee Evidence/N01_SOURCE_BUILD.log
"$GODOT_BIN" --headless --editor --quit --path . 2>&1 | tee Evidence/N01_SOURCE_IMPORT.log
"$GODOT_BIN" --headless --path . --export-release "Android Debug" "$OUT_FILE" --verbose 2>&1 | tee Evidence/N01_SOURCE_EXPORT.log
test -s "$OUT_FILE"
sha256sum "$OUT_FILE" | tee "$OUT_FILE.sha256"
echo "SOURCE_APK_BUILT=$OUT_FILE"
