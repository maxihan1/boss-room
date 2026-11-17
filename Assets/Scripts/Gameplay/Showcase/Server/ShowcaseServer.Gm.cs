// 시험 서버 전용 GM 명령 — 채팅 칸에 「/gm …」을 보내면 전투 · 성장 상태를 바로 만든다. 서버 환경값 BOSSROOM_GM=1 일 때만 듣는다
using System;
using System.Linq;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        // 운영 서버에서는 꺼 둔다 — 켜지 않은 서버에서 「/gm …」은 그냥 채팅 글이다
        /// <summary>무적을 켠 접속 — 서버만 안다. 접속 번호는 다시 쓰이지 않아 나간 사람 것을 따로 지우지 않는다</summary>
        public static readonly System.Collections.Generic.HashSet<ulong> GmGod = new System.Collections.Generic.HashSet<ulong>();

        // 남은 체력만큼만 깎으면 방어율 · 버프가 피해를 줄여 안 쓰러진다(레벨 30 · 갑옷 — 2026-11-17 작성 폰 실행에서 「쓰러뜨리기 실패」) — 줄어도 남지 않을 만큼 크게 깎는다
        const int k_Lethal = -1_000_000;

        static bool GmEnabled => Environment.GetEnvironmentVariable("BOSSROOM_GM") == "1";

        /// <summary>GM 명령이면 처리하고 true. 결과는 보낸 사람에게만 「[GM] …」 알림으로 돌려준다</summary>
        static bool TryGm(Profile p, string text)
        {
            if (!GmEnabled || !(text == "/gm" || text.StartsWith("/gm "))) return false;
            var a = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var cmd = a.Length > 1 ? a[1] : "";
            var hasN = int.TryParse(a.Length > 2 ? a[2] : "", out var n);
            var me = PlayerServerCharacter.GetPlayerServerCharacter(p.ClientId);
            string done;
            switch (cmd)
            {
                case "killall":
                case "killboss":
                    var npcs = UnityEngine.Object.FindObjectsByType<ServerCharacter>(FindObjectsSortMode.None)
                        .Where(c => c.IsNpc && c.LifeState == LifeState.Alive && (cmd == "killall" || c.CharacterType == CharacterTypeEnum.ImpBoss)).ToList();
                    foreach (var c in npcs) c.GetComponent<IDamageable>()?.ReceiveHitPoints(null, k_Lethal);
                    done = $"{cmd} {npcs.Count}";
                    break;
                case "god":
                    if (!GmGod.Remove(p.ClientId)) GmGod.Add(p.ClientId);
                    done = $"god {(GmGod.Contains(p.ClientId) ? "on" : "off")}";
                    break;
                case "heal" when me != null:
                    var hp = me.CharacterClass.BaseHP.Value;
                    if (me.LifeState == LifeState.Fainted) me.Revive(null, hp); else me.GetComponent<IDamageable>()?.ReceiveHitPoints(null, hp);
                    done = "heal";
                    break;
                case "faint" when me != null:
                    GmGod.Remove(p.ClientId);
                    me.GetComponent<IDamageable>()?.ReceiveHitPoints(null, k_Lethal);
                    done = "faint";
                    break;
                case "exp" when hasN && n > 0:
                    GainExp(p, n);
                    done = $"exp +{n} → Lv.{p.Data.CharacterLevel}";
                    break;
                case "level" when hasN:
                    p.Data.CharacterLevel = Mathf.Clamp(n, 1, GameData.MaxLevel);
                    p.P.Exp = 0;
                    done = $"level {p.Data.CharacterLevel}";
                    break;
                case "gold" when hasN:
                    p.Gold = n;
                    done = $"gold {p.Gold}";
                    break;
                default:
                    Toast(p, "[GM] 쓸 수 있는 명령: killall · killboss · god · heal · faint(던전 안) · exp <n> · level <n> · gold <n>", true);
                    return true;
            }
            p.Dirty = true;
            Push(p);
            Toast(p, $"[GM] {done}");
            return true;
        }
    }
}
