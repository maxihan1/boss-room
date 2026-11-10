// 우편함 창 — 운영팀 지급 · 보상 우편을 보여 주고 「받기」로 첨부(아이템 · 골드)를 가방에 넣는다. 열어 본 우편은 읽음으로 바뀐다
using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class MailWindow : Window
    {
        static MailListView s_Last;
        static event Action<MailListView> Received;
        RectTransform m_List;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            GameHud.ExtraMenu.Add(("우편", Toggle));
            ShowcaseNet.OnClient("mail", json =>
            {
                s_Last = JsonUtility.FromJson<MailListView>(json);
                Received?.Invoke(s_Last);
            });
        }

        public static void Toggle(Transform canvas)
        {
            var w = Open<MailWindow>(canvas, "우편함", new Vector2(1200, 820));
            if (w == null) return;
            Received += w.Draw;
            ShowcaseClient.Send("mail_list");
        }

        void OnDestroy() => Received -= Draw;

        protected override void Render(ProfileView p) { }

        void Draw(MailListView v)
        {
            if (this == null) return;
            if (m_List == null)
            {
                m_List = UIKit.ScrollList(Body, new Vector2(0.5f, 1f), new Vector2(0, -380), new Vector2(1080, 560));
                UIKit.Button(Body, "모두 받기", new Vector2(0f, 0f), new Vector2(150, 56), new Vector2(220, 70), () => ShowcaseClient.Send("mail_take_all"), 28);
            }
            UIKit.Clear(m_List);
            if (v.Mails.Count == 0)
            {
                UIKit.Label(UIKit.Row(m_List, 120, new Color(0, 0, 0, 0)), "받은 우편이 없습니다", 30, UIKit.Dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 40), TextAlignmentOptions.Center);
                return;
            }
            foreach (var m in v.Mails)
            {
                var hasGift = m.Gold > 0 || m.Items.Count > 0;
                var row = UIKit.Row(m_List, 150, m.IsRead ? (Color?)null : new Color(1f, 0.82f, 0.32f, 0.14f));
                var mark = m.IsRead ? "" : "<color=#FFD152>● </color>";
                UIKit.Label(row, $"{mark}{m.Subject}", 30, UIKit.Cream, new Vector2(0f, 1f), new Vector2(390, -32), new Vector2(740, 40));
                UIKit.Label(row, $"{m.Sender} · {m.Body}", 22, UIKit.Dim, new Vector2(0f, 1f), new Vector2(390, -70), new Vector2(740, 32));
                var gifts = m.Items.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key).ToList();
                if (m.Gold > 0) gifts.Add($"{m.Gold:N0} 골드");
                UIKit.Label(row, hasGift ? "첨부: " + string.Join(", ", gifts) : "첨부 없음", 24, hasGift ? UIKit.Gold : UIKit.Dim, new Vector2(0f, 1f), new Vector2(390, -110), new Vector2(740, 32));
                var id = m.Id;
                if (hasGift) UIKit.Button(row, "받기", new Vector2(1f, 0.5f), new Vector2(-250, 0), new Vector2(150, 72), () => ShowcaseClient.Send("mail_take", new Req { Id = id }), 28);
                UIKit.Button(row, "삭제", new Vector2(1f, 0.5f), new Vector2(-90, 0), new Vector2(140, 72), () => ShowcaseClient.Send("mail_delete", new Req { Id = id }), 26);
                if (!m.IsRead) ShowcaseClient.Send("mail_read", new Req { Id = id });
            }
        }
    }
}
