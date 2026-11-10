// 서버 쪽 성장 · 보상 · 일일 퀘스트 — 몬스터가 죽으면 던전 안 모든 플레이어가 경험치 · 골드 · 전리품을 받는다(협동 게임이라 나눠 갖지 않는다)
using System.Linq;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static void RegisterProgressHandlers()
        {
            ShowcaseNet.OnServer("quest_claim", (id, json) => With(id, json, ClaimQuest));
        }

        public static void OnNpcKilled(ServerCharacter npc)
        {
            var boss = npc.CharacterType == CharacterTypeEnum.ImpBoss;
            foreach (var p in Profiles.Values.Where(x => x.Loaded).ToList())
            {
                var partyBonus = PartyBonus(p);
                if (boss)
                {
                    p.P.BossKills++;
                    GainGold(p, Mathf.RoundToInt(GameData.BossGold * p.GoldMult));
                    GainExp(p, Mathf.RoundToInt(GameData.BossExp * p.ExpMult * partyBonus));
                    AddQuest(p, "daily_boss", 1);
                    var legendary = Random.value < 0.35f ? (Random.value < 0.5f ? "불꽃의 검" : "용비늘 갑옷") : "보스의 왕관 조각";
                    AddItem(p, legendary);
                    Toast(p, $"보스 처치! {legendary} 획득");
                }
                else
                {
                    p.P.TotalKills++;
                    GainGold(p, Mathf.RoundToInt(Random.Range(GameData.ImpGoldMin, GameData.ImpGoldMax + 1) * p.GoldMult));
                    GainExp(p, Mathf.RoundToInt(GameData.ImpExp * p.ExpMult * partyBonus));
                    AddQuest(p, "daily_imp", 1);
                    if (Random.value < GameData.ImpHornDrop) AddItem(p, "임프의 뿔");
                    if (Random.value < GameData.PotionDrop) AddItem(p, "체력 물약");
                }
                p.Dirty = true;
                Push(p);
            }
        }

        static void GainGold(Profile p, int amount) => p.Gold += amount;

        static void GainExp(Profile p, int amount)
        {
            p.P.Exp += amount;
            while (p.Data.CharacterLevel < GameData.MaxLevel && p.P.Exp >= GameData.ExpToNext(p.Data.CharacterLevel))
            {
                p.P.Exp -= GameData.ExpToNext(p.Data.CharacterLevel);
                p.Data.CharacterLevel++;
                Toast(p, $"레벨 업! Lv.{p.Data.CharacterLevel}");
            }
            p.Dirty = true;
        }

        static void ResetDailyIfNeeded(Profile p)
        {
            var today = GameData.Today();
            if (p.P.QuestDay == today && p.P.Quests.Count == GameData.DailyQuests.Count) return;
            p.P.QuestDay = today;
            p.P.Quests = GameData.DailyQuests.Select(q => new Unity.BossRoom.Showcase.QuestProgress { QuestId = q.Id }).ToList();
            p.Dirty = true;
        }

        static void AddQuest(Profile p, string id, int n)
        {
            ResetDailyIfNeeded(p);
            var q = p.P.Quests.FirstOrDefault(x => x.QuestId == id);
            var def = GameData.DailyQuests.First(d => d.Id == id);
            if (q == null || q.Count >= def.Target) return;
            q.Count = Mathf.Min(def.Target, q.Count + n);
            if (q.Count >= def.Target) Toast(p, $"퀘스트 완료: {def.Title} — 퀘스트 창에서 보상을 받으세요");
            p.Dirty = true;
        }

        static void ClaimQuest(Profile p, Req r)
        {
            ResetDailyIfNeeded(p);
            var q = p.P.Quests.FirstOrDefault(x => x.QuestId == r.Name);
            var def = GameData.DailyQuests.FirstOrDefault(d => d.Id == r.Name);
            if (q == null || def == null || q.Rewarded || q.Count < def.Target) return;
            q.Rewarded = true;
            GainGold(p, def.Gold);
            GainExp(p, def.Exp);
            Toast(p, $"보상 받음 +{def.Gold} 골드 · +{def.Exp} 경험치");
        }

        static System.Collections.Generic.List<QuestView> QuestViews(Profile p) => GameData.DailyQuests.Select(d =>
        {
            var q = p.P.Quests.FirstOrDefault(x => x.QuestId == d.Id);
            return new QuestView { Id = d.Id, Title = d.Title, Target = d.Target, Count = q?.Count ?? 0, Rewarded = q?.Rewarded ?? false, Gold = d.Gold, Exp = d.Exp };
        }).ToList();
    }
}
