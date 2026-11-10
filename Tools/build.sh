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
  setup)   unity OSXUniversal ShowcaseSetup.Run setup; grep -E "\[setup\]|error CS" "$ROOT/Builds/setup.log" ;;
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
    "$BIN" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -host -autoplay -uiTour -seat "${SEAT:-0}" -shots "$OUT" -quitAfter "${QUIT_AFTER:-60}" -logFile "$ROOT/Builds/shots.log" || true
    ls "$OUT"
    ;;
  login)
    # 실제 게임 API 에 붙은 맥 서버 + 화면 없는 접속기 둘(맞는 · 틀린 비밀번호) — 비밀은 ~/.config/anyrpg-test 에서 읽는다
    BIN="$ROOT/Builds/mac/BossRoom.app/Contents/MacOS/$(ls "$ROOT/Builds/mac/BossRoom.app/Contents/MacOS" | head -1)"
    CFG=${CFG:-$HOME/.config/anyrpg-test}
    USER_ID=${USER_ID:-qa_player1}
    PASS=$(grep "^$USER_ID=" "$CFG/game-accounts.txt" | cut -d= -f2-)
    PORT=${PORT:-7791}
    ( set -a; . "$CFG/game-server.env"; set +a; exec "$BIN" -batchmode -nographics -server -port "$PORT" -logFile "$ROOT/Builds/t-server.log" ) > /dev/null 2>&1 &
    SERVER=$!
    trap 'kill $SERVER 2>/dev/null || true' EXIT
    sleep 8
    "$BIN" -batchmode -nographics -connect "127.0.0.1:$PORT" -user "$USER_ID" -pass "$PASS" -quitAfter "${QUIT_AFTER:-15}" ${ACTIONS:+-actions "$ACTIONS"} -logFile "$ROOT/Builds/t-client.log" > /dev/null 2>&1 || true
    "$BIN" -batchmode -nographics -connect "127.0.0.1:$PORT" -user "$USER_ID" -pass wrong-pass -quitAfter 8 -logFile "$ROOT/Builds/t-client-bad.log" > /dev/null 2>&1 || true
    echo "== 서버"; grep -E "\[api\]|Exception" "$ROOT/Builds/t-server.log" | head -10 || true
    echo "== 맞는 비밀번호"; grep -E "\[profile\]|\[toast\]|to ClientConnectedState" "$ROOT/Builds/t-client.log" | head
    echo "== 틀린 비밀번호"; grep -E "Changed connection state" "$ROOT/Builds/t-client-bad.log" | head
    ;;
  social)
    # 실제 게임 API 에 붙은 맥 서버 + 계정 둘이 친구 · 파티 · 길드 · 채팅을 주고받는다
    BIN="$ROOT/Builds/mac/BossRoom.app/Contents/MacOS/$(ls "$ROOT/Builds/mac/BossRoom.app/Contents/MacOS" | head -1)"
    CFG=${CFG:-$HOME/.config/anyrpg-test}
    P1=$(grep "^qa_player1=" "$CFG/game-accounts.txt" | cut -d= -f2-)
    P2=$(grep "^qa_player2=" "$CFG/game-accounts.txt" | cut -d= -f2-)
    PORT=${PORT:-7792}
    ( set -a; . "$CFG/game-server.env"; set +a; exec "$BIN" -batchmode -nographics -server -port "$PORT" -logFile "$ROOT/Builds/s-server.log" ) > /dev/null 2>&1 &
    SERVER=$!
    trap 'kill $SERVER 2>/dev/null || true' EXIT
    sleep 8
    "$BIN" -batchmode -nographics -connect "127.0.0.1:$PORT" -user qa_player1 -pass "$P1" -quitAfter 45 -logFile "$ROOT/Builds/s-p1.log" \
      -actions "${A1:-wait:3;friend_add:qa_player2;wait:4;party_invite:qa_player2;wait:4;guild_create:보스헌터;wait:2;guild_invite:qa_player2;wait:5;say:안녕하세요;psay:파티 출발;gsay:길드 반가워요}" > /dev/null 2>&1 &
    C1=$!
    sleep 1
    "$BIN" -batchmode -nographics -connect "127.0.0.1:$PORT" -user qa_player2 -pass "$P2" -quitAfter 44 -logFile "$ROOT/Builds/s-p2.log" \
      -actions "${A2:-wait:5;friend_accept:qa_player1;wait:3;party_accept:qa_player1;wait:5;guild_accept:qa_player1;wait:4;say:반가워요}" > /dev/null 2>&1 || true
    wait $C1 || true
    echo "== 서버"; grep -E "\[api\]|Exception" "$ROOT/Builds/s-server.log" | head -8 || true
    for n in p1 p2; do echo "== $n"; grep -E "\[toast\]|\[invite\]|\[script\] (소셜|끝)|\[chat\]" "$ROOT/Builds/s-$n.log" | head -25 || true; done
    ;;
  *) echo "쓰는 법: Tools/build.sh android|server|mac|smoke|shots [이름]" >&2; exit 2 ;;
esac
