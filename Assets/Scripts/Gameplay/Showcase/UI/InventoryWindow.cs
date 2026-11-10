// 가방 창 — 왼쪽은 장착 칸 셋(무기 · 갑옷 · 장신구)과 능력치, 오른쪽은 가방 목록(같은 물약 · 전리품은 묶어서). 장착 · 해제 · 판매를 서버에 요청한다
using System.Linq;
using TMPro;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class InventoryWindow : Window
    {
        RectTransform m_Gear;
        RectTransform m_List;

        public static void Toggle(Transform canvas) => Open<InventoryWindow>(canvas, "가방", new Vector2(1400, 820));

        protected override void Render(ProfileView p)
        {
            if (m_List == null)
            {
                UIKit.Label(Body, "장착 중", 30, UIKit.Gold, new Vector2(0f, 1f), new Vector2(260, -110), new Vector2(420, 40), title: true);
                m_Gear = UIKit.Rect(Body, "Gear", new Vector2(0f, 1f), new Vector2(260, -380), new Vector2(440, 460));
                UIKit.Label(Body, "가방", 30, UIKit.Gold, new Vector2(0f, 1f), new Vector2(940, -110), new Vector2(820, 40), title: true);
                m_List = UIKit.ScrollList(Body, new Vector2(0f, 1f), new Vector2(940, -420), new Vector2(840, 560));
            }
            UIKit.Clear(m_Gear);
            Slot(p, "무기", p.Weapon, 0);
            Slot(p, "갑옷", p.Armor, 1);
            Slot(p, "장신구", p.Accessory, 2);
            UIKit.Label(m_Gear, $"Lv.{p.Level} · 골드 {UIKit.Number(p.Gold)}", 26, UIKit.Cream, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(440, 40));

            UIKit.Clear(m_List);
            var equipped = new[] { p.Weapon, p.Armor, p.Accessory };
            foreach (var group in p.Items.Where(i => !equipped.Contains(i.Id)).GroupBy(i => GameData.Item(i.Name).Slot is ItemSlot.Potion or ItemSlot.Material ? i.Name : i.Id.ToString()))
            {
                var first = group.First();
                var def = GameData.Item(first.Name);
                var row = UIKit.Row(m_List, 96);
                var count = group.Count() > 1 ? $" <color=#FFD152>x{group.Count()}</color>" : "";
                UIKit.Label(row, $"<color={GameData.GradeColor(def.Grade)}>{def.Name}</color>{count}", 30, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(230, 16), new Vector2(420, 40));
                UIKit.Label(row, def.Desc, 22, UIKit.Dim, new Vector2(0f, 0.5f), new Vector2(230, -20), new Vector2(420, 32));
                if (def.Slot is ItemSlot.Weapon or ItemSlot.Armor or ItemSlot.Accessory)
                    UIKit.Button(row, "장착", new Vector2(1f, 0.5f), new Vector2(-250, 0), new Vector2(140, 70), () => ShowcaseClient.Send("equip", new Req { Id = first.Id }), 26);
                else if (def.Slot == ItemSlot.Potion)
                    UIKit.Button(row, "사용", new Vector2(1f, 0.5f), new Vector2(-250, 0), new Vector2(140, 70), () => ShowcaseClient.Send("potion"), 26);
                UIKit.Button(row, $"판매 {def.SellPrice}G", new Vector2(1f, 0.5f), new Vector2(-90, 0), new Vector2(160, 70), () => ShowcaseClient.Send("sell", new Req { Id = first.Id }), 24);
            }
            if (m_List.childCount == 0) UIKit.Label(UIKit.Row(m_List, 96, new Color(0, 0, 0, 0)), "가방이 비어 있습니다", 28, UIKit.Dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 40), TextAlignmentOptions.Center);
        }

        void Slot(ProfileView p, string label, long id, int index)
        {
            var item = p.Items.FirstOrDefault(i => i.Id == id);
            var y = -70 - index * 125;
            var box = UIKit.Image(m_Gear, label, ShowcaseSkin.I.Button, Color.white, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(440, 110));
            UIKit.Label(box.transform, label, 22, UIKit.Dim, new Vector2(0f, 1f), new Vector2(80, -24), new Vector2(120, 30));
            if (item == null)
            {
                UIKit.Label(box.transform, "비어 있음", 28, UIKit.Dim, new Vector2(0.5f, 0.5f), new Vector2(20, -12), new Vector2(380, 40), TextAlignmentOptions.Center);
                return;
            }
            var def = GameData.Item(item.Name);
            UIKit.Label(box.transform, $"<color={GameData.GradeColor(def.Grade)}>{def.Name}</color>", 30, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(170, -6), new Vector2(280, 40));
            UIKit.Label(box.transform, def.Desc, 20, UIKit.Dim, new Vector2(0f, 0.5f), new Vector2(170, -36), new Vector2(280, 28));
            UIKit.Button(box.transform, "해제", new Vector2(1f, 0.5f), new Vector2(-62, 0), new Vector2(100, 60), () => ShowcaseClient.Send("unequip", new Req { Id = id }), 22);
        }
    }
}
