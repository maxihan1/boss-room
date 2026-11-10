// 내 화면 쪽 소셜 상태 — 채팅 기록(최근 60줄) · 친구 · 파티 · 길드 요약 · 받은 초대
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static class SocialState
    {
        public static readonly List<ChatLine> Chat = new();
        public static SocialView Last = new();
        public static event Action<ChatLine> ChatReceived;
        public static event Action<SocialView> Changed;
        public static event Action<Invite> Invited;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            ShowcaseNet.OnClient("chat", json =>
            {
                var line = JsonUtility.FromJson<ChatLine>(json);
                Chat.Add(line);
                if (Chat.Count > 60) Chat.RemoveAt(0);
                ChatReceived?.Invoke(line);
            });
            ShowcaseNet.OnClient("social", json =>
            {
                Last = JsonUtility.FromJson<SocialView>(json);
                Changed?.Invoke(Last);
            });
            // 다시 접속하면 앞 접속의 채팅 · 소셜이 남지 않게 비운다
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null) nm.OnClientStopped += _ => { Chat.Clear(); Last = new SocialView(); };
            ShowcaseNet.OnClient("invite", json =>
            {
                var invite = JsonUtility.FromJson<Invite>(json);
                Debug.Log($"[invite] {invite.Text}");
                Invited?.Invoke(invite);
            });
        }

        public static string Color(string channel) => channel switch
        {
            "파티" => "#7FD7FF",
            "길드" => "#9BE37B",
            "시스템" => "#FFD152",
            _ => "#F2ECE0",
        };

        public static string Format(ChatLine l) => l.Channel == "시스템"
            ? $"<color={Color(l.Channel)}>[알림] {l.Text}</color>"
            : $"<color={Color(l.Channel)}>[{l.Channel}] {l.From}:</color> {l.Text}";
    }
}
