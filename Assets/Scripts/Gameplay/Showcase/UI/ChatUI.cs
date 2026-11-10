// 채팅 — 화면 왼쪽 아래 최근 두 줄 띠(누르면 창), 채팅 창은 전체 · 파티 · 길드 탭과 입력 칸
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class ChatStrip : MonoBehaviour
    {
        TextMeshProUGUI m_Text;

        public static RectTransform Create(Transform canvas)
        {
            var bg = UIKit.Image(canvas, "ChatStrip", null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0f, 0f), Vector2.zero, new Vector2(700, 96));
            var strip = bg.gameObject.AddComponent<ChatStrip>();
            strip.m_Text = UIKit.Text(bg.transform, "", 24, UIKit.Cream, TextAlignmentOptions.BottomLeft);
            strip.m_Text.rectTransform.offsetMin = new Vector2(18, 10);
            strip.m_Text.rectTransform.offsetMax = new Vector2(-18, -8);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => ChatWindow.Toggle(canvas));
            SocialState.ChatReceived += strip.OnLine;
            strip.Redraw();
            return bg.rectTransform;
        }

        void OnDestroy() => SocialState.ChatReceived -= OnLine;
        void OnLine(ChatLine _) => Redraw();

        void Redraw()
        {
            var lines = SocialState.Chat.Skip(System.Math.Max(0, SocialState.Chat.Count - 2)).Select(SocialState.Format).ToList();
            m_Text.text = lines.Count == 0 ? "<color=#B8AE99>눌러서 채팅하기</color>" : string.Join("\n", lines);
        }
    }

    public class ChatWindow : Window
    {
        RectTransform m_List;
        TMP_InputField m_Input;
        string m_Channel = "전체";
        readonly System.Collections.Generic.Dictionary<string, Button> m_Tabs = new();

        public static void Toggle(Transform canvas)
        {
            var w = Open<ChatWindow>(canvas, "채팅", new Vector2(1100, 820));
            if (w == null) return;
            w.Build();
        }

        void Build()
        {
            var x = -330f;
            foreach (var ch in new[] { "전체", "파티", "길드" })
            {
                var c = ch;
                m_Tabs[ch] = UIKit.Button(Body, ch, new Vector2(0.5f, 1f), new Vector2(x, -110), new Vector2(200, 64), () => { m_Channel = c; Redraw(); }, 26);
                x += 220;
            }
            m_List = UIKit.ScrollList(Body, new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(980, 500));
            m_Input = UIKit.Input(Body, "메시지 입력 (100자)", new Vector2(0f, 0f), new Vector2(380, 56), new Vector2(620, 76));
            m_Input.characterLimit = 100;
            m_Input.onSubmit.AddListener(_ => SendLine());
            UIKit.Button(Body, "보내기", new Vector2(0f, 0f), new Vector2(810, 56), new Vector2(190, 76), SendLine, 28);
            SocialState.ChatReceived += OnLine;
            Redraw();
        }

        void OnDestroy() => SocialState.ChatReceived -= OnLine;
        void OnLine(ChatLine _) => Redraw();
        protected override void Render(ProfileView p) { }

        void SendLine()
        {
            if (string.IsNullOrWhiteSpace(m_Input.text)) return;
            ShowcaseClient.Send("chat", new Req { Name = m_Channel, Text = m_Input.text });
            m_Input.text = "";
            m_Input.ActivateInputField();
        }

        void Redraw()
        {
            if (m_List == null) return;
            foreach (var kv in m_Tabs) kv.Value.GetComponent<Image>().color = kv.Key == m_Channel ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            UIKit.Clear(m_List);
            foreach (var line in SocialState.Chat.Where(l => m_Channel == "전체" || l.Channel == m_Channel || l.Channel == "시스템").TakeLast(30))
            {
                var row = UIKit.Row(m_List, 46, new Color(0, 0, 0, 0));
                UIKit.Text(row, SocialState.Format(line), 26, UIKit.Cream).rectTransform.offsetMin = new Vector2(16, 0);
            }
        }
    }
}
