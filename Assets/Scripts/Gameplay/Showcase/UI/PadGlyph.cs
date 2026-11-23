// 꽂힌 패드 종류(PlayStation · Xbox)에 맞춰 행동 막대 단추 아래 키보드 숫자 안내를 그 패드의 버튼 그림으로 바꾼다 — 안내 그림만 바꾸고 패드 조작은 만들지 않는다
using Unity.BossRoom.Gameplay.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class PadGlyph : MonoBehaviour
    {
        // 행동 막대(Hero Action Bar 프리팹) 단추와 그 아래 숫자 안내 — 공격 · 스킬 1 · 스킬 2 · 감정
        static readonly (string button, string ps, string xbox)[] k_ActionBar =
        {
            ("Button0", "ps_square", "xbox_x"),
            ("Button1", "ps_triangle", "xbox_y"),
            ("Button2", "ps_circle", "xbox_b"),
            ("Button3", "ps_cross", "xbox_a"),
        };

        Image m_Glyph;
        GameObject m_Hint;
        string m_Ps, m_Xbox;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // 서버(헤드리스)에는 HUD 가 없다
            if (Application.isBatchMode) return;
            SceneManager.sceneLoaded += (_, _) => AttachActionBar();
            AttachActionBar();
        }

        static void AttachActionBar()
        {
            foreach (var bar in FindObjectsByType<HeroActionBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var (buttonName, ps, xbox) in k_ActionBar)
                {
                    var button = bar.transform.Find(buttonName);
                    if (button == null || button.GetComponent<PadGlyph>() != null) continue;
                    // 숫자 안내(Text (1))와 같은 자리 — 단추 바로 아래
                    Attach(button, button.Find("Text (1)")?.gameObject, ps, xbox, new Vector2(0, -1), 28);
                }
            }
        }

        // 단추 아래에 PadGlyph 그림을 붙인다. hint 는 패드가 있을 때 숨길 키보드 안내(없으면 null)
        public static void Attach(Transform button, GameObject hint, string ps, string xbox, Vector2 pos, float size)
        {
            var rt = new GameObject("PadGlyph", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(button, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.gameObject.SetActive(false);

            var glyph = button.gameObject.AddComponent<PadGlyph>();
            glyph.m_Glyph = img;
            glyph.m_Hint = hint;
            glyph.m_Ps = ps;
            glyph.m_Xbox = xbox;
            // 단추가 꺼진 채 붙으면 OnEnable 이 켜질 때 맞춘다
            if (glyph.isActiveAndEnabled) glyph.Refresh();
        }

        void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            if (m_Glyph != null) Refresh();
        }

        void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

        void OnDeviceChange(InputDevice device, InputDeviceChange change) => Refresh();

        void Refresh()
        {
            var kind = Kind();
            var sprite = kind == null ? null : Resources.Load<Sprite>("PadGlyphs/" + (kind == "ps" ? m_Ps : m_Xbox));
            m_Glyph.sprite = sprite;
            m_Glyph.gameObject.SetActive(sprite != null);
            if (m_Hint != null) m_Hint.SetActive(sprite == null);
        }

        // 가장 나중에 꽂힌 PlayStation · Xbox 패드를 따른다. DualSense · DualShock 4 와 Windows XInput · macOS Xbox layout 이 모두 이 두 타입을 잇는다.
        // Switch · 일반 패드는 그림이 없어 숫자를 그대로 둔다
        static string Kind()
        {
            var all = Gamepad.all;
            for (var i = all.Count - 1; i >= 0; i--)
            {
                if (all[i] is DualShockGamepad) return "ps";
                if (all[i] is XInputController) return "xbox";
            }
            return null;
        }
    }
}
