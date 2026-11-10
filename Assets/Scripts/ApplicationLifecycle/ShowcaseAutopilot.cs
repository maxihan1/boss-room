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
                if (scene.name == "BossRoom") StartCoroutine(Shot(scene.name + "-later", 15f));
            }
            if (AutoPlay && scene.name == "CharSelect") StartCoroutine(PickSeatAndReady());
        }

        IEnumerator PickSeatAndReady()
        {
            yield return new WaitForSeconds(2f);
            ClientCharSelectState.Instance?.OnPlayerClickedSeat(Seat);
            yield return new WaitForSeconds(1.5f);
            ClientCharSelectState.Instance?.OnPlayerClickedReady();
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
