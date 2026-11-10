// 서버 쪽 채팅 · 제재 반영 · 캐릭터 이름 지도 — 운영툴에서 채팅 금지를 걸면 채팅을 막고, 정지를 걸면 30초 안에 사유와 함께 접속을 끊는다
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Showcase;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        // 캐릭터 번호 → (이름, 레벨) — 접속 안 한 친구 · 길드원 이름을 보여 주려고
        static readonly Dictionary<int, (string name, int level)> s_Names = new();
        static readonly Dictionary<int, (AccountRestrictions r, float at)> s_Restrictions = new();
        static float s_NextNames, s_NextRestrictionPoll;
        static bool s_SocialLoaded;

        static void RegisterSocialHandlers()
        {
            ShowcaseNet.OnServer("chat", (id, json) => { if (Of(id) is { } p) _ = Chat(p, JsonUtility.FromJson<Req>(json)); });
            ShowcaseNet.OnServer("social", (id, json) => { if (Of(id) is { } p) PushSocial(p); });
            RegisterFriendHandlers();
            RegisterGuildHandlers();
        }

        static async Task AfterLoad(Profile p)
        {
            if (!s_SocialLoaded) { s_SocialLoaded = true; await LoadSocial(); }
            s_Names[p.CharacterId] = (p.Name, p.Data.CharacterLevel);
            SystemChat($"{p.Name} 님이 접속했습니다");
            RefreshSocialAround(p);
        }

        static Task OnLeft(Profile p)
        {
            LeaveParty(p, quiet: true);
            s_Invites.RemoveAll(i => i.to == p.ClientId);
            SystemChat($"{p.Name} 님이 나갔습니다");
            foreach (var other in Profiles.Values.Where(o => o.Loaded)) PushSocial(other);
            return Task.CompletedTask;
        }

        static async Task LoadSocial()
        {
            if (!GameApi.Enabled) return;
            await RefreshNames(force: true);
            await LoadFriends();
            await LoadGuilds();
            // 읽는 동안 이미 들어온 사람에게 길드 · 친구를 다시 보내 준다
            foreach (var p in Profiles.Values.Where(p => p.Loaded).ToList()) { Push(p); PushSocial(p); }
        }

        static async Task RefreshNames(bool force = false)
        {
            if (!GameApi.Enabled || (!force && Time.unscaledTime < s_NextNames)) return;
            s_NextNames = Time.unscaledTime + 60f;
            var res = await GameApi.Post("/api/getallplayercharacters", null);
            if (!res.Ok) return;
            foreach (var row in JsonUtility.FromJson<PlayerCharacterList>(res.Text).playerCharacters)
            {
                var level = 1;
                try { level = JsonUtility.FromJson<CharacterSave>(row.saveData ?? "{}")?.CharacterSaveData?.CharacterLevel ?? 1; } catch (Exception) { }
                s_Names[row.id] = (row.name, level);
            }
        }

        static async Task<int> IdByName(string name)
        {
            name = (name ?? "").Trim();
            var online = Profiles.Values.FirstOrDefault(p => p.Loaded && p.Name == name);
            if (online != null) return online.CharacterId;
            await RefreshNames(force: true);
            return s_Names.FirstOrDefault(kv => kv.Value.name == name).Key;
        }

        static Profile Online(int characterId) => Profiles.Values.FirstOrDefault(p => p.Loaded && p.CharacterId == characterId && characterId != 0);

        static Member MemberOf(int characterId)
        {
            var online = Online(characterId);
            if (online != null) return new Member { Id = characterId, Name = online.Name, Level = online.Data.CharacterLevel, Online = true };
            var (name, level) = s_Names.TryGetValue(characterId, out var n) ? n : ($"#{characterId}", 1);
            return new Member { Id = characterId, Name = name, Level = level };
        }

        static async Task Chat(Profile p, Req r)
        {
            var text = (r.Text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > 100) text = text.Substring(0, 100);
            var rest = await Restrictions(p);
            if (rest != null && rest.chatMuted)
            {
                var until = string.IsNullOrEmpty(rest.chatMutedUntil) ? "해제할 때까지" : rest.chatMutedUntil.Replace('T', ' ').Substring(0, 16) + " (UTC) 까지";
                Toast(p, $"채팅이 금지된 계정입니다 · {until} · 사유: {rest.chatMuteReason}", true);
                return;
            }
            var channel = r.Name is "파티" or "길드" ? r.Name : "전체";
            var line = new ChatLine { Channel = channel, From = p.Name, Text = text };
            IEnumerable<Profile> to = channel switch
            {
                "파티" => PartyOf(p)?.Select(id => Of(id)).Where(x => x != null) ?? new[] { p },
                "길드" => p.P.GuildId == 0 ? new[] { p } : Profiles.Values.Where(o => o.Loaded && o.P.GuildId == p.P.GuildId),
                _ => Profiles.Values.Where(o => o.Loaded),
            };
            if (channel != "전체" && to.Count() <= 1 && (channel == "파티" ? PartyOf(p) == null : p.P.GuildId == 0))
            {
                Toast(p, channel == "파티" ? "파티에 들어가 있지 않습니다" : "길드에 들어가 있지 않습니다", true);
                return;
            }
            foreach (var o in to.ToList()) ShowcaseNet.ToClient(o.ClientId, "chat", line);
        }

        static void SystemChat(string text)
        {
            foreach (var o in Profiles.Values.Where(o => o.Loaded).ToList()) ShowcaseNet.ToClient(o.ClientId, "chat", new ChatLine { Channel = "시스템", Text = text });
        }

        static async Task<AccountRestrictions> Restrictions(Profile p, bool fresh = false)
        {
            if (!GameApi.Enabled || p.AccountId == 0) return null;
            if (!fresh && s_Restrictions.TryGetValue(p.AccountId, out var c) && Time.unscaledTime - c.at < 20f) return c.r;
            var res = await GameApi.Post("/api/getaccountrestrictions", new AccountIdRequest { AccountId = p.AccountId });
            if (!res.Ok) return null;
            var r = JsonUtility.FromJson<AccountRestrictions>(res.Text);
            s_Restrictions[p.AccountId] = (r, Time.unscaledTime);
            return r;
        }

        // 접속 중 정지 · 점검 차단이 걸리면 사유와 함께 내보낸다
        static void PollRestrictions()
        {
            if (!GameApi.Enabled || Time.unscaledTime < s_NextRestrictionPoll) return;
            s_NextRestrictionPoll = Time.unscaledTime + 30f;
            foreach (var p in Profiles.Values.Where(p => p.Loaded).ToList()) _ = CheckKick(p);
        }

        static async Task CheckKick(Profile p)
        {
            var r = await Restrictions(p, fresh: true);
            if (r == null || (!r.suspended && !r.maintenanceBlocked)) return;
            var status = r.suspended ? ConnectStatus.AccountSuspended : ConnectStatus.ServerMaintenance;
            var detail = r.suspended ? r.suspendReason : (await ServerMaintenanceMessage());
            if (p.Dirty) await Persist(p);
            NetworkManager.Singleton.DisconnectClient(p.ClientId, ConnectReason.Encode(status, detail));
        }

        static async Task<string> ServerMaintenanceMessage()
        {
            var res = await GameApi.Send("GET", "/api/serverstatus", null, auth: false);
            return res.Ok ? JsonUtility.FromJson<ServerStatus>(res.Text)?.maintenance?.message ?? "서버 점검 중입니다" : "서버 점검 중입니다";
        }

        static void RefreshSocialAround(Profile p)
        {
            foreach (var o in Profiles.Values.Where(o => o.Loaded).ToList()) PushSocial(o);
        }

        static void PushSocial(Profile p)
        {
            var v = new SocialView();
            v.Friends = FriendIds(p).Select(MemberOf).OrderByDescending(m => m.Online).ThenBy(m => m.Name).ToList();
            v.FriendRequests = s_FriendRequests.Where(x => x.to == p.CharacterId).Select(x => MemberOf(x.from).Name).ToList();
            var party = PartyOf(p);
            if (party != null) v.Party = party.Select(id => Of(id)).Where(x => x != null).Select(x => new Member { Id = x.CharacterId, Name = x.Name, Level = x.Data.CharacterLevel, Online = true, Leader = s_Parties.First(pt => pt.Contains(p.ClientId))[0] == x.ClientId }).ToList();
            FillGuild(p, v);
            ShowcaseNet.ToClient(p.ClientId, "social", v);
        }
    }
}
