// 우편함 창이 그리는 우편 목록 꼴
using System;
using System.Collections.Generic;

namespace Unity.BossRoom.Gameplay.Showcase
{
    [Serializable]
    public class MailListView
    {
        public List<MailView> Mails = new();
    }

    [Serializable]
    public class MailView
    {
        public int Id;
        public string Sender;
        public string Subject;
        public string Body;
        public List<string> Items = new();
        public int Gold;
        public bool IsRead;
    }
}
