// 내 화면 쪽 상태 — 서버가 보낸 캐릭터 요약 · 알림을 받아 두고 화면들이 구독한다
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static class ShowcaseClient
    {
        public static ProfileView Profile;
        public static event Action<ProfileView> ProfileChanged;
        public static event Action<Toast> Toasted;
        static readonly Dictionary<string, Action<string>> s_Extra = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            ShowcaseNet.OnClient("profile", json =>
            {
                Profile = JsonUtility.FromJson<ProfileView>(json);
                Debug.Log($"[profile] {Profile.Name} Lv.{Profile.Level} 골드 {Profile.Gold} 가방 {Profile.Items.Count}");
                ProfileChanged?.Invoke(Profile);
            });
            ShowcaseNet.OnClient("toast", json =>
            {
                var t = JsonUtility.FromJson<Toast>(json);
                Debug.Log($"[toast] {t.Text}");
                Toasted?.Invoke(t);
            });
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientStopped += _ => Profile = null;
        }

        public static void Send(string type, Req req = null) => ShowcaseNet.ToServer(type, req ?? new Req());
    }
}
