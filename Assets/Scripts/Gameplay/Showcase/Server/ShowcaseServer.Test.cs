// 시험 주소 — 시험 서버(BOSSROOM_GM=1 · BOSSROOM_TEST_KEY)에서만 열리는 HTTP. 케이스가 폰 밖에서 봇(두 번째 접속자)을 켜고 시키고, 다음 게임 API 요청을 실패시키고, 계정을 되돌린다
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Showcase;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        [Serializable]
        class TestReq
        {
            public string bot, user, pass, cmd, path, status, mode, charName, detail;
            public int count, level;
            public long itemId;
            public bool all;
        }

        // 요청이 틀렸을 때 — 400 과 까닭을 돌려준다
        class TestError : Exception { public TestError(string m) : base(m) { } }

        class Bot
        {
            public string Name, User;
            public Process Proc;
            public ulong? ClientId;
            public int Seat = -1;
            public readonly List<string> Lines = new List<string>();
        }

        static readonly Dictionary<string, Bot> s_Bots = new Dictionary<string, Bot>();
        static HttpListener s_Http;
        static string TestKey => Environment.GetEnvironmentVariable("BOSSROOM_TEST_KEY");
        // 봇이 본 것 가운데 캐릭터 요약 · 친구 목록은 크고 자주 와서 뺀다 — 알림 · 채팅 · 초대만 남긴다
        static readonly HashSet<string> k_Quiet = new HashSet<string> { "profile", "social", "mail" };

        static void StartTestApi()
        {
            if (!GmEnabled || string.IsNullOrEmpty(TestKey) || s_Http != null) return;
            var port = int.TryParse(Environment.GetEnvironmentVariable("BOSSROOM_TEST_PORT"), out var p) ? p : 7780;
            ShowcaseLogin.BotToken = Guid.NewGuid().ToString("N");
            ShowcaseNet.Sent += (clientId, type, json) =>
            {
                if (k_Quiet.Contains(type)) return;
                var bot = s_Bots.Values.FirstOrDefault(b => b.ClientId == clientId);
                if (bot == null) return;
                bot.Lines.Add($"{{\"type\":{Q(type)},\"data\":{json}}}");
                if (bot.Lines.Count > 300) bot.Lines.RemoveAt(0);
            };
            Application.quitting += () => { foreach (var b in s_Bots.Values) Kill(b); };
            try
            {
                s_Http = new HttpListener();
                s_Http.Prefixes.Add($"http://*:{port}/");
                s_Http.Start();
                Listen();
                Debug.Log($"[test] 시험 주소 {port} 열림");
            }
            catch (Exception e)
            {
                s_Http = null;
                Debug.LogException(new Exception("[test] 시험 주소를 못 열었다", e));
            }
        }

        // 유니티 주 스레드에서 기다린다 — 이어지는 처리도 주 스레드라 게임 상태를 바로 만져도 된다
        static async void Listen()
        {
            while (s_Http != null && s_Http.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await s_Http.GetContextAsync(); }
                catch (Exception) { break; }
                _ = Handle(ctx);
            }
        }

        static async Task Handle(HttpListenerContext ctx)
        {
            var code = 200;
            string body;
            try
            {
                if (ctx.Request.Headers["X-Admin-Key"] != TestKey) throw new UnauthorizedAccessException();
                string text;
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) text = reader.ReadToEnd();
                var r = string.IsNullOrWhiteSpace(text) ? new TestReq() : JsonUtility.FromJson<TestReq>(text) ?? new TestReq();
                foreach (var k in ctx.Request.QueryString.AllKeys.Where(k => k == "bot")) r.bot = ctx.Request.QueryString[k];
                if (ctx.Request.QueryString["clear"] == "1") r.all = true;
                body = await Route(ctx.Request.Url.AbsolutePath.TrimEnd('/'), r);
            }
            catch (UnauthorizedAccessException) { code = 401; body = Err("X-Admin-Key 가 맞지 않습니다"); }
            catch (TestError e) { code = 400; body = Err(e.Message); }
            catch (Exception e) { code = 500; Debug.LogException(e); body = Err(e.Message); }
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = code;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.Close();
            }
            catch (Exception e) { Debug.LogWarning($"[test] 응답을 못 보냄: {e.Message}"); }
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
        static string Err(string m) => $"{{\"ok\":false,\"error\":{Q(m)}}}";
        const string k_Ok = "{\"ok\":true}";

        static string SessionName(ulong clientId) => SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId)?.PlayerName;
        static Profile OnlineUser(string user) => Profiles.Values.FirstOrDefault(p => p.Loaded && SessionName(p.ClientId) == user);

        static async Task<string> Route(string path, TestReq r)
        {
            switch (path)
            {
                case "/test/state":
                    var players = Profiles.Values.Where(p => p.Loaded).Select(p => $"{{\"user\":{Q(SessionName(p.ClientId))},\"name\":{Q(p.Name)},\"level\":{p.Data.CharacterLevel},\"bot\":{(s_Bots.Values.Any(b => b.ClientId == p.ClientId) ? "true" : "false")}}}");
                    return $"{{\"ok\":true,\"players\":[{string.Join(",", players)}],\"bots\":[{string.Join(",", s_Bots.Keys.Select(Q))}]}}";
                case "/test/bot/start": return await BotStart(r);
                case "/test/bot/do": await BotDo(BotOf(r), r.cmd ?? ""); return k_Ok;
                case "/test/bot/log":
                    var bot = BotOf(r);
                    var lines = string.Join(",", bot.Lines);
                    if (r.all) bot.Lines.Clear();
                    return $"{{\"ok\":true,\"lines\":[{lines}]}}";
                case "/test/bot/stop":
                    foreach (var b in r.all ? s_Bots.Values.ToList() : new List<Bot> { BotOf(r) }) Kill(b);
                    return k_Ok;
                case "/test/fail":
                    if (string.IsNullOrEmpty(r.path)) throw new TestError("path 가 비었습니다 — 실패시킬 게임 API 경로(예 /api/login)");
                    GameApi.FailNext[r.path] = Math.Max(1, r.count);
                    return k_Ok;
                case "/test/kick":
                    var online = OnlineUser(r.user) ?? throw new TestError($"{r.user} 는 접속 중이 아닙니다");
                    if (!Enum.TryParse<ConnectStatus>(r.status ?? "HostEndedSession", out var status)) throw new TestError($"모르는 status {r.status}");
                    if (online.Dirty) await Persist(online);
                    NetworkManager.Singleton.DisconnectClient(online.ClientId, ConnectReason.Encode(status, r.detail ?? ""));
                    return k_Ok;
                case "/test/reset": return await Reset(r);
                case "/test/mail": return await RawMail(r);
                default: throw new TestError($"모르는 주소 {path}");
            }
        }

        static Bot BotOf(TestReq r) => s_Bots.TryGetValue(r.bot ?? "", out var b) ? b : throw new TestError($"{r.bot} 봇이 없습니다 — /test/bot/start 로 먼저 켭니다");

        static void Kill(Bot b)
        {
            s_Bots.Remove(b.Name);
            if (b.ClientId is { } id && NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Contains(id)) NetworkManager.Singleton.DisconnectClient(id);
            try { if (!b.Proc.HasExited) b.Proc.Kill(); } catch (Exception) { }
        }

        // 계정이 있으면(user · pass) 그 계정으로, 없으면 이름만 가진 손님으로 들어온다. 캐릭터를 다 불러올 때까지 기다렸다가 돌려준다
        static async Task<string> BotStart(TestReq r)
        {
            var guest = string.IsNullOrEmpty(r.pass);
            var user = r.user ?? r.bot;
            var name = r.bot ?? r.user;
            if (string.IsNullOrEmpty(user) || user.Any(c => char.IsWhiteSpace(c) || c == '"')) throw new TestError("bot 또는 user 가 비었거나 빈칸 · 따옴표가 들어 있습니다");
            if (s_Bots.ContainsKey(name)) throw new TestError($"{name} 봇이 이미 켜져 있습니다");
            var args = Environment.GetCommandLineArgs();
            var port = Array.IndexOf(args, "-port") is var i && i >= 0 && i + 1 < args.Length ? args[i + 1] : "7770";
            var log = Path.Combine(Path.GetTempPath(), $"bossroom-bot-{name}.log");
            var pass = (guest ? ShowcaseLogin.BotToken : r.pass).Replace("\\", "\\\\").Replace("\"", "\\\"");
            var bot = new Bot { Name = name, User = user };
            bot.Proc = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,
                $"-batchmode -nographics -bot -connect 127.0.0.1:{port} -user {user} -pass \"{pass}\" -logFile \"{log}\"") { UseShellExecute = false, CreateNoWindow = true });
            s_Bots[name] = bot;
            var until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until && !bot.Proc.HasExited && s_Bots.ContainsKey(name))
            {
                if (OnlineUser(user) is { } p) { bot.ClientId = p.ClientId; return $"{{\"ok\":true,\"bot\":{Q(name)},\"name\":{Q(p.Name)}}}"; }
                await Task.Delay(250);
            }
            Kill(bot);
            throw new TestError($"{name} 봇이 60초 안에 접속하지 못했습니다 — 계정 · 비밀번호 · 접속자 수(8명) · 같은 계정 접속을 봅니다. 봇 기록 {log}");
        }

        // 봇이 화면에서 누른 것과 똑같이 서버 처리기를 탄다 — 「동작:값」 한 줄
        static async Task BotDo(Bot bot, string cmd)
        {
            var parts = cmd.Split(new[] { ':' }, 2);
            var verb = parts[0].Trim();
            var arg = parts.Length > 1 ? parts[1].Trim() : "";
            if (bot.ClientId is not { } id || Of(id) is not { } p) throw new TestError($"{bot.Name} 봇이 접속 중이 아닙니다");
            long Item() => p.Bag.FirstOrDefault(x => x.ItemName == arg)?.ItemInstanceId ?? 0;
            var req = new Req { Name = arg, Text = arg };
            var type = verb;
            switch (verb)
            {
                case "seat":
                case "ready":
                case "unready":
                    var lobby = UnityEngine.Object.FindAnyObjectByType<NetworkCharSelection>() ?? throw new TestError("지금은 로비(영웅 고르기)가 아닙니다");
                    if (int.TryParse(arg, out var seat)) bot.Seat = seat;
                    if (bot.Seat < 0) throw new TestError("자리를 먼저 고릅니다 — seat:0");
                    lobby.ServerChangeSeatRpc(id, bot.Seat, verb == "ready");
                    return;
                case "gm":
                    if (!TryGm(p, "/gm " + arg)) throw new TestError("GM 명령이 꺼져 있습니다");
                    return;
                case "buy": req.Count = 1; break;
                case "equip": case "unequip": case "sell": req.Id = Item(); break;
                case "claim": type = "quest_claim"; break;
                case "say": case "psay": case "gsay":
                    type = "chat";
                    req.Name = verb == "psay" ? "파티" : verb == "gsay" ? "길드" : "전체";
                    break;
                case "friend_remove": case "guild_kick": req.Id = await IdByName(arg); break;
            }
            if (!ShowcaseNet.InvokeServer(type, id, JsonUtility.ToJson(req))) throw new TestError($"모르는 동작 {verb}");
        }

        static async Task<(int account, List<PlayerCharacterRow> rows)> Characters(TestReq r, bool kick = true)
        {
            if (!GameApi.Enabled) throw new TestError("게임 API 가 안 붙은 서버입니다");
            var login = await GameApi.Post("/api/login", new LoginRequest { UserName = r.user, Password = r.pass }, auth: false);
            if (!login.Ok) throw new TestError($"{r.user} 로그인 실패({login.Code}) — user · pass 를 봅니다");
            var account = JsonUtility.FromJson<LoginResponse>(login.Text).accountId;
            // 접속 중이면 내보낸다 — 나가면서 저장하면 되돌린 것을 덮으니 저장할 것을 지운다
            if (kick && Profiles.Values.FirstOrDefault(p => p.AccountId == account) is { } online)
            {
                online.Dirty = false;
                NetworkManager.Singleton.DisconnectClient(online.ClientId, ConnectReason.Encode(ConnectStatus.HostEndedSession, "시험 주소가 계정을 되돌립니다"));
                for (var n = 0; n < 40 && Profiles.ContainsKey(online.ClientId); n++) await Task.Delay(100);
            }
            var res = await GameApi.Post("/api/getplayercharacters", new AccountIdRequest { AccountId = account });
            if (!res.Ok) throw new TestError($"캐릭터 읽기 실패({res.Code})");
            return (account, JsonUtility.FromJson<PlayerCharacterList>(res.Text).playerCharacters);
        }

        // mode new: 캐릭터를 지워 새 계정으로 · base: 기준 상태(레벨 1 · 골드 200 · 물약 3 · 친구 · 길드 없음) · raw: 이름 · 레벨을 검사 없이 그대로 넣는다
        static async Task<string> Reset(TestReq r)
        {
            var (_, rows) = await Characters(r);
            foreach (var row in rows)
            {
                if (r.mode == "new")
                {
                    await ClearSocial(row.id);
                    var del = await GameApi.Post("/api/deleteplayercharacter", new IdRequest { Id = row.id });
                    if (!del.Ok) throw new TestError($"캐릭터 지우기 실패({del.Code})");
                    continue;
                }
                var p = new Profile { CharacterId = row.id, Name = row.name };
                if (r.mode == "raw")
                {
                    if (!string.IsNullOrEmpty(row.saveData)) p.Save = JsonUtility.FromJson<CharacterSave>(row.saveData) ?? new CharacterSave();
                    if (r.level != 0) p.Data.CharacterLevel = r.level;
                    if (r.charName != null) p.Name = r.charName;
                }
                else if (r.mode == "base")
                {
                    await ClearSocial(row.id);
                    if (string.IsNullOrWhiteSpace(p.Name) || p.Name == "New Player") p.Name = r.user;
                    Starter(p);
                    p.Data.CharacterLevel = 1;
                }
                else throw new TestError("mode 는 new · base · raw 가운데 하나입니다");
                p.Data.CharacterId = row.id;
                var saved = await GameApi.Post("/api/saveplayercharacter", new SaveCharacterRequest { Id = row.id, Name = p.Name, SaveData = JsonUtility.ToJson(p.Save) });
                if (!saved.Ok) throw new TestError($"캐릭터 저장 실패({saved.Code})");
                s_Names[row.id] = (p.Name, p.Data.CharacterLevel);
            }
            return $"{{\"ok\":true,\"characters\":{rows.Count}}}";
        }

        // 친구 목록 · 친구 신청 · 길드에서 이 캐릭터를 뺀다 — 접속 중인 사람 화면도 다시 그린다
        static async Task ClearSocial(int characterId)
        {
            s_FriendRequests.RemoveAll(x => x.from == characterId || x.to == characterId);
            foreach (var list in s_FriendLists.Values.Where(l => l.PlayerIdList.Remove(characterId)).ToList()) await SaveFriends(list.PlayerCharacterId);
            if (s_FriendLists.TryGetValue(characterId, out var mine) && mine.PlayerIdList.Count > 0)
            {
                mine.PlayerIdList.Clear();
                await SaveFriends(characterId);
            }
            foreach (var g in s_Guilds.Values.Where(g => g.MemberList.Any(m => m.CharacterId == characterId)).ToList())
            {
                g.MemberList.RemoveAll(m => m.CharacterId == characterId);
                if (g.MemberList.Count == 0)
                {
                    s_Guilds.Remove(g.GuildId);
                    await GameApi.Post("/api/deleteguild", new IdRequest { Id = g.GuildId });
                    continue;
                }
                if (g.LeaderPlayerCharacterId == characterId) g.LeaderPlayerCharacterId = g.MemberList[0].CharacterId;
                await SaveGuild(g);
            }
            foreach (var o in Profiles.Values.Where(o => o.Loaded).ToList()) { PushSocial(o); Push(o); }
        }

        // 아이템 표에 없는 번호를 첨부로 단 우편 — 운영툴은 표에 있는 아이템만 보내 이 상황을 못 만든다
        static async Task<string> RawMail(TestReq r)
        {
            var (_, rows) = await Characters(r, kick: false);
            if (rows.Count == 0) throw new TestError($"{r.user} 에 캐릭터가 없습니다");
            var mail = new MailMessage { Sender = "시험", Recipient = rows[0].name, Subject = r.cmd ?? "시험 우편", Body = r.detail ?? "" };
            mail.AttachmentSlots.Add(new MailAttachmentSlot { ItemInstanceIds = new List<long> { r.itemId != 0 ? r.itemId : 990000000000000 + UnityEngine.Random.Range(0, 1000000) } });
            var res = await GameApi.Post("/api/createmailmessage", new CreateMailRequest { PlayerCharacterId = rows[0].id, SaveData = JsonUtility.ToJson(mail) });
            if (!res.Ok) throw new TestError($"우편 만들기 실패({res.Code})");
            return k_Ok;
        }
    }
}
