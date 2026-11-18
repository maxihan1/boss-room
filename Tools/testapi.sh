#!/usr/bin/env bash
# 맥 서버(GM · 시험 주소 켬)를 띄우고 시험 주소로 봇 · GM · 실패시키기 · 되돌리기를 한 번씩 불러 본다 — 계정은 qa_player3 · 4 만 쓴다
set -u
ROOT=$(cd "$(dirname "$0")/.." && pwd); OUT=${OUT:-$ROOT/Builds/testapi}; mkdir -p "$OUT"; rm -f "$OUT"/*.log
BIN="$ROOT/Builds/mac/BossRoom.app/Contents/MacOS/$(ls "$ROOT/Builds/mac/BossRoom.app/Contents/MacOS" | head -1)"
CFG=$HOME/.config/anyrpg-test; PORT=7793; TP=7783; KEY=local-test-key
pw() { grep "^$1=" "$CFG/game-accounts.txt" | cut -d= -f2-; }
( set -a; . "$CFG/game-server.env"; set +a; export BOSSROOM_PREP_USERS="qa_player3:$(grep '^qa_player3=' "$CFG/game-accounts.txt" | cut -d= -f2-)" BOSSROOM_GM=1 BOSSROOM_TEST_KEY=$KEY BOSSROOM_TEST_PORT=$TP; exec "$BIN" -batchmode -nographics -server -port $PORT -logFile "$OUT/server.log" ) >/dev/null 2>&1 &
S=$!; trap 'kill $S 2>/dev/null; pkill -P $S 2>/dev/null' EXIT; sleep 10
c() { local path=$1; shift; echo "→ $path ${NOTE:-}"; curl -sS -m 75 -H "X-Admin-Key: $KEY" -H 'content-type: application/json' "http://127.0.0.1:$TP$path" "$@" | cut -c1-${CUT:-600}; echo; }
j() { python3 -c 'import json,sys; a=sys.argv[1:]; print(json.dumps({k:(int(v) if v.isdigit() else v) for k,v in zip(a[::2],a[1::2])}, ensure_ascii=False))' "$@"; }
do_() { NOTE="$1 $2" c /test/bot/do -d "$(j bot "$1" cmd "$2")"; }
P3=$(pw qa_player3); P4=$(pw qa_player4)

echo "== 열쇠 틀림"; curl -sS -m 5 -o /dev/null -w "%{http_code}\n" "http://127.0.0.1:$TP/test/state"
c /test/state
echo "== 계정 되돌리기(기준 상태)"; c /test/reset -d "$(j user qa_player3 pass "$P3" mode base)"; c /test/reset -d "$(j user qa_player4 pass "$P4" mode base)"
echo "== 봇 셋"; c /test/bot/start -d '{"bot":"guest1"}'; c /test/bot/start -d "$(j user qa_player3 pass "$P3")"; c /test/bot/start -d "$(j user qa_player4 pass "$P4")"
c /test/state
echo "== 친구"; do_ qa_player3 "friend_add:qa_player4"; sleep 1; do_ qa_player4 "friend_accept:qa_player3"; sleep 2
echo "== 화면에 없는 요청"; do_ qa_player3 "buy:없는 아이템"; do_ qa_player3 "equip:체력 물약"
echo "== GM 값 읽기(로비)"; do_ qa_player3 "gm:gold 900"; do_ qa_player3 "gm:stat"; do_ qa_player3 "gm:data monster"; do_ qa_player3 "gm:data skill"; do_ qa_player3 "gm:data item"; do_ qa_player3 "gm:data bag"; do_ qa_player3 "gm:quest reset"; do_ qa_player3 "gm:loot 임프의 뿔"
sleep 1; CUT=300 c "/test/bot/log?bot=qa_player3&clear=1"
echo "== 로비 → 던전"; do_ guest1 seat:0; do_ qa_player3 seat:1; do_ qa_player4 seat:2; do_ guest1 ready; do_ qa_player3 ready; do_ qa_player4 ready; sleep 25
echo "== GM 던전"; do_ qa_player3 "gm:stat"; do_ qa_player3 "gm:god"; do_ qa_player3 "gm:spawn imp"; do_ qa_player3 "gm:spawn thrower"; do_ qa_player3 "gm:spawn pot"; sleep 2; do_ qa_player3 "gm:killall"; sleep 1
do_ qa_player3 "gm:hp 123"; do_ qa_player3 "gm:tp switch"; sleep 12; do_ qa_player3 "gm:stat"; do_ qa_player3 "gm:tp boss"; sleep 14; do_ qa_player3 "gm:stat"; do_ qa_player3 "gm:killboss"; sleep 2; do_ qa_player3 "gm:stat"
CUT=9000 c "/test/bot/log?bot=qa_player3" | python3 -c "import sys,json,re; [print(\"   \", m) for m in re.findall(r'\"Text\":\"([^\"]*)\"', sys.stdin.read())]"
echo "== 미션 로그 — 피해 · 처치 · 아이템 · 레벨 업 줄이 차례대로 쌓였고(종류마다 줄 수 · 첫 줄), since 뒤 것만 온다"
CUT=900000 c "/test/log?since=0" | python3 -c "
import sys, json, collections
d = json.loads(sys.stdin.read().split('\n', 1)[1]); L = d['lines']; k = collections.Counter(x['kind'] for x in L)
print('    줄', len(L), '· last', d['last'], '·', dict(k), '· 번호 이어짐', [x['seq'] for x in L] == list(range(1, len(L) + 1)))
for kind in k: print('   ', json.dumps(next(x for x in L if x['kind'] == kind), ensure_ascii=False)[:420])
"
LAST=$(curl -sS -m 10 -H "X-Admin-Key: $KEY" "http://127.0.0.1:$TP/test/log?since=999999" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['last'], len(d['lines']))"); echo "    since 999999 → last · 줄 수: $LAST(줄 수 0 이어야 함)"
echo "== 내보내기 · 실패시키기 · 우편 · 새 계정"; c /test/kick -d '{"user":"qa_player4"}'; sleep 2; c /test/state
c /test/fail -d '{"path":"/api/getmailmessages","count":1}'; c /test/mail -d "$(j user qa_player3 pass "$P3" cmd "시험 첨부")"
c /test/reset -d "$(j user qa_player4 pass "$P4" mode raw level 31)"; c /test/reset -d "$(j user qa_player4 pass "$P4" mode new)"
c /test/bot/stop -d '{"all":true}'; sleep 2; c /test/state
echo "== 싼 아이템 — 3 골드에 사서 1 골드에 판다(200 → 197 → 198)"; c /test/reset -d "$(j user qa_player3 pass "$P3" mode base)"; c /test/bot/start -d "$(j user qa_player3 pass "$P3")"
do_ qa_player3 "buy:조약돌"; do_ qa_player3 "gm:stat"; do_ qa_player3 "sell:조약돌"; do_ qa_player3 "gm:stat"; sleep 1
CUT=9000 c "/test/bot/log?bot=qa_player3&clear=1" | python3 -c "import sys,re; [print('   ', m) for m in re.findall(r'\"Text\":\"([^\"]*)\"', sys.stdin.read()) if '골드=' in m or '조약돌' in m]"
c /test/bot/stop -d '{"all":true}'; sleep 2
echo "== 캐릭터 불러오기 실패 — 접속이 안 되고 캐릭터가 늘지 않는다(characters 1)"; c /test/fail -d '{"path":"/api/getplayercharacters","count":1}'
NOTE="실패해야 함(60초)" c /test/bot/start -d "$(j user qa_player3 pass "$P3")"; c /test/bot/stop -d '{"all":true}'; c /test/reset -d "$(j user qa_player3 pass "$P3" mode base)"
echo "== 되돌리기가 우편함도 비운다 — 시험 우편을 넣고 base 로 되돌린 뒤 「모두 받기」가 「받을 첨부가 없습니다」"; c /test/mail -d "$(j user qa_player3 pass "$P3" cmd "남는 우편")"; c /test/reset -d "$(j user qa_player3 pass "$P3" mode base)"
c /test/bot/start -d "$(j user qa_player3 pass "$P3")"; do_ qa_player3 "mail_take_all"; sleep 2
CUT=9000 c "/test/bot/log?bot=qa_player3&clear=1" | python3 -c "import sys,re; [print('   ', m) for m in re.findall(r'\"Text\":\"([^\"]*)\"', sys.stdin.read()) if '첨부' in m or '우편' in m]"; c /test/bot/stop -d '{"all":true}'; sleep 2
echo "== 실행 전 준비 — 봇을 끄고 qa_player3 을 기준 상태로(레벨 1 · 골드 200)";c /test/bot/start -d "$(j user qa_player3 pass "$P3")"; do_ qa_player3 "gm:level 9"; do_ qa_player3 "gm:gold 5000"; sleep 1
c /test/prep -d '{"runId":1,"service":"ARG","env":"qa","origin":"MANUAL"}'; c /test/state; c /test/bot/start -d "$(j user qa_player3 pass "$P3")"; do_ qa_player3 "gm:stat"; sleep 1
CUT=9000 c "/test/bot/log?bot=qa_player3&clear=1" | python3 -c "import sys,re; [print('   ', m) for m in re.findall(r'\"Text\":\"([^\"]*)\"', sys.stdin.read()) if '골드=' in m]"; c /test/bot/stop -d '{"all":true}'
echo "== 서버 기록"; grep -E "Exception|\[test\]|시험 주소|\[api\]" "$OUT/server.log" | cut -c1-220 | sort | uniq -c | sort -rn | head -25
