// 시연 게임의 규칙표 — 아이템 · 레벨 곡선 · 처치 보상 · 일일 퀘스트. 아이템 이름이 곧 운영툴 지급 칸에 적는 이름이다
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public enum ItemSlot { None, Weapon, Armor, Accessory, Potion, Material }
    public enum ItemGrade { Common, Rare, Epic, Legendary }

    public class ItemDef
    {
        public string Name;
        public ItemSlot Slot;
        public ItemGrade Grade;
        public int Price;        // 상점 판매가. 0 이면 상점에 없다
        public float Attack;     // 공격력 +비율
        public float Defense;    // 받는 피해 -비율
        public float GoldBonus;
        public float ExpBonus;
        public string Desc;
        public int SellPrice => Math.Max(1, (Price > 0 ? Price : GradeValue) / 4);
        int GradeValue => Grade switch { ItemGrade.Legendary => 2000, ItemGrade.Epic => 800, ItemGrade.Rare => 200, _ => 40 };
    }

    public class QuestDef
    {
        public string Id;
        public string Title;
        public int Target;
        public int Gold;
        public int Exp;
    }

    public static class GameData
    {
        public const string Gold = "Gold";
        public const int MaxLevel = 30;

        public static readonly List<ItemDef> Items = new()
        {
            new ItemDef { Name = "녹슨 단검", Slot = ItemSlot.Weapon, Grade = ItemGrade.Common, Price = 80, Attack = 0.05f, Desc = "공격력 +5%" },
            new ItemDef { Name = "강철 롱소드", Slot = ItemSlot.Weapon, Grade = ItemGrade.Rare, Price = 350, Attack = 0.12f, Desc = "공격력 +12%" },
            new ItemDef { Name = "룬 새김 도끼", Slot = ItemSlot.Weapon, Grade = ItemGrade.Epic, Price = 900, Attack = 0.2f, Desc = "공격력 +20%" },
            new ItemDef { Name = "불꽃의 검", Slot = ItemSlot.Weapon, Grade = ItemGrade.Legendary, Attack = 0.3f, Desc = "공격력 +30% · 보스 처치 보상" },
            new ItemDef { Name = "가죽 갑옷", Slot = ItemSlot.Armor, Grade = ItemGrade.Common, Price = 100, Defense = 0.08f, Desc = "받는 피해 -8%" },
            new ItemDef { Name = "사슬 갑옷", Slot = ItemSlot.Armor, Grade = ItemGrade.Rare, Price = 400, Defense = 0.15f, Desc = "받는 피해 -15%" },
            new ItemDef { Name = "용비늘 갑옷", Slot = ItemSlot.Armor, Grade = ItemGrade.Legendary, Defense = 0.25f, Desc = "받는 피해 -25% · 보스 처치 보상" },
            new ItemDef { Name = "행운의 반지", Slot = ItemSlot.Accessory, Grade = ItemGrade.Rare, Price = 300, GoldBonus = 0.2f, Desc = "골드 획득 +20%" },
            new ItemDef { Name = "현자의 목걸이", Slot = ItemSlot.Accessory, Grade = ItemGrade.Rare, Price = 300, ExpBonus = 0.2f, Desc = "경험치 획득 +20%" },
            new ItemDef { Name = "체력 물약", Slot = ItemSlot.Potion, Grade = ItemGrade.Common, Price = 30, Desc = "체력 50% 회복 · 던전에서 사용" },
            new ItemDef { Name = "임프의 뿔", Slot = ItemSlot.Material, Grade = ItemGrade.Common, Desc = "상점에 팔 수 있는 전리품" },
            new ItemDef { Name = "보스의 왕관 조각", Slot = ItemSlot.Material, Grade = ItemGrade.Epic, Desc = "상점에 비싸게 팔 수 있는 전리품" },
        };

        static readonly Dictionary<string, ItemDef> s_ByName = Items.ToDictionary(i => i.Name);

        // 운영툴이 모르는 이름을 보내도 가방에는 넣고 「알 수 없는 아이템」으로 보여 준다
        public static ItemDef Item(string name) => name != null && s_ByName.TryGetValue(name, out var d) ? d
            : new ItemDef { Name = name ?? "?", Slot = ItemSlot.Material, Grade = ItemGrade.Common, Desc = "알 수 없는 아이템" };

        public static bool Known(string name) => name != null && s_ByName.ContainsKey(name);

        public static int ExpToNext(int level) => level >= MaxLevel ? 0 : (int)(100 * Math.Pow(level, 1.5));

        // 레벨마다 공격 +3% · 받는 피해 -1%
        public static float LevelBonus(int level) => 0.03f * (level - 1);
        public static float LevelDefense(int level) => 0.01f * (level - 1);

        public static readonly List<QuestDef> DailyQuests = new()
        {
            new QuestDef { Id = "daily_imp", Title = "임프 30마리 처치", Target = 30, Gold = 150, Exp = 200 },
            new QuestDef { Id = "daily_boss", Title = "던전 보스 처치", Target = 1, Gold = 300, Exp = 400 },
            new QuestDef { Id = "daily_shop", Title = "상점에서 물건 구매", Target = 1, Gold = 50, Exp = 50 },
        };

        public const int ImpExp = 12;
        public const int ImpGoldMin = 3, ImpGoldMax = 7;
        public const int BossExp = 300;
        public const int BossGold = 200;
        public const float ImpHornDrop = 0.3f;
        public const float PotionDrop = 0.08f;

        // 한국 시간 하루 — 일일 퀘스트가 자정에 바뀐다
        public static string Today() => DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd");

        public static string GradeColor(ItemGrade g) => g switch
        {
            ItemGrade.Legendary => "#FF9A3C",
            ItemGrade.Epic => "#C77DFF",
            ItemGrade.Rare => "#5AB8FF",
            _ => "#E8E2D6",
        };
    }
}
