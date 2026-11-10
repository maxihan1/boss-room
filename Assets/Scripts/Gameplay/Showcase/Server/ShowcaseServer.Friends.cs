// 서버 쪽 친구 · 파티 — 친구는 게임 API 친구 목록에 양쪽으로 저장하고, 파티는 서버 메모리에만 둔다(최대 4명, 파티원 1명당 경험치 +10%)
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.BossRoom.Showcase;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static readonly Dictionary<int, FriendListSaveData> s_FriendLists = new();
        static readonly List<(int from, int to)> s_FriendRequests = new();
        static readonly List<List<ulong>> s_Parties = new(); // [0] 이 파티장
        // 초대한 사람 이름도 같이 둔다 — 초대한 사람이 나간 뒤 수락해도 초대를 찾아 「나갔습니다」를 알릴 수 있게
        static readonly List<(string kind, ulong from, string fromName, ulong to)> s_Invites = new();
        const int k_PartyMax = 4;

        static void RegisterFriendHandlers()
        {
            ShowcaseNet.OnServer("friend_add", (id, json) => { if (Of(id) is { } p) _ = FriendAdd(p, Name(json)); });
            ShowcaseNet.OnServer("friend_accept", (id, json) => { if (Of(id) is { } p) _ = FriendAnswer(p, Name(json), true); });
            ShowcaseNet.OnServer("friend_decline", (id, json) => { if (Of(id) is { } p) _ = FriendAnswer(p, Name(json), false); });
            ShowcaseNet.OnServer("friend_remove", (id, json) => { if (Of(id) is { } p) _ = FriendRemove(p, (int)JsonUtility.FromJson<Req>(json).Id); });
            ShowcaseNet.OnServer("party_invite", (id, json) => { if (Of(id) is { } p) _ = PartyInvite(p, Name(json)); });
            ShowcaseNet.OnServer("party_accept", (id, json) => { if (Of(id) is { } p) PartyAnswer(p, Name(json), true); });
            ShowcaseNet.OnServer("party_decline", (id, json) => { if (Of(id) is { } p) PartyAnswer(p, Name(json), false); });
            ShowcaseNet.OnServer("party_leave", (id, json) => { if (Of(id) is { } p) LeaveParty(p, quiet: false); });
        }

        static string Name(string json) => (JsonUtility.FromJson<Req>(json)?.Name ?? "").Trim();

        static async Task LoadFriends()
        {
            var res = await GameApi.Post("/api/getfriendlists", null);
            if (!res.Ok) return;
            foreach (var row in JsonUtility.FromJson<FriendLists>(res.Text).friendLists)
            {
                var list = string.IsNullOrEmpty(row.saveData) ? null : JsonUtility.FromJson<FriendListSaveData>(row.saveData);
                s_FriendLists[row.playerCharacterId] = list ?? new FriendListSaveData { PlayerCharacterId = row.playerCharacterId };
            }
        }

        static List<int> FriendIds(Profile p) => s_FriendLists.TryGetValue(p.CharacterId, out var l) ? l.PlayerIdList : new List<int>();

        static FriendListSaveData FriendList(int characterId)
        {
            if (!s_FriendLists.TryGetValue(characterId, out var l)) s_FriendLists[characterId] = l = new FriendListSaveData { PlayerCharacterId = characterId };
            return l;
        }

        static Task SaveFriends(int characterId) => !GameApi.Enabled ? Task.CompletedTask
            : GameApi.Post("/api/savefriendlist", new SaveFriendListRequest { PlayerCharacterId = characterId, SaveData = JsonUtility.ToJson(FriendList(characterId)) });

        static async Task FriendAdd(Profile p, string name)
        {
            var targetId = await IdByName(name);
            var target = Online(targetId);
            if (targetId == 0) { Toast(p, "그런 이름의 캐릭터가 없습니다", true); return; }
            if (targetId == p.CharacterId) { Toast(p, "자신에게는 신청할 수 없습니다", true); return; }
            if (FriendIds(p).Contains(targetId)) { Toast(p, "이미 친구입니다", true); return; }
            if (target == null) { Toast(p, "접속 중인 캐릭터에게만 신청할 수 있습니다", true); return; }
            if (!s_FriendRequests.Contains((p.CharacterId, targetId))) s_FriendRequests.Add((p.CharacterId, targetId));
            ShowcaseNet.ToClient(target.ClientId, "invite", new Invite { Kind = "friend", From = p.Name, Text = $"{p.Name} 님이 친구 신청을 했습니다" });
            Toast(p, $"{target.Name} 님에게 친구 신청을 보냈습니다");
            PushSocial(target);
        }

        static async Task FriendAnswer(Profile p, string fromName, bool accept)
        {
            var fromId = await IdByName(fromName);
            if (!s_FriendRequests.Remove((fromId, p.CharacterId))) return;
            var from = Online(fromId);
            if (accept)
            {
                if (!FriendList(p.CharacterId).PlayerIdList.Contains(fromId)) FriendList(p.CharacterId).PlayerIdList.Add(fromId);
                if (!FriendList(fromId).PlayerIdList.Contains(p.CharacterId)) FriendList(fromId).PlayerIdList.Add(p.CharacterId);
                await SaveFriends(p.CharacterId);
                await SaveFriends(fromId);
                Toast(p, $"{fromName} 님과 친구가 되었습니다");
                if (from != null) Toast(from, $"{p.Name} 님과 친구가 되었습니다");
            }
            else if (from != null) Toast(from, $"{p.Name} 님이 친구 신청을 거절했습니다");
            PushSocial(p);
            if (from != null) PushSocial(from);
        }

        static async Task FriendRemove(Profile p, int friendId)
        {
            FriendList(p.CharacterId).PlayerIdList.Remove(friendId);
            FriendList(friendId).PlayerIdList.Remove(p.CharacterId);
            await SaveFriends(p.CharacterId);
            await SaveFriends(friendId);
            PushSocial(p);
            if (Online(friendId) is { } f) PushSocial(f);
        }

        static List<ulong> PartyOf(Profile p) => s_Parties.FirstOrDefault(pt => pt.Contains(p.ClientId));

        static float PartyBonus(Profile p) => 1f + 0.1f * ((PartyOf(p)?.Count ?? 1) - 1);

        static async Task PartyInvite(Profile p, string name)
        {
            var target = Online(await IdByName(name));
            if (target == null) { Toast(p, "접속 중인 캐릭터만 초대할 수 있습니다", true); return; }
            if (target == p) { Toast(p, "자신은 초대할 수 없습니다", true); return; }
            if (PartyOf(target) != null) { Toast(p, $"{target.Name} 님은 이미 파티에 있습니다", true); return; }
            if ((PartyOf(p)?.Count ?? 1) >= k_PartyMax) { Toast(p, "파티는 최대 4명입니다", true); return; }
            s_Invites.RemoveAll(i => i.kind == "party" && i.to == target.ClientId);
            s_Invites.Add(("party", p.ClientId, p.Name, target.ClientId));
            ShowcaseNet.ToClient(target.ClientId, "invite", new Invite { Kind = "party", From = p.Name, Text = $"{p.Name} 님이 파티에 초대했습니다" });
            Toast(p, $"{target.Name} 님을 파티에 초대했습니다");
        }

        static void PartyAnswer(Profile p, string fromName, bool accept)
        {
            var invite = s_Invites.FirstOrDefault(i => i.kind == "party" && i.to == p.ClientId && i.fromName == fromName);
            if (invite.kind == null) return;
            s_Invites.Remove(invite);
            var from = Of(invite.from);
            if (from == null) { Toast(p, "초대한 사람이 나갔습니다", true); return; }
            if (!accept) { Toast(from, $"{p.Name} 님이 파티 초대를 거절했습니다"); return; }
            var party = PartyOf(from);
            if (party == null) s_Parties.Add(party = new List<ulong> { from.ClientId });
            if (party.Count >= k_PartyMax) { Toast(p, "파티 인원이 가득 찼습니다", true); return; }
            LeaveParty(p, quiet: true);
            party.Add(p.ClientId);
            foreach (var m in party.Select(id => Of(id)).Where(x => x != null).ToList())
            {
                Toast(m, $"{p.Name} 님이 파티에 들어왔습니다");
                PushSocial(m);
            }
        }

        static void LeaveParty(Profile p, bool quiet)
        {
            var party = PartyOf(p);
            if (party == null) return;
            party.Remove(p.ClientId);
            if (party.Count <= 1) s_Parties.Remove(party);
            foreach (var m in party.Select(id => Of(id)).Where(x => x != null).ToList())
            {
                Toast(m, party.Count <= 1 ? "파티가 해산되었습니다" : $"{p.Name} 님이 파티를 떠났습니다");
                PushSocial(m);
            }
            if (!quiet) { Toast(p, "파티를 떠났습니다"); PushSocial(p); }
        }
    }
}
