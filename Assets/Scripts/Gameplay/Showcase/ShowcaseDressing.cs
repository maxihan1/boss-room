// 장면이 열릴 때마다 샘플 티가 나는 개발용 표시(네트워크 통계 안내 · 제작사 로고 줄 등)를 끈다 — 출시 게임처럼 보이게
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static class ShowcaseDressing
    {
        static readonly string[] k_Hidden = { "UnityLogo", "NetStatsMonitorPrefab", "NetStatsMonitor", "NetworkOverlay" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (_, _) => Hide();
            Hide();
        }

        static void Hide()
        {
            // 휴대폰에서는 마우스 그림 대신 터치 설명을 보인다
            if (Application.isMobilePlatform && GameObject.Find("HowToPlayPopupPanel") is { } help)
            {
                foreach (var t in help.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Controls Img")) t.gameObject.SetActive(false);
                }
                foreach (var text in help.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    text.text = text.text switch
                    {
                        "이동 · 대상" => "화면을 눌러\n이동 · 대상",
                        "기본 공격" => "적을 눌러\n기본 공격",
                        "스킬" => "아래 버튼으로\n스킬 사용",
                        _ => text.text,
                    };
                }
            }
            // 던전 왼쪽 위 초상화(파티 HUD)는 원래 화면 끝에 붙어 있어 휴대폰 카메라 구멍 · 둥근 모서리 쪽(안전 영역 밖)에 걸린다 — 안전 영역 안으로 민다
            if (GameObject.Find("Party HUD") is { } party && party.transform is RectTransform partyRect && party.GetComponentInParent<Canvas>() is { } hudCanvas && hudCanvas.scaleFactor > 0f)
                partyRect.anchoredPosition += new Vector2(Screen.safeArea.xMin / hudCanvas.scaleFactor, 0f);
            // 로비 영웅 자리 카드는 원래 화면 아래 끝에 걸쳐 누르는 칸(ClickInteract)이 몇 px 화면 밖으로 나간다 — 안전 영역 아래 여백 + 조금 위로 올린다
            if (GameObject.Find("PlayerSeats") is { } seats && seats.transform is RectTransform seatsRect && seats.GetComponentInParent<Canvas>() is { } lobbyCanvas && lobbyCanvas.scaleFactor > 0f)
                seatsRect.anchoredPosition += new Vector2(0f, Screen.safeArea.yMin / lobbyCanvas.scaleFactor + 12f);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // 접속 실패 같은 안내 창은 시연 화면(순서 100)보다 위에 떠야 한다
                if (t.name.StartsWith("PopupPanelsCanvas") && t.TryGetComponent<Canvas>(out var popup)) popup.sortingOrder = 200;
                foreach (var name in k_Hidden)
                {
                    if (t.name.StartsWith(name)) t.gameObject.SetActive(false);
                }
            }
        }
    }
}
