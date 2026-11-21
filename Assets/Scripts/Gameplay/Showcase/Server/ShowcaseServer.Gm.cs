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
            // 맞은 플레이어가 방패(ChargedShieldAction)를 쓰는 중이면 그 순간 충전률 c 도 「조건」으로 적는다 — 받는 피해 배율 1 − (0.5 + c²/2) 를 테스트가 줄마다 다시 셈한다.
            // 맞으면 충전이 먼저 멈추고(AttackedByEnemy) 그 값으로 배율을 셈한 뒤 여기 오니, 적는 c 가 배율에 쓴 c 와 같다.
            // 방패는 효과(5초)가 재사용(1.2초)보다 길어 겹쳐 켜질 수 있고, 겹치면 배율을 곱한다(원래 Boss Room · BRM-REQ-103) — 배율에 든 방패의 c 를 모두 배열로 적는다
            var shields = hp < 0 && target != null && !target.IsNpc ? target.ActionPlayer.BuffingActions<Unity.BossRoom.Gameplay.Actions.ChargedShieldAction>() : null;
            var shieldField = shields is { Count: > 0 } ? $",\"shield\":[{string.Join(",", shields.Select(x => F(x.PercentChargedUp)))}]" : "";
            MissionLog(hp < 0 ? "hit" : "heal", $"\"from\":{LogWho(inflicter)},\"to\":{LogWho(target)},\"skill\":{Q(inflicter != null && !inflicter.IsNpc ? SeenOf(inflicter.OwnerClientId).Action : "-")},\"value\":{Math.Abs(hp)},\"hpBefore\":{(target != null ? target.HitPoints : 0)}{shieldField}");
            if (hp < 0 && inflicter != null && !inflicter.IsNpc) SeenOf(inflicter.OwnerClientId).Dealt = -hp;
            if (target == null || target.IsNpc) return;
            if (hp < 0) SeenOf(target.OwnerClientId).Taken = -hp; else SeenOf(target.OwnerClientId).Healed = hp;
        }

        public static void GmNoteAction(ServerCharacter who, Unity.BossRoom.Gameplay.Actions.ActionID id)
        {
            if (!GmEnabled || who == null || who.IsNpc) return;
            if (GameDataSource.Instance.TryGetActionPrototypeByID(id, out var action)) SeenOf(who.OwnerClientId).Action = ActionName(action);
        }

        // 미션 로그 — 피해 · 회복 · 처치 · 아이템 · 레벨 업을 일어난 차례대로 한 줄씩 쌓고 시험 주소 /test/log 가 내준다.
        // 「/gm stat」은 마지막 값 하나만 기억해 준비하는 사이 먼저 맞은 값이 읽혔고(받은 피해 12 자리에 13), 읽으려면 채팅 창을 열어야 했다.
        // 줄에는 그때의 조건(레벨 · 직업 · 장비 · 던전 인원)과 결과 값만 적는다 — 게임이 셈한 배율은 안 적는다. 테스트가 기획서 공식으로 다시 셈해 견준다
        const int k_LogMax = 5000;
        static readonly List<string> s_Log = new List<string>();
        static int s_LogDropped;

        public static void MissionLog(string kind, string rest)
        {
            if (!GmEnabled) return;
            // ponytail: 꽉 차면 맨 앞 줄을 뺀다(O(n)) — 줄이 훨씬 많아지면 둥근 버퍼로
            if (s_Log.Count >= k_LogMax) { s_Log.RemoveAt(0); s_LogDropped++; }
            s_Log.Add($"{{\"seq\":{s_LogDropped + s_Log.Count + 1},\"at\":{Q(DateTime.UtcNow.ToString("o"))},\"kind\":{Q(kind)},\"players\":{PlayerServerCharacter.GetPlayerServerCharacters().Count},{rest}}}");
        }

        /// <summary>since 번 줄 뒤의 줄 — 줄 번호는 서버가 켜진 뒤로 이어진다. clear 면 내준 뒤 비운다</summary>
        static string MissionLogSince(int since, bool clear)
        {
            var lines = string.Join(",", s_Log.Skip(Math.Max(0, since - s_LogDropped)));
            var last = s_LogDropped + s_Log.Count;
            if (clear) { s_LogDropped = last; s_Log.Clear(); }
            return $"{{\"ok\":true,\"last\":{last},\"lines\":[{lines}]}}";
        }

        static string LogWho(ServerCharacter c) => c == null ? "{\"npc\":\"-\"}" : c.IsNpc ? $"{{\"npc\":{Q(c.CharacterType.ToString())}}}" : LogWho(Of(c.OwnerClientId), c);

        static string LogWho(Profile p, ServerCharacter c = null)
        {
            if (p == null) return "{\"user\":\"?\"}";
            if (c == null) c = PlayerServerCharacter.GetPlayerServerCharacter(p.ClientId);
            var job = "-";
            // 플레이어 프리팹은 직업이 비어 있어 읽으면 던진다(로비)
            try { if (c != null && c.CharacterClass != null) job = c.CharacterClass.DisplayedName; }
            catch (Exception) { }
            // 접속 안 한 계정(되돌리기의 기본 지급)은 세션이 없어 캐릭터 이름을 쓴다
            return $"{{\"user\":{Q(SessionName(p.ClientId) is { Length: > 0 } s ? s : p.Name)},\"level\":{p.Data.CharacterLevel},\"job\":{Q(job)},\"weapon\":{Q(p.Equipped(p.P.EquippedWeapon)?.Name ?? "-")},\"armor\":{Q(p.Equipped(p.P.EquippedArmor)?.Name ?? "-")},\"accessory\":{Q(p.Equipped(p.P.EquippedAccessory)?.Name ?? "-")}}}";
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

        // 공통 행동은 원래 이름(Revive 등) 대신 화면 · 기획서 낱말로 찍는다
        static string CommonRow(string name, Unity.BossRoom.Gameplay.Actions.Action a) => a == null ? $"{name}(-)" : $"{name}(값 {a.Config.Amount} · 사거리 {F(a.Config.Range)} · 재사용 {F(a.Config.ReuseTimeSeconds)}초)";

        static string ActionRow(Unity.BossRoom.Gameplay.Actions.Action a) => a == null ? "-" : $"{ActionName(a)}(값 {ActionAmount(a)} · 사거리 {F(a.Config.Range)} · 재사용 {F(a.Config.ReuseTimeSeconds)}초)";

        // 투사체 스킬(화살 쏘기 · 마력탄 등)은 실제 피해가 Amount 가 아니라 투사체 설정(Projectiles[].Damage)에 있다 — 그 값을 찍는다
        static int ActionAmount(Unity.BossRoom.Gameplay.Actions.Action a) =>
            a.Config.Projectiles != null && a.Config.Projectiles.Length > 0 ? a.Config.Projectiles[0].Damage : a.Config.Amount;

        static bool GmData(Profile p, string what)
        {
            switch (what)
            {
                case "monster":
                case "skill":
                    foreach (var c in GameDataSource.Instance.CharacterDataByType.Values.Where(c => c.IsNpc == (what == "monster")))
                        GmLine(p, $"data {what} {(c.CharacterType switch { CharacterTypeEnum.Imp => "임프", CharacterTypeEnum.VandalImp => "던지는 임프", CharacterTypeEnum.ImpBoss => "보스 임프", _ => c.DisplayedName })}({c.CharacterType}) 체력={c.BaseHP.Value} 스킬={ActionRow(c.Skill1)},{ActionRow(c.Skill2)},{ActionRow(c.Skill3)}");
                    // 직업과 상관없는 공통 행동(부활 · 들기 · 내려놓기)도 같은 꼴로 — 기획서가 그 값(부활 체력 · 사거리 · 재사용)을 적는데 읽을 길이 없었다
                    if (what == "skill")
                    {
                        var g = GameDataSource.Instance;
                        GmLine(p, $"data skill 공통 행동 스킬={CommonRow("부활", g.ReviveActionPrototype)},{CommonRow("들기", g.PickUpActionPrototype)},{CommonRow("내려놓기", g.DropActionPrototype)}");
                    }
                    // item · bag 처럼 끝 줄을 찍는다 — 없으면 읽는 쪽이 언제 다 왔는지 몰라 줄이 멈출 때까지 기다렸다
                    GmLine(p, $"data {what} 끝 {GameDataSource.Instance.CharacterDataByType.Values.Count(x => x.IsNpc == (what == "monster"))}종");
                    return true;
                case "item":
                    // 아이템 표 — 기획서 4.1 표와 같은 낱말(분류 · 등급)로 낸다. 효과는 비율 그대로(0.05 = +5%)
                    foreach (var d in GameData.Items)
                        GmLine(p, $"data item {d.Name} 분류={(d.Slot switch { ItemSlot.Weapon => "무기", ItemSlot.Armor => "갑옷", ItemSlot.Accessory => "장신구", ItemSlot.Potion => "물약", _ => "전리품" })} 등급={(d.Grade switch { ItemGrade.Legendary => "전설", ItemGrade.Epic => "영웅", ItemGrade.Rare => "희귀", _ => "일반" })} 상점가={(d.Price > 0 ? d.Price.ToString() : "없음")} 판매가={d.SellPrice} 공격={d.Attack} 방어={d.Defense} 골드획득={d.GoldBonus} 경험치획득={d.ExpBonus} 설명={d.Desc}");
                    GmLine(p, $"data item 끝 {GameData.Items.Count}종");
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
                case "quest" when arg == "done":
                    // 오늘 일일 퀘스트를 모두 목표까지 채운다(보상은 안 받은 그대로) — 임프 30마리를 실제로 잡으면 케이스 준비가 100초를 넘었다.
                    // 실제로 채울 때와 같은 길(AddQuest)이라 「퀘스트 완료」 알림도 같이 뜨고, 이미 채웠거나 받은 퀘스트는 건너뛴다
                    ResetDailyIfNeeded(p);
                    foreach (var def in GameData.DailyQuests) AddQuest(p, def.Id, def.Target);
                    done = "quest done";
                    break;
                case "stat":
                    GmStat(p, me);
                    done = "stat";
                    break;
                case "data" when GmData(p, arg):
                    done = $"data {arg}";
                    break;
                default:
                    Toast(p, "[GM] 쓸 수 있는 명령: killall · killboss · god · heal · faint · hp <n> · tp boss|switch · spawn imp|thrower|boss|pot(던전 안) · exp <n> · level <n> · gold <n> · loot <아이템|none|off> · quest reset|done · stat · data monster|skill|item|bag", true);
                    return true;
            }
            p.Dirty = true;
            Push(p);
            Toast(p, $"[GM] {done}");
            return true;
        }
    }
}
