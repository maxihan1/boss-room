// 채팅 · 친구 · 파티 · 길드 창이 그리는 꼴
using System;
using System.Collections.Generic;

namespace Unity.BossRoom.Gameplay.Showcase
{
    [Serializable]
    public class ChatLine
    {
        public string Channel; // 전체 · 파티 · 길드 · 시스템
        public string From;
        public string Text;
    }

    [Serializable]
    public class Member
    {
        public int Id;
        public string Name;
        public int Level;
        public bool Online;
        public bool Leader;
    }

    [Serializable]
    public class SocialView
    {
        public List<Member> Friends = new();
        public List<string> FriendRequests = new();
        public List<Member> Party = new();
        public string GuildName = "";
        public bool GuildLeader;
        public List<Member> Guild = new();
    }

    // 상대가 보낸 친구 신청 · 파티 · 길드 초대 — 받은 쪽 화면에 수락 · 거절 창을 띄운다
    [Serializable]
    public class Invite
    {
        public string Kind; // friend · party · guild
        public string From;
        public string Text;
    }
}
