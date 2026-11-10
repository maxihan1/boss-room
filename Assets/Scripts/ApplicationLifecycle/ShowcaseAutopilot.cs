// 명령줄 -autoplay · -shots 로 켜는 자동 진행기 — 자리 고르고 준비해 던전까지 가며 장면마다 화면을 찍는다(전후 비교 · 시험용)
using System.Collections;
using System.IO;
using Unity.BossRoom.Gameplay.GameState;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.BossRoom.ApplicationLifecycle
{
    public class ShowcaseAutopilot : MonoBehaviour
    {
        public bool AutoPlay;
        public int Seat;
        public string ShotDir;
        public bool UiTour;
        int m_ShotCount;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (ShotDir != null)
            {
                StartCoroutine(Shot(scene.name, 4f));
                if (scene.name == "BossRoom")
                {
                    StartCoroutine(CloseHowToPlay(6f));
                    if (AutoPlay) StartCoroutine(Fight());
                    StartCoroutine(Shot(scene.name + "-later", 15f));
                }
            }
            if (AutoPlay && scene.name == "CharSelect") StartCoroutine(PickSeatAndReady());
        }

        IEnumerator PickSeatAndReady()
        {
            yield return new WaitForSeconds(2f);
            if (UiTour) yield return Tour();
            ClientCharSelectState.Instance?.OnPlayerClickedSeat(Seat);
            yield return new WaitForSeconds(1.5f);
            ClientCharSelectState.Instance?.OnPlayerClickedReady();
        }

        // 로비에서 창을 하나씩 열어 찍는다
        IEnumerator Tour()
        {
            yield return new WaitForSeconds(2f);
            var hud = Unity.BossRoom.Gameplay.Showcase.GameHud.Instance;
            if (hud == null) yield break;
            System.Action<Transform>[] windows =
            {
                Unity.BossRoom.Gameplay.Showcase.InventoryWindow.Toggle,
                Unity.BossRoom.Gameplay.Showcase.ShopWindow.Toggle,
                Unity.BossRoom.Gameplay.Showcase.QuestWindow.Toggle,
            };
            foreach (var extra in Unity.BossRoom.Gameplay.Showcase.GameHud.ExtraMenu) System.Array.Resize(ref windows, windows.Length + 1);
            for (var i = 3; i < windows.Length; i++) windows[i] = Unity.BossRoom.Gameplay.Showcase.GameHud.ExtraMenu[i - 3].open;
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i](hud.CanvasRoot);
                yield return Shot($"window{i}", 1.2f);
                // 저장은 프레임 끝에 일어난다 — 같은 프레임에 닫으면 닫힌 화면이 찍힌다
                yield return new WaitForSeconds(0.3f);
                windows[i](hud.CanvasRoot);
                yield return new WaitForSeconds(0.3f);
            }
        }

        // 호스트 시험용 — 적을 쓰러뜨려 보상 · 레벨 업 화면을 찍고, 보스까지 잡아 결과 화면으로 간다
        IEnumerator Fight()
        {
            yield return new WaitForSeconds(9f);
            Unity.BossRoom.Gameplay.Showcase.ShowcaseClient.Send("cheat_kill");
            yield return Shot("after-kill", 1.5f);
            yield return new WaitForSeconds(8f);
            Unity.BossRoom.Gameplay.Showcase.ShowcaseClient.Send("cheat_boss");
            yield return Shot("after-boss", 1.5f);
        }

        // 던전 첫 조작 설명 창을 닫고 전투 화면을 찍는다
        static IEnumerator CloseHowToPlay(float delay)
        {
            yield return new WaitForSeconds(delay);
            var panel = GameObject.Find("HowToPlayPopupPanel");
            if (panel != null) panel.SetActive(false);
        }

        IEnumerator Shot(string label, float delay)
        {
            yield return new WaitForSeconds(delay);
            Directory.CreateDirectory(ShotDir);
            var path = Path.Combine(ShotDir, $"{++m_ShotCount:00}-{label}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[shot] {path}");
        }
    }
}
