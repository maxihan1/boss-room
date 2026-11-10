// 친구 · 파티 창 — 이름으로 친구 신청 · 파티 초대, 받은 신청 수락, 친구 목록(접속 표시), 지금 파티
using TMPro;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class SocialWindow : Window
    {
        RectTransform m_List;
        TMP_InputField m_Name;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init() => GameHud.ExtraMenu.Add(("친구", Toggle));

        public static void Toggle(Transform canvas)
        {
            var w = Open<SocialWindow>(canvas, "친구 · 파티", new Vector2(1200, 800));
            if (w == null) return;
            w.m_Name = UIKit.Input(w.Body, "캐릭터 이름", new Vector2(0f, 1f), new Vector2(330, -116), new Vector2(520, 76));
            UIKit.Button(w.Body, "친구 신청", new Vector2(0f, 1f), new Vector2(720, -116), new Vector2(220, 76), () => w.SendName("friend_add"), 26);
            UIKit.Button(w.Body, "파티 초대", new Vector2(0f, 1f), new Vector2(960, -116), new Vector2(220, 76), () => w.SendName("party_invite"), 26);
            w.m_List = UIKit.ScrollList(w.Body, new Vector2(0.5f, 1f), new Vector2(0, -450), new Vector2(1080, 520));
            SocialState.Changed += w.Draw;
            w.Draw(SocialState.Last);
            ShowcaseClient.Send("social");
        }

        void OnDestroy() => SocialState.Changed -= Draw;
        protected override void Render(ProfileView p) { }

        void SendName(string type)
        {
            if (string.IsNullOrWhiteSpace(m_Name.text)) return;
            ShowcaseClient.Send(type, new Req { Name = m_Name.text.Trim() });
        }

        void Header(string text) => UIKit.Text(UIKit.Row(m_List, 48, new Color(0, 0, 0, 0)), text, 28, UIKit.Gold, title: true).rectTransform.offsetMin = new Vector2(10, 0);

        void Draw(SocialView v)
        {
            if (this == null || m_List == null) return;
            UIKit.Clear(m_List);
            if (v.FriendRequests.Count > 0)
            {
                Header("받은 친구 신청");
                foreach (var name in v.FriendRequests)
                {
                    var row = UIKit.Row(m_List, 84);
                    UIKit.Label(row, name, 28, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(240, 0), new Vector2(440, 40));
                    UIKit.Button(row, "수락", new Vector2(1f, 0.5f), new Vector2(-250, 0), new Vector2(140, 64), () => ShowcaseClient.Send("friend_accept", new Req { Name = name }), 24);
                    UIKit.Button(row, "거절", new Vector2(1f, 0.5f), new Vector2(-90, 0), new Vector2(140, 64), () => ShowcaseClient.Send("friend_decline", new Req { Name = name }), 24);
                }
            }
            Header($"파티 {(v.Party.Count > 0 ? v.Party.Count + "/4 · 경험치 +" + (v.Party.Count - 1) * 10 + "%" : "없음")}");
            foreach (var m in v.Party)
            {
                var row = UIKit.Row(m_List, 76);
                UIKit.Label(row, $"{(m.Leader ? "<color=#FFD152>★</color> " : "")}{m.Name}  <color=#B8AE99>Lv.{m.Level}</color>", 28, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(240, 0), new Vector2(440, 40));
            }
            if (v.Party.Count > 0)
                UIKit.Button(UIKit.Row(m_List, 80, new Color(0, 0, 0, 0)), "파티 나가기", new Vector2(1f, 0.5f), new Vector2(-120, 0), new Vector2(220, 66), () => ShowcaseClient.Send("party_leave"), 24);
            Header($"친구 {v.Friends.Count}명");
            foreach (var f in v.Friends)
            {
                var row = UIKit.Row(m_List, 84);
                var dot = f.Online ? "<color=#7BE37B>●</color>" : "<color=#6B6458>●</color>";
                UIKit.Label(row, $"{dot} {f.Name}  <color=#B8AE99>Lv.{f.Level} · {(f.Online ? "접속 중" : "오프라인")}</color>", 28, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(300, 0), new Vector2(560, 40));
                var name = f.Name;
                var id = f.Id;
                if (f.Online) UIKit.Button(row, "파티 초대", new Vector2(1f, 0.5f), new Vector2(-270, 0), new Vector2(180, 64), () => ShowcaseClient.Send("party_invite", new Req { Name = name }), 24);
                UIKit.Button(row, "삭제", new Vector2(1f, 0.5f), new Vector2(-90, 0), new Vector2(140, 64), () => ShowcaseClient.Send("friend_remove", new Req { Id = id }), 24);
            }
        }
    }
}
