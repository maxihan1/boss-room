// 서버 쪽 길드 — 500 골드로 만들고, 길드장이 초대 · 추방, 누구나 탈퇴. 게임 API 길드 표에 저장한다
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.BossRoom.Showcase;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static readonly Dictionary<int, GuildSaveData> s_Guilds = new();
        const int k_GuildCost = 500;
        static int s_LocalGuildId = 100000; // 게임 API 가 없을 때(로컬 호스트) 쓰는 번호
        // 길드 목록을 다 읽기 전에는 캐릭터의 길드 번호를 지우지 않는다 — 서버를 켠 직후 첫 접속자의 길드가 사라지던 버그
        static bool s_GuildsLoaded;

        static void RegisterGuildHandlers()
        {
            ShowcaseNet.OnServer("guild_create", (id, json) => { if (Of(id) is { } p) _ = GuildCreate(p, Name(json)); });
            ShowcaseNet.OnServer("guild_invite", (id, json) => { if (Of(id) is { } p) _ = GuildInvite(p, Name(json)); });
            ShowcaseNet.OnServer("guild_accept", (id, json) => { if (Of(id) is { } p) _ = GuildAnswer(p, Name(json), true); });
            ShowcaseNet.OnServer("guild_decline", (id, json) => { if (Of(id) is { } p) _ = GuildAnswer(p, Name(json), false); });
            ShowcaseNet.OnServer("guild_leave", (id, json) => { if (Of(id) is { } p) _ = GuildLeave(p, p.CharacterId); });
            ShowcaseNet.OnServer("guild_kick", (id, json) => { if (Of(id) is { } p) _ = GuildKick(p, (int)JsonUtility.FromJson<Req>(json).Id); });
        }

        static async Task LoadGuilds()
        {
            var res = await GameApi.Post("/api/getguilds", null);
            if (!res.Ok) return;
            foreach (var row in JsonUtility.FromJson<GuildList>(res.Text).guilds)
            {
                var g = string.IsNullOrEmpty(row.saveData) ? null : JsonUtility.FromJson<GuildSaveData>(row.saveData);
                if (g == null) continue;
                g.GuildId = row.id;
                s_Guilds[row.id] = g;
            }
            s_GuildsLoaded = true;
        }

        static GuildSaveData GuildOf(Profile p)
        {
            if (p.P.GuildId != 0 && s_Guilds.TryGetValue(p.P.GuildId, out var g) && g.MemberList.Any(m => m.CharacterId == p.CharacterId)) return g;
            if (!s_GuildsLoaded && GameApi.Enabled) return null;
            // 길드 명단이 정본이다 — 명단에 있으면 캐릭터 쪽 번호를 되살리고, 없으면(해산 · 추방) 비운다
            var found = s_Guilds.Values.FirstOrDefault(x => x.MemberList.Any(m => m.CharacterId == p.CharacterId));
            var id = found?.GuildId ?? 0;
            if (p.P.GuildId != id) { p.P.GuildId = id; p.Dirty = true; }
            return found;
        }

        static string GuildName(Profile p) => GuildOf(p)?.GuildName ?? "";

        static Task SaveGuild(GuildSaveData g) => !GameApi.Enabled ? Task.CompletedTask
            : GameApi.Post("/api/saveguild", new SaveDataRequest { Id = g.GuildId, SaveData = JsonUtility.ToJson(g) });

        static void FillGuild(Profile p, SocialView v)
        {
            var g = GuildOf(p);
            if (g == null) return;
            v.GuildName = g.GuildName;
            v.GuildLeader = g.LeaderPlayerCharacterId == p.CharacterId;
            v.Guild = g.MemberList.Select(m => { var x = MemberOf(m.CharacterId); x.Leader = m.CharacterId == g.LeaderPlayerCharacterId; return x; })
                .OrderByDescending(m => m.Leader).ThenByDescending(m => m.Online).ToList();
        }

        static void PushGuild(GuildSaveData g)
        {
            foreach (var o in Profiles.Values.Where(o => o.Loaded && g.MemberList.Any(m => m.CharacterId == o.CharacterId)).ToList()) { PushSocial(o); Push(o); }
        }

        static async Task GuildCreate(Profile p, string name)
        {
            if (GuildOf(p) != null) { Toast(p, "이미 길드에 들어가 있습니다", true); return; }
            if (name.Length < 2 || name.Length > 12) { Toast(p, "길드 이름은 2~12자로 지어 주세요", true); return; }
            if (s_Guilds.Values.Any(g => g.GuildName == name)) { Toast(p, "이미 있는 길드 이름입니다", true); return; }
            if (p.Gold < k_GuildCost) { Toast(p, $"길드를 만들려면 {k_GuildCost} 골드가 필요합니다", true); return; }
            var guild = new GuildSaveData { GuildName = name, LeaderPlayerCharacterId = p.CharacterId, MemberList = { new GuildMemberSaveData { CharacterId = p.CharacterId, Rank = 1 } } };
            if (GameApi.Enabled)
            {
                var res = await GameApi.Post("/api/createguild", new SaveDataOnly { SaveData = JsonUtility.ToJson(guild) });
                if (!res.Ok) { Toast(p, "길드를 만들지 못했습니다", true); return; }
                guild.GuildId = JsonUtility.FromJson<IdResponse>(res.Text).id;
                await SaveGuild(guild);
            }
            else guild.GuildId = ++s_LocalGuildId;
            s_Guilds[guild.GuildId] = guild;
            p.Gold -= k_GuildCost;
            p.P.GuildId = guild.GuildId;
            p.Dirty = true;
            Toast(p, $"길드 창설 완료: 「{name}」");
            PushSocial(p);
            Push(p);
        }

        static async Task GuildInvite(Profile p, string name)
        {
            var g = GuildOf(p);
            if (g == null || g.LeaderPlayerCharacterId != p.CharacterId) { Toast(p, "길드장만 초대할 수 있습니다", true); return; }
            var target = Online(await IdByName(name));
            if (target == null) { Toast(p, "접속 중인 캐릭터만 초대할 수 있습니다", true); return; }
            if (GuildOf(target) != null) { Toast(p, $"{target.Name} 님은 이미 길드에 있습니다", true); return; }
            s_Invites.RemoveAll(i => i.kind == "guild" && i.to == target.ClientId);
            s_Invites.Add(("guild", p.ClientId, p.Name, target.ClientId));
            ShowcaseNet.ToClient(target.ClientId, "invite", new Invite { Kind = "guild", From = p.Name, Text = $"{p.Name} 님이 길드 「{g.GuildName}」에 초대했습니다" });
            Toast(p, $"{target.Name} 님을 길드에 초대했습니다");
        }

        static async Task GuildAnswer(Profile p, string fromName, bool accept)
        {
            var invite = s_Invites.FirstOrDefault(i => i.kind == "guild" && i.to == p.ClientId && i.fromName == fromName);
            if (invite.kind == null) return;
            s_Invites.Remove(invite);
            var from = Of(invite.from);
            var g = from == null ? null : GuildOf(from);
            if (g == null) { Toast(p, "초대한 길드를 찾지 못했습니다", true); return; }
            if (!accept) { Toast(from, $"{p.Name} 님이 길드 초대를 거절했습니다"); return; }
            if (GuildOf(p) != null) { Toast(p, "이미 길드에 들어가 있습니다", true); return; }
            g.MemberList.Add(new GuildMemberSaveData { CharacterId = p.CharacterId });
            p.P.GuildId = g.GuildId;
            p.Dirty = true;
            await SaveGuild(g);
            foreach (var o in Profiles.Values.Where(o => o.Loaded && o.P.GuildId == g.GuildId).ToList()) Toast(o, $"{p.Name} 님이 길드에 들어왔습니다");
            PushGuild(g);
        }

        static async Task GuildLeave(Profile p, int characterId)
        {
            var g = GuildOf(p);
            if (g == null) return;
            g.MemberList.RemoveAll(m => m.CharacterId == characterId);
            var who = MemberOf(characterId).Name;
            if (Online(characterId) is { } left) { left.P.GuildId = 0; left.Dirty = true; PushSocial(left); Push(left); }
            if (g.MemberList.Count == 0)
            {
                s_Guilds.Remove(g.GuildId);
                if (GameApi.Enabled) await GameApi.Post("/api/deleteguild", new IdRequest { Id = g.GuildId });
                return;
            }
            if (g.LeaderPlayerCharacterId == characterId) g.LeaderPlayerCharacterId = g.MemberList[0].CharacterId;
            await SaveGuild(g);
            foreach (var o in Profiles.Values.Where(o => o.Loaded && o.P.GuildId == g.GuildId).ToList()) Toast(o, $"{who} 님이 길드를 떠났습니다");
            PushGuild(g);
        }

        static async Task GuildKick(Profile p, int characterId)
        {
            var g = GuildOf(p);
            if (g == null || g.LeaderPlayerCharacterId != p.CharacterId || characterId == p.CharacterId) return;
            if (Online(characterId) is { } kicked) Toast(kicked, $"길드 「{g.GuildName}」에서 추방되었습니다", true);
            await GuildLeave(p, characterId);
        }
    }
}
