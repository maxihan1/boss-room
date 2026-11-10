// 전용 서버(또는 호스트)가 접속한 플레이어마다 캐릭터를 불러오고 · 만들고 · 저장한다. 게임 API 가 없으면 메모리에만 둔다(로컬 시험)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Showcase;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class Profile
    {
        public ulong ClientId;
        public int AccountId;
        public int CharacterId;
        public string Name;
        public CharacterSave Save = new CharacterSave();
        public bool Dirty;
        public bool Loaded;
        public CharacterSaveData Data => Save.CharacterSaveData;
        public ShowcaseProgress P => Save.Showcase;
        public List<ItemInstanceSaveData> Bag => Save.ItemInstanceListSaveData.ItemInstances;

        public int Gold
        {
            get => Data.CurrencySaveData.Where(c => c.CurrencyName == GameData.Gold).Sum(c => c.Amount);
            set
            {
                Data.CurrencySaveData.RemoveAll(c => c.CurrencyName == GameData.Gold);
                Data.CurrencySaveData.Add(new CurrencySaveData { CurrencyName = GameData.Gold, Amount = Math.Max(0, value) });
            }
        }

        public ItemDef Equipped(long id) => id == 0 ? null : Bag.FirstOrDefault(i => i.ItemInstanceId == id) is { } it ? GameData.Item(it.ItemName) : null;
        IEnumerable<ItemDef> Gear => new[] { Equipped(P.EquippedWeapon), Equipped(P.EquippedArmor), Equipped(P.EquippedAccessory) }.Where(d => d != null);
        public float AttackMult => 1 + GameData.LevelBonus(Data.CharacterLevel) + Gear.Sum(d => d.Attack);
        public float Defense => Mathf.Min(0.6f, GameData.LevelDefense(Data.CharacterLevel) + Gear.Sum(d => d.Defense));
        public float GoldMult => 1 + Gear.Sum(d => d.GoldBonus);
        public float ExpMult => 1 + Gear.Sum(d => d.ExpBonus);
    }

    public static partial class ShowcaseServer
    {
        public static readonly Dictionary<ulong, Profile> Profiles = new();
        static float s_NextSave;
        const long k_Generator = 7; // 운영툴은 1023 — 같은 밀리초에 겹치지 않게 다른 번호를 쓴다
        static long s_LastMs = -1, s_Seq;
        static readonly DateTime k_Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted += () =>
            {
                Profiles.Clear();
                nm.OnConnectionEvent -= OnConnection;
                nm.OnConnectionEvent += OnConnection;
                RegisterHandlers();
                Ticker.Ensure();
            };
        }

        static void OnConnection(NetworkManager nm, ConnectionEventData e)
        {
            if (!nm.IsServer) return;
            if (e.EventType == ConnectionEvent.ClientConnected) _ = Load(e.ClientId);
            else if (e.EventType == ConnectionEvent.ClientDisconnected) _ = Unload(e.ClientId);
        }

        static async Task Load(ulong clientId)
        {
            var session = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            var name = session?.PlayerName ?? "모험가";
            var profile = new Profile { ClientId = clientId, AccountId = session?.AccountId ?? 0, Name = name };
            Profiles[clientId] = profile;
            try
            {
                if (GameApi.Enabled && profile.AccountId > 0)
                {
                    var res = await GameApi.Post("/api/getplayercharacters", new AccountIdRequest { AccountId = profile.AccountId });
                    var row = res.Ok ? JsonUtility.FromJson<PlayerCharacterList>(res.Text).playerCharacters.FirstOrDefault() : null;
                    if (row != null)
                    {
                        profile.CharacterId = row.id;
                        // AnyMMO 가 기본 이름으로 만든 캐릭터는 아이디로 이름을 바꾼다
                        if (!string.IsNullOrWhiteSpace(row.name) && row.name != "New Player") profile.Name = row.name;
                        if (!string.IsNullOrEmpty(row.saveData)) profile.Save = JsonUtility.FromJson<CharacterSave>(row.saveData) ?? new CharacterSave();
                        if (!profile.P.Started) Starter(profile, keepSave: true);
                    }
                    else
                    {
                        Starter(profile);
                        var created = await GameApi.Post("/api/createplayercharacter", new CreateCharacterRequest { AccountId = profile.AccountId, Name = profile.Name, SaveData = JsonUtility.ToJson(profile.Save) });
                        if (!created.Ok) throw new InvalidOperationException($"캐릭터를 만들지 못했습니다 ({created.Code})");
                        profile.CharacterId = JsonUtility.FromJson<IdResponse>(created.Text).id;
                    }
                }
                else
                {
                    Starter(profile);
                }
                Normalize(profile);
                profile.Loaded = true;
                if (GameApi.Enabled && profile.CharacterId > 0) await RefreshMail(profile, announce: false);
                profile.Dirty = true;
                await AfterLoad(profile);
                Push(profile);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowcaseNet.ToClient(clientId, "toast", new Toast { Text = "캐릭터 정보를 불러오지 못했습니다", Error = true });
            }
        }

        // 새 캐릭터 기본 지급 — 처음 상점에서 하나는 살 수 있게
        static void Starter(Profile p, bool keepSave = false)
        {
            if (!keepSave) p.Save = new CharacterSave();
            p.Save.Showcase ??= new ShowcaseProgress();
            p.P.Started = true;
            p.Data.CharacterName = p.Name;
            p.Gold = Math.Max(p.Gold, 200);
            AddItem(p, "체력 물약", 3);
        }

        static void Normalize(Profile p)
        {
            p.Save.CharacterSaveData ??= new CharacterSaveData();
            p.Save.ItemInstanceListSaveData ??= new ItemInstanceListSaveData();
            p.Save.Showcase ??= new ShowcaseProgress();
            p.Data.CharacterId = p.CharacterId;
            p.Data.CharacterName = p.Name;
            p.Data.CharacterLevel = Mathf.Clamp(p.Data.CharacterLevel, 1, GameData.MaxLevel);
            ResetDailyIfNeeded(p);
        }

        static async Task Unload(ulong clientId)
        {
            if (!Profiles.TryGetValue(clientId, out var p)) return;
            Profiles.Remove(clientId);
            await OnLeft(p);
            if (p.Loaded && p.Dirty) await Persist(p);
        }

        public static void Tick()
        {
            PollMail();
            if (Time.unscaledTime < s_NextSave) return;
            s_NextSave = Time.unscaledTime + 10f;
            foreach (var p in Profiles.Values.Where(p => p.Loaded && p.Dirty).ToList()) _ = Persist(p);
        }

        static async Task Persist(Profile p)
        {
            p.Dirty = false;
            p.Data.DataSavedOn = DateTime.UtcNow.ToString("o");
            if (!GameApi.Enabled || p.CharacterId == 0) return;
            var res = await GameApi.Post("/api/saveplayercharacter", new SaveCharacterRequest { Id = p.CharacterId, Name = p.Name, SaveData = JsonUtility.ToJson(p.Save) });
            if (!res.Ok) p.Dirty = true;
        }

        public static Profile Of(ulong clientId) => Profiles.TryGetValue(clientId, out var p) && p.Loaded ? p : null;

        public static void Push(Profile p)
        {
            var v = new ProfileView
            {
                Name = p.Name, Level = p.Data.CharacterLevel, Exp = p.P.Exp, ExpNext = GameData.ExpToNext(p.Data.CharacterLevel), Gold = p.Gold,
                Items = p.Bag.Where(i => string.IsNullOrEmpty(i.GainCurrencyName)).Select(i => new ItemView { Id = i.ItemInstanceId, Name = i.ItemName }).ToList(),
                Weapon = p.P.EquippedWeapon, Armor = p.P.EquippedArmor, Accessory = p.P.EquippedAccessory,
                Quests = QuestViews(p), UnreadMail = UnreadMail(p), Guild = GuildName(p), Persistent = GameApi.Enabled,
            };
            ShowcaseNet.ToClient(p.ClientId, "profile", v);
        }

        public static void Toast(Profile p, string text, bool error = false) => ShowcaseNet.ToClient(p.ClientId, "toast", new Toast { Text = text, Error = error });

        public static long NewItemId()
        {
            long ms = (long)(DateTime.UtcNow - k_Epoch).TotalMilliseconds;
            if (ms < s_LastMs) ms = s_LastMs;
            if (ms == s_LastMs) { s_Seq = (s_Seq + 1) & 0xFFF; if (s_Seq == 0) ms = ++s_LastMs; }
            else s_Seq = 0;
            s_LastMs = ms;
            return (ms << 22) | (k_Generator << 12) | s_Seq;
        }

        // 아이템을 가방에 넣고 게임 API 아이템 표에도 남긴다(운영툴 「아이템 이름 힌트」와 흐름 기록이 이 표를 본다)
        public static void AddItem(Profile p, string name, int count = 1, bool log = true)
        {
            for (var i = 0; i < count; i++)
            {
                var item = new ItemInstanceSaveData { ItemName = name, DisplayName = name, ItemQuality = GameData.Item(name).Grade.ToString(), ItemInstanceId = NewItemId() };
                p.Bag.Add(item);
                if (log && GameApi.Enabled) _ = GameApi.Post("/api/createiteminstance", new CreateItemRequest { ItemInstanceId = item.ItemInstanceId, SaveData = JsonUtility.ToJson(item) });
            }
            p.Dirty = true;
        }

        [Serializable] class CreateItemRequest { public long ItemInstanceId; public string SaveData; }
    }
}
