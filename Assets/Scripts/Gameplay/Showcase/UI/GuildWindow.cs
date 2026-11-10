// 길드 창 — 길드가 없으면 이름을 지어 500 골드로 만들고, 있으면 길드원 목록(접속 표시) · 길드장 초대 · 추방 · 탈퇴
using TMPro;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class GuildWindow : Window
    {
        RectTransform m_Content;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init() => GameHud.ExtraMenu.Add(("길드", Toggle));

        public static void Toggle(Transform canvas)
        {
            var w = Open<GuildWindow>(canvas, "길드", new Vector2(1100, 820));
            if (w == null) return;
            SocialState.Changed += w.Draw;
            w.Draw(SocialState.Last);
            ShowcaseClient.Send("social");
        }

        void OnDestroy() => SocialState.Changed -= Draw;
        protected override void Render(ProfileView p) { }

        void Draw(SocialView v)
        {
            if (this == null) return;
            if (m_Content != null) Destroy(m_Content.gameObject);
            m_Content = UIKit.Stretch(UIKit.Rect(Body, "Content", Vector2.zero, Vector2.zero, Vector2.zero));
            if (string.IsNullOrEmpty(v.GuildName))
            {
                UIKit.Label(m_Content, "아직 길드가 없습니다\n길드를 만들거나 길드장의 초대를 받아 보세요", 30, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(900, 120), TextAlignmentOptions.Center);
                var name = UIKit.Input(m_Content, "길드 이름 (2~12자)", new Vector2(0.5f, 0.5f), new Vector2(-130, -40), new Vector2(560, 80));
                name.characterLimit = 12;
                UIKit.Button(m_Content, "만들기 · 500G", new Vector2(0.5f, 0.5f), new Vector2(320, -40), new Vector2(280, 80), () => ShowcaseClient.Send("guild_create", new Req { Name = name.text.Trim() }), 26);
                return;
            }
            UIKit.Label(m_Content, $"「{v.GuildName}」 · 길드원 {v.Guild.Count}명", 32, UIKit.Gold, new Vector2(0.5f, 1f), new Vector2(0, -116), new Vector2(940, 44), TextAlignmentOptions.MidlineLeft, title: true);
            if (v.GuildLeader)
            {
                var invite = UIKit.Input(m_Content, "초대할 캐릭터 이름", new Vector2(0.5f, 1f), new Vector2(-140, -190), new Vector2(560, 76));
                UIKit.Button(m_Content, "초대", new Vector2(0.5f, 1f), new Vector2(260, -190), new Vector2(180, 76), () => ShowcaseClient.Send("guild_invite", new Req { Name = invite.text.Trim() }), 26);
            }
            var list = UIKit.ScrollList(m_Content, new Vector2(0.5f, 1f), new Vector2(0, -470), new Vector2(980, 440));
            foreach (var m in v.Guild)
            {
                var row = UIKit.Row(list, 80);
                var dot = m.Online ? "<color=#7BE37B>●</color>" : "<color=#6B6458>●</color>";
                UIKit.Label(row, $"{dot} {(m.Leader ? "<color=#FFD152>[길드장]</color> " : "")}{m.Name}  <color=#B8AE99>Lv.{m.Level}</color>", 28, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(300, 0), new Vector2(560, 40));
                var id = m.Id;
                if (v.GuildLeader && !m.Leader) UIKit.Button(row, "추방", new Vector2(1f, 0.5f), new Vector2(-90, 0), new Vector2(140, 62), () => ShowcaseClient.Send("guild_kick", new Req { Id = id }), 24);
            }
            UIKit.Button(m_Content, "길드 탈퇴", new Vector2(0f, 0f), new Vector2(150, 56), new Vector2(220, 70), () => ShowcaseClient.Send("guild_leave"), 26);
        }
    }
}
