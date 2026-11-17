// 시험 서버 전용 GM 명령 — 채팅 칸에 「/gm …」을 보내면 전투 · 성장 상태를 바로 만들고 서버 값을 읽어 준다. 서버 환경값 BOSSROOM_GM=1 일 때만 듣는다
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Showcase;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        // 운영 서버에서는 꺼 둔다 — 켜지 않은 서버에서 「/gm …」은 그냥 채팅 글이다
        /// <summary>무적을 켠 접속 — 서버만 안다. 접속 번호는 다시 쓰이지 않아 나간 사람 것을 따로 지우지 않는다</summary>
        public static readonly System.Collections.Generic.HashSet<ulong> GmGod = new System.Collections.Generic.HashSet<ulong>();

        // 남은 체력만큼만 깎으면 방어율 · 버프가 피해를 줄여 안 쓰러진다(레벨 30 · 갑옷 — 2026-11-17 작성 폰 실행에서 「쓰러뜨리기 실패」) — 줄어도 남지 않을 만큼 크게 깎는다
        const int k_Lethal = -1_000_000;

        public static bool GmEnabled => Environment.GetEnvironmentVariable("BOSSROOM_GM") == "1";

        /// <summary>전리품 고정 — null 이면 원래 확률, "none" 이면 아무것도 안 나오고, 아이템 이름이면 그것만 나온다. 서버 전체에 걸린다(시험 서버는 폰 한 대가 쓴다)</summary>
        public static string GmLoot;

        // 마지막으로 주고받은 피해 · 회복 · 쓴 스킬 — 화면에 안 보이는 서버 값을 「/gm stat」이 읽어 준다
        class Seen { public int Dealt, Taken, Healed; public string Action = "-"; }
        static readonly Dictionary<ulong, Seen> s_Seen = new Dictionary<ulong, Seen>();
        static Seen SeenOf(ulong clientId) => s_Seen.TryGetValue(clientId, out var v) ? v : s_Seen[clientId] = new Seen();

        /// <summary>체력이 바뀔 때마다 — 보정이 다 끝난 값(음수 피해 · 양수 회복)</summary>
        public static void GmNote(ServerCharacter inflicter, ServerCharacter target, int hp)
        {
            if (!GmEnabled || hp == 0) return;
            if (hp < 0 && inflicter != null && !inflicter.IsNpc) SeenOf(inflicter.OwnerClientId).Dealt = -hp;
            if (target == null || target.IsNpc) return;
            if (hp < 0) SeenOf(target.OwnerClientId).Taken = -hp; else SeenOf(target.OwnerClientId).Healed = hp;
        }

        public static void GmNoteAction(ServerCharacter who, Unity.BossRoom.Gameplay.Actions.ActionID id)
        {
            if (!GmEnabled || who == null || who.IsNpc) return;
            if (GameDataSource.Instance.TryGetActionPrototypeByID(id, out var action)) SeenOf(who.OwnerClientId).Action = ActionName(action);
        }

        static string ActionName(Unity.BossRoom.Gameplay.Actions.Action a) => a == null ? "-" : string.IsNullOrEmpty(a.Config.DisplayedName) ? a.name : a.Config.DisplayedName;

        // 값 읽기는 채팅 창 「알림」 줄로 돌려준다 — 알림 띠는 몇 초 뒤 사라져 케이스가 읽기 전에 없어진다
        static void GmLine(Profile p, string text) => ShowcaseNet.ToClient(p.ClientId, "chat", new ChatLine { Channel = "시스템", Text = "[GM] " + text });

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        static Vector3 OnNav(Vector3 at) => NavMesh.SamplePosition(at, out var hit, 6f, NavMesh.AllAreas) ? hit.position : at;

        static IEnumerable<NetworkObject> NetPrefabs() => NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs.Where(x => x.Prefab != null).Select(x => x.Prefab.GetComponent<NetworkObject>()).Where(x => x != null);

        static NetworkObject NpcPrefab(CharacterTypeEnum type) => NetPrefabs().FirstOrDefault(n =>
        {
            // 플레이어 프리팹은 직업이 비어 있어 읽으면 던진다
            try { return n.TryGetComponent<ServerCharacter>(out var c) && c.CharacterClass != null && c.CharacterClass.CharacterType == type; }
            catch (Exception) { return false; }
        });

        static void GmStat(Profile p, ServerCharacter me)
        {
            var seen = SeenOf(p.ClientId);
            GmLine(p, $"stat 레벨={p.Data.CharacterLevel} 경험치={p.P.Exp} 다음레벨={GameData.ExpToNext(p.Data.CharacterLevel)} 골드={p.Gold} 공격배율={F(p.AttackMult)} 방어율={F(p.Defense)} 골드배율={F(p.GoldMult)} 경험치배율={F(p.ExpMult)}");
            GmLine(p, $"stat 처치={p.P.TotalKills} 보스처치={p.P.BossKills} 준피해={seen.Dealt} 받은피해={seen.Taken} 회복={seen.Healed} 쓴스킬={seen.Action} 저장대기={(p.Dirty ? "예" : "아니오")} 전리품고정={GmLoot ?? "없음"}");
            if (me == null) return;
            var c = me.CharacterClass;
            var at = me.physicsWrapper.Transform.position;
            GmLine(p, $"stat 직업={c.DisplayedName} 체력={me.HitPoints}/{c.BaseHP.Value} 위치={F(at.x)},{F(at.z)} 스킬={ActionName(c.Skill1)},{ActionName(c.Skill2)},{ActionName(c.Skill3)}");
        }

        static string ActionRow(Unity.BossRoom.Gameplay.Actions.Action a) => a == null ? "-" : $"{ActionName(a)}(값 {a.Config.Amount} · 사거리 {F(a.Config.Range)} · 재사용 {F(a.Config.ReuseTimeSeconds)}초)";

        static bool GmData(Profile p, string what)
        {
            switch (what)
            {
                case "monster":
                case "skill":
                    foreach (var c in GameDataSource.Instance.CharacterDataByType.Values.Where(c => c.IsNpc == (what == "monster")))
                        GmLine(p, $"data {what} {(c.CharacterType switch { CharacterTypeEnum.Imp => "임프", CharacterTypeEnum.VandalImp => "던지는 임프", CharacterTypeEnum.ImpBoss => "보스 임프", _ => c.DisplayedName })}({c.CharacterType}) 체력={c.BaseHP.Value} 스킬={ActionRow(c.Skill1)},{ActionRow(c.Skill2)},{ActionRow(c.Skill3)}");
                    return true;
                case "bag":
                    foreach (var i in p.Bag.Where(i => string.IsNullOrEmpty(i.GainCurrencyName))) GmLine(p, $"data bag {i.ItemName} 번호={i.ItemInstanceId}");
                    GmLine(p, $"data bag 끝 {p.Bag.Count}개");
                    return true;
                default: return false;
            }
        }

        // 보스 · 바닥 스위치가 지금 있는 자리 — 그 방이 아직 안 열렸으면 null
        static Vector3? TpSpot(ServerCharacter me, string where)
        {
            if (where == "switch") return UnityEngine.Object.FindAnyObjectByType<FloorSwitch>() is { } sw ? sw.transform.position : (Vector3?)null;
            var boss = UnityEngine.Object.FindObjectsByType<ServerCharacter>(FindObjectsSortMode.None).FirstOrDefault(c => c.IsNpc && c.CharacterType == CharacterTypeEnum.ImpBoss && c.LifeState == LifeState.Alive);
            var prefab = NpcPrefab(CharacterTypeEnum.ImpBoss);
            var spawner = UnityEngine.Object.FindObjectsByType<ServerWaveSpawner>(FindObjectsSortMode.None).FirstOrDefault(w => w.Prefab != null && w.Prefab == prefab);
            var at = boss != null ? boss.physicsWrapper.Transform.position : spawner != null ? spawner.transform.position : (Vector3?)null;
            // 보스 바로 위가 아니라 몇 걸음 앞에 세운다
            return at == null ? null : at.Value + (me.physicsWrapper.Transform.position - at.Value).normalized * 6f;
        }

        // 던전의 방은 플레이어가 문턱(방 여는 장치)에 닿아야 열린다 — 자리가 아직 없으면 문턱을 차례로 밟아 방을 연 뒤 보낸다. 끝나면 「도착」 알림을 준다
        static async System.Threading.Tasks.Task GmTp(Profile p, ServerCharacter me, string where)
        {
            var spot = TpSpot(me, where);
            if (spot == null)
            {
                foreach (var loader in UnityEngine.Object.FindObjectsByType<Unity.Multiplayer.Samples.Utilities.ServerAdditiveSceneLoader>(FindObjectsSortMode.None))
                {
                    if (me == null) return;
                    me.Movement.Teleport(OnNav(loader.TryGetComponent<Collider>(out var edge) ? edge.bounds.center : loader.transform.position));
                    for (var i = 0; i < 32 && me != null && (spot = TpSpot(me, where)) == null; i++) await System.Threading.Tasks.Task.Delay(250);
                    if (spot != null) break;
                }
            }
            if (me == null) return;
            if (spot == null) { Toast(p, $"[GM] tp {where} 실패 — 던전에서 그 자리를 못 찾았습니다", true); return; }
            me.Movement.Teleport(OnNav(spot.Value));
            Toast(p, $"[GM] tp {where} 도착");
        }

        static string GmSpawn(ServerCharacter me, string what)
        {
            var prefab = what switch
            {
                "imp" => NpcPrefab(CharacterTypeEnum.Imp),
                "thrower" => NpcPrefab(CharacterTypeEnum.VandalImp),
                "boss" => NpcPrefab(CharacterTypeEnum.ImpBoss),
                "pot" => NetPrefabs().FirstOrDefault(n => n.GetComponent<PickUpState>() != null),
                _ => null,
            };
            if (prefab == null) return null;
            var t = me.physicsWrapper.Transform;
            var clone = UnityEngine.Object.Instantiate(prefab, OnNav(t.position + t.forward * 3f), Quaternion.LookRotation(-t.forward));
            if (!clone.IsSpawned) clone.Spawn(true);
            return $"spawn {what}";
        }

        /// <summary>GM 명령이면 처리하고 true. 결과는 보낸 사람에게만 「[GM] …」 알림으로 돌려준다</summary>
        static bool TryGm(Profile p, string text)
        {
            if (!GmEnabled || !(text == "/gm" || text.StartsWith("/gm "))) return false;
            var a = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var cmd = a.Length > 1 ? a[1] : "";
            var arg = a.Length > 2 ? a[2] : "";
            var hasN = int.TryParse(arg, out var n);
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
                case "hp" when hasN && me != null:
                    me.GmSetHitPoints(Mathf.Clamp(n, 1, me.CharacterClass.BaseHP.Value));
                    done = $"hp {me.HitPoints}";
                    break;
                case "tp" when me != null && (arg == "boss" || arg == "switch"):
                    _ = GmTp(p, me, arg);
                    done = $"tp {arg} 가는 중";
                    break;
                case "spawn" when me != null && GmSpawn(me, arg) is { } spawned:
                    done = spawned;
                    break;
                case "loot" when arg != "":
                    // 이름에 빈칸이 있는 아이템(임프의 뿔)도 받는다
                    var loot = string.Join(" ", a.Skip(2));
                    GmLoot = loot == "off" ? null : loot;
                    done = $"loot {GmLoot ?? "off"}";
                    break;
                case "quest" when arg == "reset":
                    p.P.Quests.Clear();
                    ResetDailyIfNeeded(p);
                    done = "quest reset";
                    break;
                case "stat":
                    GmStat(p, me);
                    done = "stat";
                    break;
                case "data" when GmData(p, arg):
                    done = $"data {arg}";
                    break;
                default:
                    Toast(p, "[GM] 쓸 수 있는 명령: killall · killboss · god · heal · faint · hp <n> · tp boss|switch · spawn imp|thrower|boss|pot(던전 안) · exp <n> · level <n> · gold <n> · loot <아이템|none|off> · quest reset · stat · data monster|skill|bag", true);
                    return true;
            }
            p.Dirty = true;
            Push(p);
            Toast(p, $"[GM] {done}");
            return true;
        }
    }
}
