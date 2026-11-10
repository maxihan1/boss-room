// 일일 퀘스트 창 — 한국 시간 자정에 바뀐다. 다 채우면 「보상 받기」
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class QuestWindow : Window
    {
        RectTransform m_List;

        public static void Toggle(Transform canvas) => Open<QuestWindow>(canvas, "일일 퀘스트", new Vector2(1100, 640));

        protected override void Render(ProfileView p)
        {
            m_List ??= UIKit.ScrollList(Body, new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(980, 400));
            UIKit.Clear(m_List);
            foreach (var q in p.Quests)
            {
                var done = q.Count >= q.Target;
                var row = UIKit.Row(m_List, 110, done && !q.Rewarded ? new Color(1f, 0.82f, 0.32f, 0.16f) : (Color?)null);
                UIKit.Label(row, q.Title, 30, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(240, 20), new Vector2(440, 40));
                UIKit.Label(row, $"보상 {q.Gold} 골드 · {q.Exp} 경험치", 22, UIKit.Dim, new Vector2(0f, 0.5f), new Vector2(240, -22), new Vector2(440, 32));
                var bar = UIKit.Bar(row, new Vector2(1f, 0.5f), new Vector2(-370, 0), new Vector2(220, 26), UIKit.Gold);
                bar.fillAmount = Mathf.Clamp01((float)q.Count / q.Target);
                UIKit.Label(row, $"{q.Count}/{q.Target}", 24, UIKit.Cream, new Vector2(1f, 0.5f), new Vector2(-370, 30), new Vector2(220, 30), TMPro.TextAlignmentOptions.Center);
                if (q.Rewarded) UIKit.Label(row, "받음", 28, UIKit.Green, new Vector2(1f, 0.5f), new Vector2(-110, 0), new Vector2(160, 40), TMPro.TextAlignmentOptions.Center);
                else
                {
                    var id = q.Id;
                    var btn = UIKit.Button(row, "보상 받기", new Vector2(1f, 0.5f), new Vector2(-110, 0), new Vector2(170, 70), () => ShowcaseClient.Send("quest_claim", new Req { Name = id }), 24);
                    btn.interactable = done;
                }
            }
        }
    }
}
