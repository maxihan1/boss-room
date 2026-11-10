// 게임 API 요청 · 응답 꼴 — JsonUtility 로 바로 읽고 쓴다
using System;
using System.Collections.Generic;

namespace Unity.BossRoom.Showcase
{
    [Serializable] public class LoginRequest { public string UserName; public string Password; }
    [Serializable] public class LoginResponse { public int accountId; public string token; }
    [Serializable] public class LoginDenied { public string code; public string message; public string until; }
    [Serializable] public class AccountIdRequest { public int AccountId; }
    [Serializable] public class CreateCharacterRequest { public int AccountId; public string Name; public string SaveData; }
    [Serializable] public class SaveCharacterRequest { public int Id; public string Name; public string SaveData; }
    [Serializable] public class IdResponse { public int id; }
    [Serializable] public class PlayerCharacterRow { public int id; public int accountId; public string name; public string saveData; }
    [Serializable] public class PlayerCharacterList { public List<PlayerCharacterRow> playerCharacters = new List<PlayerCharacterRow>(); }

    [Serializable] public class CharacterIdRequest { public int PlayerCharacterId; }
    [Serializable] public class MailIdRequest { public int MessageId; }
    [Serializable] public class IdRequest { public int Id; }
    [Serializable] public class SaveDataRequest { public int Id; public string SaveData; }
    [Serializable] public class CreateMailRequest { public int PlayerCharacterId; public string SaveData; }
    [Serializable] public class MailRow { public int id; public int playerCharacterId; public string saveData; }
    [Serializable] public class MailList { public List<MailRow> mailMessages = new List<MailRow>(); }
    [Serializable] public class ItemRow { public int id; public long itemInstanceId; public string saveData; }
    [Serializable] public class ItemList { public List<ItemRow> itemInstances = new List<ItemRow>(); }

    [Serializable] public class GuildRow { public int id; public string name; public string saveData; }
    [Serializable] public class GuildList { public List<GuildRow> guilds = new List<GuildRow>(); }
    [Serializable] public class SaveDataOnly { public string SaveData; }
    [Serializable] public class FriendRow { public int id; public int playerCharacterId; public string saveData; }
    [Serializable] public class FriendLists { public List<FriendRow> friendLists = new List<FriendRow>(); }
    [Serializable] public class SaveFriendListRequest { public int PlayerCharacterId; public string SaveData; }

    [Serializable] public class ServerStatus { public MaintenanceInfo maintenance = new MaintenanceInfo(); public List<NoticeInfo> notices = new List<NoticeInfo>(); public string serverTime; }
    [Serializable] public class MaintenanceInfo { public bool active; public string message; public string startsAt; public string endsAt; }
    [Serializable] public class NoticeInfo { public int id; public string title; public string body; public string startsAt; public string endsAt; }

    [Serializable] public class AccountRestrictions { public int accountId; public bool suspended; public string suspendedUntil; public string suspendReason; public bool chatMuted; public string chatMutedUntil; public string chatMuteReason; public bool maintenanceBlocked; }
}
