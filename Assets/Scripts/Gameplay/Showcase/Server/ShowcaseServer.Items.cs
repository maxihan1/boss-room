// 서버 쪽 가방 · 장비 · 상점 · 물약 처리 — 클라이언트 요청을 확인한 뒤 바꾸고 다시 보내 준다
using System.Linq;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Showcase;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static void RegisterHandlers()
        {
            ShowcaseNet.OnServer("hello", (id, _) => { if (Of(id) is { } p) Push(p); });
            ShowcaseNet.OnServer("equip", (id, json) => With(id, json, Equip));
            ShowcaseNet.OnServer("unequip", (id, json) => With(id, json, Unequip));
            ShowcaseNet.OnServer("sell", (id, json) => With(id, json, Sell));
            ShowcaseNet.OnServer("buy", (id, json) => With(id, json, Buy));
            ShowcaseNet.OnServer("potion", (id, json) => With(id, json, (p, _) => UsePotion(p)));
            RegisterProgressHandlers();
            RegisterMailHandlers();
            RegisterSocialHandlers();
        }

        static void With(ulong clientId, string json, System.Action<Profile, Req> action)
        {
            var p = Of(clientId);
            if (p == null) return;
            action(p, JsonUtility.FromJson<Req>(json) ?? new Req());
            Push(p);
        }

        static ItemInstanceSaveData Find(Profile p, long id) => p.Bag.FirstOrDefault(i => i.ItemInstanceId == id);

        static bool IsEquipped(Profile p, long id) => id != 0 && (p.P.EquippedWeapon == id || p.P.EquippedArmor == id || p.P.EquippedAccessory == id);

        static void Equip(Profile p, Req r)
        {
            var item = Find(p, r.Id);
            if (item == null) return;
            switch (GameData.Item(item.ItemName).Slot)
            {
                case ItemSlot.Weapon: p.P.EquippedWeapon = item.ItemInstanceId; break;
                case ItemSlot.Armor: p.P.EquippedArmor = item.ItemInstanceId; break;
                case ItemSlot.Accessory: p.P.EquippedAccessory = item.ItemInstanceId; break;
                default: Toast(p, "장착할 수 없는 아이템입니다", true); return;
            }
            p.Dirty = true;
            Toast(p, $"{item.ItemName} 장착");
        }

        static void Unequip(Profile p, Req r)
        {
            if (p.P.EquippedWeapon == r.Id) p.P.EquippedWeapon = 0;
            if (p.P.EquippedArmor == r.Id) p.P.EquippedArmor = 0;
            if (p.P.EquippedAccessory == r.Id) p.P.EquippedAccessory = 0;
            p.Dirty = true;
        }

        static void Sell(Profile p, Req r)
        {
            var item = Find(p, r.Id);
            if (item == null) return;
            if (IsEquipped(p, item.ItemInstanceId)) { Toast(p, "장착 중인 아이템은 팔 수 없습니다", true); return; }
            var price = GameData.Item(item.ItemName).SellPrice;
            p.Bag.Remove(item);
            p.Gold += price;
            p.Dirty = true;
            if (GameApi.Enabled) _ = GameApi.Post("/api/deleteiteminstance", new DeleteItemRequest { ItemInstanceId = item.ItemInstanceId });
            Toast(p, $"{item.ItemName} 판매 +{price} 골드");
        }

        static void Buy(Profile p, Req r)
        {
            var def = GameData.Item(r.Name);
            var count = Mathf.Clamp(r.Count, 1, 99);
            if (!GameData.Known(r.Name) || def.Price <= 0) { Toast(p, "상점에서 팔지 않는 아이템입니다", true); return; }
            if (p.Gold < def.Price * count) { Toast(p, "골드가 부족합니다", true); return; }
            p.Gold -= def.Price * count;
            AddItem(p, def.Name, count);
            AddQuest(p, "daily_shop", 1);
            Toast(p, $"{def.Name} {count}개 구매");
        }

        static void UsePotion(Profile p)
        {
            var hero = PlayerServerCharacter.GetPlayerServerCharacter(p.ClientId);
            if (hero == null || hero.LifeState != LifeState.Alive) { Toast(p, "던전에서 살아 있을 때만 쓸 수 있습니다", true); return; }
            var potion = p.Bag.FirstOrDefault(i => i.ItemName == "체력 물약");
            if (potion == null) { Toast(p, "체력 물약이 없습니다", true); return; }
            p.Bag.Remove(potion);
            p.Dirty = true;
            var max = hero.CharacterClass.BaseHP.Value;
            hero.NetHealthState.HitPoints.Value = Mathf.Min(max, hero.HitPoints + max / 2);
            Toast(p, "체력 물약 사용");
        }

        // 공격하는 쪽이 플레이어면 공격 배율, 맞는 쪽이 플레이어면 피해 감소
        public static int ModifyDamage(ServerCharacter inflicter, ServerCharacter target, int hp)
        {
            if (hp >= 0) return hp;
            if (inflicter != null && !inflicter.IsNpc && Of(inflicter.OwnerClientId) is { } a) hp = Mathf.RoundToInt(hp * a.AttackMult);
            if (target != null && !target.IsNpc && Of(target.OwnerClientId) is { } d) hp = Mathf.Min(-1, Mathf.RoundToInt(hp * (1 - d.Defense)));
            return hp;
        }

        [System.Serializable] class DeleteItemRequest { public long ItemInstanceId; }

        // 서버 저장 주기를 돌리는 빈 오브젝트
        class Ticker : MonoBehaviour
        {
            public static void Ensure()
            {
                if (FindAnyObjectByType<Ticker>() != null) return;
                var go = new GameObject("ShowcaseServerTicker");
                DontDestroyOnLoad(go);
                go.AddComponent<Ticker>();
            }

            void Update() => Tick();
        }
    }
}
