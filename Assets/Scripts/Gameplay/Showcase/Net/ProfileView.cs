// 서버가 내 화면에 보내는 캐릭터 요약 — 상단 바 · 가방 · 퀘스트 창이 이것 하나로 그린다
using System;
using System.Collections.Generic;

namespace Unity.BossRoom.Gameplay.Showcase
{
    [Serializable]
    public class ProfileView
    {
        public string Name;
        public int Level;
        public int Exp;
        public int ExpNext;
        public int Gold;
        public List<ItemView> Items = new();
        public long Weapon, Armor, Accessory;
        public List<QuestView> Quests = new();
        public int UnreadMail;
        public string Guild = "";
        public bool Persistent;
    }

    [Serializable]
    public class ItemView
    {
        public long Id;
        public string Name;
    }

    [Serializable]
    public class QuestView
    {
        public string Id;
        public string Title;
        public int Count;
        public int Target;
        public int Gold;
        public int Exp;
        public bool Rewarded;
    }

    // 서버가 띄우는 짧은 알림(획득 · 레벨 업 · 오류)
    [Serializable]
    public class Toast
    {
        public string Text;
        public bool Error;
    }

    [Serializable]
    public class Req
    {
        public long Id;
        public string Name;
        public int Count;
        public string Text;
    }
}
