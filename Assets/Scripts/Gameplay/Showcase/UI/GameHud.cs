// 접속해 있는 동안 늘 떠 있는 화면 — 상단 바(이름 · 레벨 · 경험치 · 골드) · 메뉴 버튼 · 획득 알림. 로비(캐릭터 선택)와 던전에서 배치가 다르다
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }
        public Transform CanvasRoot { get; private set; }

        TextMeshProUGUI m_Name, m_Gold, m_ExpText, m_Potion;
        Image m_Exp;
        RectTransform m_Bar, m_Menu, m_Toasts, m_PotionButton;
        GameObject m_Notice;
        readonly List<(string label, System.Action<Transform> open)> m_MenuItems = new();
        readonly Dictionary<string, TextMeshProUGUI> m_MenuLabels = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            ShowcaseClient.ProfileChanged += p => { if (Instance == null) Create(); };
            ShowcaseClient.Toasted += t => Instance?.ShowToast(t);
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientStopped += _ => { if (Instance != null) Destroy(Instance.gameObject); };
        }

        // 다른 창(우편 · 친구 · 길드)이 메뉴에 버튼을 더한다
        public static readonly List<(string label, System.Action<Transform> open)> ExtraMenu = new();

        static void Create()
        {
            var canvas = UIKit.Canvas("Showcase HUD", 100);
            DontDestroyOnLoad(canvas.gameObject);
            Instance = canvas.gameObject.AddComponent<GameHud>();
            Instance.CanvasRoot = canvas.transform;
            Instance.Build();
            ShowcaseClient.ProfileChanged += Instance.Render;
            SceneManager.sceneLoaded += Instance.OnScene;
            Instance.Layout(SceneManager.GetActiveScene().name);
            if (ShowcaseClient.Profile != null) Instance.Render(ShowcaseClient.Profile);
        }

        void OnDestroy()
        {
            ShowcaseClient.ProfileChanged -= Render;
            SceneManager.sceneLoaded -= OnScene;
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            var root = CanvasRoot;
            // 이름 · 골드 한 줄, 경험치 막대 한 줄
            m_Bar = UIKit.Image(root, "PlayerBar", ShowcaseSkin.I.Button, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(460, 100)).rectTransform;
            m_Name = UIKit.Label(m_Bar, "", 28, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(150, 18), new Vector2(260, 40));
            m_Gold = UIKit.Label(m_Bar, "", 28, UIKit.Gold, new Vector2(1f, 0.5f), new Vector2(-100, 18), new Vector2(170, 40), TextAlignmentOptions.MidlineRight, title: true);
            m_Exp = UIKit.Bar(m_Bar, new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(400, 18), new Color(0.45f, 0.75f, 1f));
            m_ExpText = UIKit.Text(m_Exp.transform.parent, "", 16, UIKit.Cream, TextAlignmentOptions.Center);

            m_Notice = NoticeBar.Create(root, -150).gameObject;

            m_Menu = UIKit.Rect(root, "Menu", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200, 600));
            m_MenuItems.Add(("가방", InventoryWindow.Toggle));
            m_MenuItems.Add(("상점", ShopWindow.Toggle));
            m_MenuItems.Add(("퀘스트", QuestWindow.Toggle));
            m_MenuItems.AddRange(ExtraMenu);
            for (var i = 0; i < m_MenuItems.Count; i++)
            {
                var item = m_MenuItems[i];
                var b = UIKit.Button(m_Menu, item.label, new Vector2(0.5f, 1f), new Vector2(0, -50 - i * 92), new Vector2(190, 80), () => item.open(CanvasRoot), 30);
                m_MenuLabels[item.label] = b.GetComponentInChildren<TextMeshProUGUI>();
            }

            m_PotionButton = UIKit.Button(root, "", new Vector2(1f, 0f), new Vector2(-760, 100), new Vector2(190, 90), () => ShowcaseClient.Send("potion"), 28).GetComponent<RectTransform>();
            m_Potion = m_PotionButton.GetComponentInChildren<TextMeshProUGUI>();
            m_Toasts = UIKit.Rect(root, "Toasts", new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(900, 300));
            var v = m_Toasts.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.spacing = 6;
            v.childControlHeight = false;
            v.childForceExpandHeight = false;
        }

        void OnScene(Scene s, LoadSceneMode mode) { if (mode == LoadSceneMode.Single) Layout(s.name); }

        void Layout(string scene)
        {
            var dungeon = scene == "BossRoom";
            gameObject.SetActive(scene != "MainMenu");
            // 로비: 왼쪽 위 「영웅 선택」 띠와 오른쪽 「준비」 창 사이에 상단 바, 메뉴는 왼쪽 세로 줄
            // 던전: 왼쪽 위 초상화 · 오른쪽 위 버튼을 피해 가운데 위, 메뉴는 오른쪽 가운데에 작게
            m_Bar.anchoredPosition = dungeon ? new Vector2(0, -62) : new Vector2(150, -70);
            m_Bar.localScale = Vector3.one * (dungeon ? 0.85f : 1f);
            m_Notice.SetActive(!dungeon);
            m_Menu.anchorMin = m_Menu.anchorMax = dungeon ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            m_Menu.anchoredPosition = dungeon ? new Vector2(-100, 60) : new Vector2(130, 30);
            m_Menu.localScale = Vector3.one * (dungeon ? 0.75f : 1f);
            m_PotionButton.gameObject.SetActive(dungeon);
        }

        void Render(ProfileView p)
        {
            m_Name.text = $"<color=#FFD152>Lv.{p.Level}</color>  {p.Name}";
            m_Exp.fillAmount = p.ExpNext > 0 ? Mathf.Clamp01((float)p.Exp / p.ExpNext) : 1f;
            m_ExpText.text = p.ExpNext > 0 ? $"{p.Exp}/{p.ExpNext}" : "최고 레벨";
            m_Gold.text = $"{UIKit.Number(p.Gold)} G";
            // 안 받은 우편 수를 버튼에 붙인다
            if (m_MenuLabels.TryGetValue("우편", out var mail)) mail.text = p.UnreadMail > 0 ? $"우편 <color=#FF6B5B>{p.UnreadMail}</color>" : "우편";
            var potions = p.Items.Count(i => i.Name == "체력 물약");
            m_Potion.text = $"물약 {potions}";
        }

        void ShowToast(Toast t)
        {
            var bg = UIKit.Image(m_Toasts, "Toast", null, t.Error ? new Color(0.45f, 0.05f, 0.05f, 0.85f) : new Color(0f, 0f, 0f, 0.7f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(820, 60));
            UIKit.Text(bg.transform, t.Text, 28, t.Error ? new Color(1f, 0.85f, 0.8f) : UIKit.Cream, TextAlignmentOptions.Center);
            while (m_Toasts.childCount > 4) DestroyImmediate(m_Toasts.GetChild(0).gameObject);
            StartCoroutine(Fade(bg.gameObject));
        }

        static IEnumerator Fade(GameObject go)
        {
            yield return new WaitForSecondsRealtime(3f);
            var group = go.AddComponent<CanvasGroup>();
            for (var a = 1f; a > 0 && go != null; a -= Time.unscaledDeltaTime * 2f)
            {
                group.alpha = a;
                yield return null;
            }
            if (go != null) Destroy(go);
        }
    }
}
