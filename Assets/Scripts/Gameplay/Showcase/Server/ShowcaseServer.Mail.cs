// 서버 쪽 우편함 — 운영툴 지급 우편을 읽고 받는다. 받을 때는 우편의 첨부를 먼저 비워 저장(운영툴에 「수령」 기록)한 뒤 가방 · 골드에 넣는다
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.BossRoom.Showcase;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static readonly Dictionary<int, List<(MailRow row, MailMessage mail)>> s_MailCache = new();
        static readonly Dictionary<long, ItemInstanceSaveData> s_Items = new();
        static float s_NextMailPoll;

        static void RegisterMailHandlers()
        {
            ShowcaseNet.OnServer("mail_list", (id, json) => { if (Of(id) is { } p) _ = SendMail(p); });
            ShowcaseNet.OnServer("mail_read", (id, json) => { if (Of(id) is { } p) _ = ReadMail(p, JsonUtility.FromJson<Req>(json)); });
            ShowcaseNet.OnServer("mail_take", (id, json) => { if (Of(id) is { } p) _ = TakeMail(p, JsonUtility.FromJson<Req>(json)); });
            ShowcaseNet.OnServer("mail_take_all", (id, json) => { if (Of(id) is { } p) _ = TakeAll(p); });
            ShowcaseNet.OnServer("mail_delete", (id, json) => { if (Of(id) is { } p) _ = DeleteMail(p, JsonUtility.FromJson<Req>(json)); });
        }

        static int UnreadMail(Profile p) => s_MailCache.TryGetValue(p.CharacterId, out var list) ? list.Count(m => !m.mail.IsRead || HasAttachment(m.mail)) : 0;

        static bool HasAttachment(MailMessage m) => m.CurrencyAmount > 0 || m.AttachmentSlots.Any(s => s.ItemInstanceIds.Count > 0);

        // 접속 중인 캐릭터의 우편을 30초마다 다시 읽는다 — 새 우편이 오면 알린다
        static void PollMail()
        {
            if (!GameApi.Enabled || Time.unscaledTime < s_NextMailPoll) return;
            s_NextMailPoll = Time.unscaledTime + 30f;
            foreach (var p in Profiles.Values.Where(p => p.Loaded && p.CharacterId > 0).ToList()) _ = RefreshMail(p, announce: true);
        }

        static async Task RefreshMail(Profile p, bool announce)
        {
            var res = await GameApi.Post("/api/getmailmessages", new CharacterIdRequest { PlayerCharacterId = p.CharacterId });
            if (!res.Ok) return;
            var before = s_MailCache.TryGetValue(p.CharacterId, out var old) ? old.Select(m => m.row.id).ToHashSet() : null;
            var list = JsonUtility.FromJson<MailList>(res.Text).mailMessages
                .Select(r => (r, JsonUtility.FromJson<MailMessage>(string.IsNullOrEmpty(r.saveData) ? "{}" : r.saveData) ?? new MailMessage())).ToList();
            s_MailCache[p.CharacterId] = list;
            var fresh = before == null ? 0 : list.Count(m => !before.Contains(m.r.id));
            if (announce && fresh > 0) Toast(p, $"새 우편 {fresh}통이 도착했습니다");
            if (Profiles.ContainsKey(p.ClientId)) Push(p);
        }

        static async Task SendMail(Profile p)
        {
            if (GameApi.Enabled) await RefreshMail(p, announce: false);
            var view = new MailListView();
            if (s_MailCache.TryGetValue(p.CharacterId, out var list))
            {
                await EnsureItems(list.SelectMany(m => m.mail.AttachmentSlots.SelectMany(s => s.ItemInstanceIds)));
                foreach (var (row, mail) in list.OrderByDescending(m => m.row.id))
                {
                    var v = new MailView { Id = row.id, Sender = mail.Sender, Subject = mail.Subject, Body = mail.Body, IsRead = mail.IsRead, Gold = mail.CurrencyAmount };
                    foreach (var id in mail.AttachmentSlots.SelectMany(s => s.ItemInstanceIds))
                    {
                        if (!s_Items.TryGetValue(id, out var item)) { v.Items.Add("알 수 없는 아이템"); continue; }
                        if (!string.IsNullOrEmpty(item.GainCurrencyName)) v.Gold += item.GainCurrencyAmount;
                        else v.Items.Add(item.ItemName);
                    }
                    view.Mails.Add(v);
                }
            }
            ShowcaseNet.ToClient(p.ClientId, "mail", view);
        }

        // 우편 첨부의 아이템 내용은 아이템 표에만 있다 — 모르는 번호가 나오면 표를 다시 읽는다
        static async Task EnsureItems(IEnumerable<long> ids)
        {
            if (ids.All(s_Items.ContainsKey)) return;
            var res = await GameApi.Post("/api/getiteminstances", null);
            if (!res.Ok) return;
            foreach (var row in JsonUtility.FromJson<ItemList>(res.Text).itemInstances)
            {
                if (string.IsNullOrEmpty(row.saveData)) continue;
                var item = JsonUtility.FromJson<ItemInstanceSaveData>(row.saveData);
                if (item != null) s_Items[row.itemInstanceId] = item;
            }
        }

        static (MailRow row, MailMessage mail)? FindMail(Profile p, int id) =>
            s_MailCache.TryGetValue(p.CharacterId, out var list) && list.Any(m => m.row.id == id) ? list.First(m => m.row.id == id) : null;

        static async Task ReadMail(Profile p, Req r)
        {
            if (FindMail(p, (int)r.Id) is not { } m || m.mail.IsRead) return;
            m.mail.IsRead = true;
            await GameApi.Post("/api/savemailmessage", new SaveDataRequest { Id = m.row.id, SaveData = JsonUtility.ToJson(m.mail) });
            await SendMail(p);
            Push(p);
        }

        static async Task TakeMail(Profile p, Req r)
        {
            await RefreshMail(p, announce: false);
            if (FindMail(p, (int)r.Id) is not { } m || !HasAttachment(m.mail)) { Toast(p, "받을 첨부가 없습니다", true); return; }
            var ids = m.mail.AttachmentSlots.SelectMany(s => s.ItemInstanceIds).ToList();
            await EnsureItems(ids);
            var gold = m.mail.CurrencyAmount;
            // 아이템 표에서 못 찾은 첨부는 잃지 않게 우편에 남긴다
            var missing = ids.Where(id => !s_Items.ContainsKey(id)).ToList();
            ids = ids.Where(s_Items.ContainsKey).ToList();
            if (ids.Count == 0 && gold == 0) { Toast(p, "첨부 정보를 읽지 못했습니다. 잠시 뒤 다시 시도해 주세요", true); return; }
            // 먼저 우편을 비워 저장한다 — 저장이 실패하면 아무것도 주지 않는다(두 번 받기 막기)
            m.mail.AttachmentSlots.Clear();
            if (missing.Count > 0) m.mail.AttachmentSlots.Add(new MailAttachmentSlot { ItemInstanceIds = missing });
            m.mail.CurrencyAmount = 0;
            m.mail.IsRead = true;
            var saved = await GameApi.Post("/api/savemailmessage", new SaveDataRequest { Id = m.row.id, SaveData = JsonUtility.ToJson(m.mail) });
            if (!saved.Ok) { Toast(p, "우편을 받지 못했습니다. 잠시 뒤 다시 시도해 주세요", true); await RefreshMail(p, false); return; }
            var names = new List<string>();
            foreach (var id in ids)
            {
                if (!s_Items.TryGetValue(id, out var item)) continue;
                if (!string.IsNullOrEmpty(item.GainCurrencyName)) { gold += item.GainCurrencyAmount; continue; }
                // 운영툴이 만든 아이템 번호를 그대로 가방에 넣는다 — 아이템 흐름 기록이 이어진다
                p.Bag.Add(new ItemInstanceSaveData { ItemName = item.ItemName, DisplayName = item.ItemName, ItemQuality = GameData.Item(item.ItemName).Grade.ToString(), ItemInstanceId = id });
                names.Add(item.ItemName);
            }
            p.Gold += gold;
            p.Dirty = true;
            await Persist(p);
            var parts = names.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key).ToList();
            if (gold > 0) parts.Add($"{gold:N0} 골드");
            Toast(p, "받음: " + string.Join(", ", parts));
            await SendMail(p);
            Push(p);
        }

        static async Task TakeAll(Profile p)
        {
            await RefreshMail(p, announce: false);
            var ids = s_MailCache.TryGetValue(p.CharacterId, out var list) ? list.Where(m => HasAttachment(m.mail)).Select(m => m.row.id).ToList() : new List<int>();
            if (ids.Count == 0) { Toast(p, "받을 첨부가 없습니다", true); return; }
            foreach (var id in ids) await TakeMail(p, new Req { Id = id });
        }

        static async Task DeleteMail(Profile p, Req r)
        {
            if (FindMail(p, (int)r.Id) is not { } m) return;
            if (HasAttachment(m.mail)) { Toast(p, "첨부를 먼저 받아 주세요", true); return; }
            await GameApi.Post("/api/deletemailmessage", new IdRequest { Id = m.row.id });
            await SendMail(p);
            Push(p);
        }
    }
}
