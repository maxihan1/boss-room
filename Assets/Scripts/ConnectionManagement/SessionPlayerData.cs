using Unity.BossRoom.Infrastructure;
using Unity.Multiplayer.Samples.BossRoom;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
    public struct SessionPlayerData : ISessionPlayerData
    {
        public string PlayerName;
        public int PlayerNumber;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        /// Instead of using a NetworkGuid (two ulongs) we could just use an int or even a byte-sized index into an array of possible avatars defined in our game data source
        public NetworkGuid AvatarNetworkGuid;
        public int CurrentHitPoints;
        public bool HasCharacterSpawned;
        // 게임 API 계정 번호 — 전용 서버가 로그인 뒤 채운다(호스트 모드면 0)
        public int AccountId;

        public SessionPlayerData(ulong clientID, string name, NetworkGuid avatarNetworkGuid, int currentHitPoints = 0, bool isConnected = false, bool hasCharacterSpawned = false)
        {
            ClientID = clientID;
            PlayerName = name;
            PlayerNumber = -1;
            PlayerPosition = Vector3.zero;
            PlayerRotation = Quaternion.identity;
            AvatarNetworkGuid = avatarNetworkGuid;
            CurrentHitPoints = currentHitPoints;
            IsConnected = isConnected;
            HasCharacterSpawned = hasCharacterSpawned;
            AccountId = 0;
        }

        public bool IsConnected { get; set; }
        public ulong ClientID { get; set; }

        public void Reinitialize()
        {
            HasCharacterSpawned = false;
        }
    }
}
