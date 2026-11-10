// 피해 숫자 — 던전에서 캐릭터 체력이 바뀌면 머리 위로 숫자를 띄워 올린다(적이 받은 피해 흰색 · 내가 받은 피해 빨강 · 회복 초록)
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class DamageNumbers : MonoBehaviour
    {
        readonly HashSet<NetworkHealthState> m_Watched = new();
        Camera m_Camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Application.isBatchMode) return; // 화면 없는 서버 · 시험 접속기에는 필요 없다
            var go = new GameObject("ShowcaseDamageNumbers");
            DontDestroyOnLoad(go);
            go.AddComponent<DamageNumbers>();
        }

        void Update()
        {
            if (Time.frameCount % 30 != 0) return;
            m_Watched.RemoveWhere(h => h == null);
            foreach (var health in FindObjectsByType<NetworkHealthState>(FindObjectsSortMode.None))
            {
                if (!health.IsSpawned || !m_Watched.Add(health)) continue;
                var target = health;
                health.HitPoints.OnValueChanged += (before, after) => Spawn(target, after - before);
            }
        }

        void Spawn(NetworkHealthState health, int delta)
        {
            if (delta == 0 || health == null || GameHud.Instance == null) return;
            m_Camera = m_Camera != null ? m_Camera : Camera.main;
            if (m_Camera == null) return;
            var mine = health.TryGetComponent<ServerCharacter>(out var sc) && !sc.IsNpc && sc.OwnerClientId == NetworkManager.Singleton.LocalClientId;
            var color = delta > 0 ? new Color(0.45f, 1f, 0.45f) : mine ? new Color(1f, 0.35f, 0.3f) : Color.white;
            var text = UIKit.Label(GameHud.Instance.CanvasRoot, delta > 0 ? $"+{delta}" : $"{-delta}", delta > 0 ? 40 : 46, color, Vector2.zero, Vector2.zero, new Vector2(200, 60), TextAlignmentOptions.Center, title: true);
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            StartCoroutine(Rise(text, health.transform));
        }

        IEnumerator Rise(TextMeshProUGUI text, Transform anchor)
        {
            var rt = (RectTransform)text.transform.parent;
            var canvas = (RectTransform)GameHud.Instance.CanvasRoot;
            var offset = new Vector3(Random.Range(-0.4f, 0.4f), 2.2f, 0);
            var basePos = anchor.position;
            for (var t = 0f; t < 0.9f && text != null; t += Time.deltaTime)
            {
                if (anchor != null) basePos = anchor.position;
                var screen = m_Camera.WorldToScreenPoint(basePos + offset + Vector3.up * t * 1.4f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
                rt.anchoredPosition = local + canvas.rect.size * 0.5f;
                text.alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.4f;
                rt.localScale = Vector3.one * (t < 0.12f ? 0.7f + t * 3f : 1.06f);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }
    }
}
