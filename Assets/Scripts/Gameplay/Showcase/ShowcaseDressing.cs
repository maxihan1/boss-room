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
