#!/usr/bin/env bash
# Boss Room 시연 빌드 — android: 폰 APK · server: Linux 전용 서버 · mac: 맥 접속 시험용 · smoke: 맥에서 서버+접속기로 접속 확인
set -euo pipefail

UNITY=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity}
ROOT=$(cd "$(dirname "$0")/.." && pwd)
mkdir -p "$ROOT/Builds"

unity() { "$UNITY" -batchmode -quit -nographics -projectPath "$ROOT" -buildTarget "$1" -executeMethod "$2" -logFile "$ROOT/Builds/$3.log"; }

case "${1:-}" in
  android) unity Android ShowcaseBuild.Android android; ls -lh "$ROOT/Builds/android/bossroom.apk" ;;
  server)  unity Linux64 ShowcaseBuild.LinuxServer linux-server; ls -lh "$ROOT/Builds/linux-server/BossRoomServer.x86_64" ;;
  mac)     unity OSXUniversal ShowcaseBuild.MacSmoke mac ;;
  smoke)
    # 서버를 띄우고 접속기가 붙어 CharSelect 까지 가는지 본다
    BIN="$ROOT/Builds/mac/BossRoom.app/Contents/MacOS/$(ls "$ROOT/Builds/mac/BossRoom.app/Contents/MacOS" | head -1)"
    PORT=${PORT:-7790}
    "$BIN" -batchmode -nographics -server -port "$PORT" -logFile "$ROOT/Builds/smoke-server.log" &
    SERVER=$!
    trap 'kill $SERVER 2>/dev/null || true' EXIT
    sleep 8
    "$BIN" -batchmode -nographics -connect "127.0.0.1:$PORT" -name smoke -quitAfter 20 -logFile "$ROOT/Builds/smoke-client.log" || true
    sleep 3
    # 전용 서버는 손님이 다 나가도 떠 있어야 한다
    if grep -q "to OfflineState" "$ROOT/Builds/smoke-server.log"; then echo "SMOKE FAIL: 손님이 나가자 서버가 꺼짐" >&2; exit 1; fi
    grep -E "Changed connection state|\[smoke\]" "$ROOT/Builds/smoke-client.log"
    grep -q "to ClientConnectedState" "$ROOT/Builds/smoke-client.log" && grep -q "장면 CharSelect" "$ROOT/Builds/smoke-client.log" && echo "SMOKE OK" || { echo "SMOKE FAIL" >&2; exit 1; }
    ;;
  shots)
    # 창을 띄워 호스트로 혼자 던전까지 들어가며 장면마다 찍는다 — 결과 Builds/shots/<이름>
    BIN="$ROOT/Builds/mac/BossRoom.app/Contents/MacOS/$(ls "$ROOT/Builds/mac/BossRoom.app/Contents/MacOS" | head -1)"
    OUT="$ROOT/Builds/shots/${2:-now}"; rm -rf "$OUT"
    "$BIN" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -host -autoplay -seat "${SEAT:-0}" -shots "$OUT" -quitAfter "${QUIT_AFTER:-60}" -logFile "$ROOT/Builds/shots.log" || true
    ls "$OUT"
    ;;
  *) echo "쓰는 법: Tools/build.sh android|server|mac|smoke|shots [이름]" >&2; exit 2 ;;
esac
