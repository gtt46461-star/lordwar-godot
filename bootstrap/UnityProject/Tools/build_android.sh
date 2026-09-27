#!/usr/bin/env bash
set -euo pipefail
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
LOG="$PROJECT/Evidence/android_build_attempt.log"
mkdir -p "$PROJECT/Evidence" "$PROJECT/Builds/Android"
find_unity(){
  if [[ -n "${UNITY_EDITOR:-}" && -x "${UNITY_EDITOR}" ]]; then printf '%s\n' "$UNITY_EDITOR"; return 0; fi
  for c in unity-editor Unity unity /opt/Unity/Editor/Unity /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity; do
    if command -v "$c" >/dev/null 2>&1; then command -v "$c"; return 0; fi
    if [[ -x "$c" ]]; then printf '%s\n' "$c"; return 0; fi
  done
  return 1
}
{
  echo "[领主战争 Android构建] $(date -u +%FT%TZ)"
  echo "project=$PROJECT"
  if ! UNITY="$(find_unity)"; then
    echo "BLOCKED: 未找到Unity Editor。未执行C#编译、场景导入或APK构建。"
    echo "需要Unity 2022.3 LTS（项目记录版本2022.3.62f1）及Android Build Support/SDK/NDK/JDK。"
    exit 42
  fi
  echo "unity=$UNITY"
  "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -executeMethod LordWar.EditorBuild.AndroidBuild.BuildFromCommandLine -logFile -
} 2>&1 | tee "$LOG"
