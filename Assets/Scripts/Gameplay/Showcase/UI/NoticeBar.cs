// 화면 위쪽 공지 띠 — 운영툴에서 켠 공지를 1분마다 다시 읽어 차례로 보여 주고, 점검 중이면 붉게 알린다
using System.Collections;
using TMPro;
using Unity.BossRoom.Showcase;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class NoticeBar : MonoBehaviour
    {
        TextMeshProUGUI m_Text;
        UnityEngine.UI.Image m_Bg;
        int m_Index;

        public static NoticeBar Create(Transform canvas, float y = -40)
        {
            // 띠를 숨겨도 다시 읽기가 멈추지 않게 컴포넌트는 늘 켜진 빈 틀에 둔다
            var holder = UIKit.Rect(canvas, "NoticeBar", new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(1240, 64));
            var bar = holder.gameObject.AddComponent<NoticeBar>();
            bar.m_Bg = UIKit.Image(holder, "Bg", null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIKit.Stretch(bar.m_Bg.rectTransform);
            bar.m_Text = UIKit.Text(bar.m_Bg.transform, "", 28, UIKit.Cream, TextAlignmentOptions.Center);
            bar.m_Text.textWrappingMode = TextWrappingModes.NoWrap;
            bar.m_Bg.gameObject.SetActive(false);
            bar.StartCoroutine(bar.Loop());
            return bar;
        }

        IEnumerator Loop()
        {
            while (true)
            {
                var task = ServerStatusClient.Fetch();
                while (!task.IsCompleted) yield return null;
                for (var i = 0; i < 6; i++)
                {
                    Show(task.Result);
                    yield return new WaitForSecondsRealtime(10f);
                }
            }
        }

        void Show(ServerStatus s)
        {
            if (s == null) { m_Bg.gameObject.SetActive(false); return; }
            if (s.maintenance != null && s.maintenance.active)
            {
                m_Bg.gameObject.SetActive(true);
                m_Bg.color = new Color(0.45f, 0.05f, 0.05f, 0.8f);
                m_Text.text = $"<color=#FFD152>[점검]</color> {s.maintenance.message}";
                return;
            }
            if (s.notices == null || s.notices.Count == 0) { m_Bg.gameObject.SetActive(false); return; }
            m_Bg.gameObject.SetActive(true);
            m_Bg.color = new Color(0f, 0f, 0f, 0.55f);
            var n = s.notices[m_Index++ % s.notices.Count];
            m_Text.text = $"<color=#FFD152>[공지]</color> {n.title} — {n.body}";
        }
    }
}
