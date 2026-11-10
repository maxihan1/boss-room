// 게임 API(anymmo-webserver) 에 저장하는 캐릭터 · 아이템 · 우편 · 길드 데이터 꼴 — 운영툴이 같은 필드 이름으로 읽으므로 AnyRPG 이름을 그대로 쓴다
using System;
using System.Collections.Generic;

namespace Unity.BossRoom.Showcase
{
    [Serializable]
    public class CharacterSave
    {
        public CharacterSaveData CharacterSaveData = new CharacterSaveData();
        public ItemInstanceListSaveData ItemInstanceListSaveData = new ItemInstanceListSaveData();
        // Boss Room 에만 있는 진행 정보 — 운영툴은 읽지 않는다
        public ShowcaseProgress Showcase = new ShowcaseProgress();
    }

    [Serializable]
    public class CharacterSaveData
    {
        public int CharacterId;
        public string CharacterName = "";
        public int CharacterLevel = 1;
        public string CharacterClass = "";
        public string CharacterRace = "Human";
        public string CurrentScene = "";
        public string DataSavedOn = "";
        public List<CurrencySaveData> CurrencySaveData = new List<CurrencySaveData>();
    }

    [Serializable]
    public struct CurrencySaveData
    {
        public string CurrencyName;
        public int Amount;
    }

    [Serializable]
    public class ItemInstanceListSaveData
    {
        public List<ItemInstanceSaveData> ItemInstances = new List<ItemInstanceSaveData>();
    }

    [Serializable]
    public class ItemInstanceSaveData
    {
        public string ItemName = "";
        public string DisplayName = "";
        public string ItemQuality = "";
        public int DropLevel = 1;
        public long ItemInstanceId;
        public List<int> RandomSecondaryStatIndexes = new List<int>();
        public string GainCurrencyName = "";
        public int GainCurrencyAmount;
    }

    [Serializable]
    public class ShowcaseProgress
    {
        // Boss Room 에 처음 들어왔을 때 시작 지급을 한 번만 하려고 남긴다(예전 AnyMMO 캐릭터도 받는다)
        public bool Started;
        public int Exp;
        public long EquippedWeapon;
        public long EquippedArmor;
        public long EquippedAccessory;
        public string QuestDay = "";
        public List<QuestProgress> Quests = new List<QuestProgress>();
        public int TotalKills;
        public int BossKills;
        public int GuildId;
    }

    [Serializable]
    public class QuestProgress
    {
        public string QuestId;
        public int Count;
        public bool Rewarded;
    }

    [Serializable]
    public class MailMessage
    {
        public int MessageId;
        public string Sender = "";
        public string Recipient = "";
        public string Subject = "";
        public string Body = "";
        public List<MailAttachmentSlot> AttachmentSlots = new List<MailAttachmentSlot>();
        public int CurrencyAmount;
        public bool IsRead;
    }

    [Serializable]
    public class MailAttachmentSlot
    {
        public List<long> ItemInstanceIds = new List<long>();
    }

    [Serializable]
    public class GuildSaveData
    {
        public int GuildId;
        public string GuildName = "";
        public int LeaderPlayerCharacterId;
        public List<GuildMemberSaveData> MemberList = new List<GuildMemberSaveData>();
    }

    [Serializable]
    public class GuildMemberSaveData
    {
        public int CharacterId;
        public int Rank;
    }

    [Serializable]
    public class FriendListSaveData
    {
        public int PlayerCharacterId;
        public List<int> PlayerIdList = new List<int>();
    }
}
