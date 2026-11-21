using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Multiplayer.Samples.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Unity.BossRoom.DebugCheats
{
    /// <summary>
    /// Handles debug cheat events, applies them on the server and logs them on all clients. This class is only
    /// available in the editor or for development builds
    /// </summary>
    public class DebugCheatsManager : NetworkBehaviour
    {
        [SerializeField]
        GameObject m_DebugCheatsPanel;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField]
        [Tooltip("Enemy to spawn. Make sure this is included in the NetworkManager's list of prefabs!")]
        NetworkObject m_EnemyPrefab;

        [SerializeField]
        [Tooltip("Boss to spawn. Make sure this is included in the NetworkManager's list of prefabs!")]
        NetworkObject m_BossPrefab;

        [SerializeField]
        InputActionReference m_ToggleCheatsAction;

        SwitchedDoor m_SwitchedDoor;

        SwitchedDoor SwitchedDoor
        {
            get
            {
                if (m_SwitchedDoor == null)
                {
                    m_SwitchedDoor = FindAnyObjectByType<SwitchedDoor>();
                }

                return m_SwitchedDoor;
            }
        }

        bool m_DestroyPortalsOnNextToggle = true;

        [Inject]
        IPublisher<CheatUsedMessage> m_CheatUsedMessagePublisher;

        void Start()
        {
            m_ToggleCheatsAction.action.performed += OnToggleCheatsActionPerformed;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            m_ToggleCheatsAction.action.performed -= OnToggleCheatsActionPerformed;
        }

        void OnToggleCheatsActionPerformed(InputAction.CallbackContext obj)
        {
            m_DebugCheatsPanel.SetActive(!m_DebugCheatsPanel.activeSelf);
        }

        public void SpawnEnemy()
        {
            ServerSpawnEnemyRpc();
        }

        public void SpawnBoss()
        {
            ServerSpawnBossRpc();
        }

        public void KillTarget()
        {
            ServerKillTargetRpc();
        }

        public void KillAllEnemies()
        {
            ServerKillAllEnemiesRpc();
        }

        public void ToggleGodMode()
        {
            ServerToggleGodModeRpc();
        }

        public void HealPlayer()
        {
            ServerHealPlayerRpc();
        }

        public void ToggleSuperSpeed()
        {
            ServerToggleSuperSpeedRpc();
        }

        public void ToggleTeleportMode()
        {
            ServerToggleTeleportModeRpc();
        }

        public void ToggleDoor()
        {
            ServerToggleDoorRpc();
        }

        public void TogglePortals()
        {
            ServerTogglePortalsRpc();
        }

        public void GoToPostGame()
        {
            GoToPostGameServerRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerSpawnEnemyRpc(RpcParams serverRpcParams = default)
        {
            var newEnemy = Instantiate(m_EnemyPrefab);
            newEnemy.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId, true);
            PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "SpawnEnemy");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerSpawnBossRpc(RpcParams serverRpcParams = default)
        {
            var newEnemy = Instantiate(m_BossPrefab);
            newEnemy.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId, true);
            PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "SpawnBoss");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerKillTargetRpc(RpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
            var playerServerCharacter = PlayerServerCharacter.GetPlayerServerCharacter(clientId);
            if (playerServerCharacter != null)
            {
                var targetId = playerServerCharacter.TargetId.Value;
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject obj))
                {
                    var damageable = obj.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsDamageable())
                    {
                        damageable.ReceiveHitPoints(playerServerCharacter, int.MinValue);
                        PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "KillTarget");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"Target {targetId} has no IDamageable component or cannot be damaged.");
                    }
                }
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerKillAllEnemiesRpc(RpcParams serverRpcParams = default)
        {
            foreach (var serverCharacter in FindObjectsByType<ServerCharacter>(FindObjectsSortMode.None))
            {
                if (serverCharacter.IsNpc && serverCharacter.LifeState == LifeState.Alive)
                {
                    if (serverCharacter.gameObject.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.ReceiveHitPoints(null, -serverCharacter.HitPoints);
                    }
                }
            }

            PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "KillAllEnemies");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerToggleGodModeRpc(RpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            var playerServerCharacter = PlayerServerCharacter.GetPlayerServerCharacter(clientId);
            if (playerServerCharacter != null)
            {
                playerServerCharacter.NetLifeState.IsGodMode.Value = !playerServerCharacter.NetLifeState.IsGodMode.Value;
                PublishCheatUsedMessage(clientId, "ToggleGodMode");
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerHealPlayerRpc(RpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            var playerServerCharacter = PlayerServerCharacter.GetPlayerServerCharacter(clientId);
            if (playerServerCharacter != null)
            {
                var baseHp = playerServerCharacter.CharacterClass.BaseHP.Value;
                if (playerServerCharacter.LifeState == LifeState.Fainted)
                {
                    playerServerCharacter.Revive(null, baseHp);
                }
                else
                {
                    if (playerServerCharacter.gameObject.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.ReceiveHitPoints(null, baseHp);
                    }
                }

                PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "HealPlayer");
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerToggleSuperSpeedRpc(RpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            foreach (var playerServerCharacter in PlayerServerCharacter.GetPlayerServerCharacters())
            {
                if (playerServerCharacter.OwnerClientId == clientId)
                {
                    playerServerCharacter.Movement.SpeedCheatActivated = !playerServerCharacter.Movement.SpeedCheatActivated;
                    break;
                }
            }

            PublishCheatUsedMessage(clientId, "ToggleSuperSpeed");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerToggleTeleportModeRpc(RpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            foreach (var playerServerCharacter in PlayerServerCharacter.GetPlayerServerCharacters())
            {
                if (playerServerCharacter.OwnerClientId == clientId)
                {
                    playerServerCharacter.Movement.TeleportModeActivated = !playerServerCharacter.Movement.TeleportModeActivated;
                    break;
                }
            }

            PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "ToggleTeleportMode");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerToggleDoorRpc(RpcParams serverRpcParams = default)
        {
            if (SwitchedDoor != null)
            {
                SwitchedDoor.ForceOpen = !SwitchedDoor.ForceOpen;
                PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "ToggleDoor");
            }
            else
            {
                UnityEngine.Debug.Log("Could not activate ToggleDoor cheat. Door not found.");
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ServerTogglePortalsRpc(RpcParams serverRpcParams = default)
        {
            foreach (var portal in FindObjectsByType<EnemyPortal>(FindObjectsSortMode.None))
            {
                if (m_DestroyPortalsOnNextToggle)
                {
                    // This will only affect portals that are currently active in a scene and are currently loaded.
                    // Portals that are already destroyed will not be affected by this, and won't have their cooldown
                    // reinitialized.
                    portal.ForceDestroy();
                }
                else
                {
                    portal.ForceRestart();
                }
            }

            m_DestroyPortalsOnNextToggle = !m_DestroyPortalsOnNextToggle;
            PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "TogglePortals");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void GoToPostGameServerRpc(RpcParams serverRpcParams = default)
        {
            SceneLoaderWrapper.Instance.LoadScene("PostGame", useNetworkSceneManager: true);
            PublishCheatUsedMessage(serverRpcParams.Receive.SenderClientId, "GoToPostGame");
        }

        void PublishCheatUsedMessage(ulong clientId, string cheatUsed)
        {
            var playerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (playerData.HasValue)
            {
                m_CheatUsedMessagePublisher.Publish(new CheatUsedMessage(cheatUsed, playerData.Value.PlayerName));
            }
        }

        static void LogCheatNotImplemented(string cheat)
        {
            UnityEngine.Debug.Log($"Cheat {cheat} not implemented");
        }

#else
        void Awake()
        {
            // 행동 버튼 말풍선(UITooltipPopup)이 이 치트 창 안에 들어 있다 — 창째 끄면 릴리스 빌드에서 말풍선이 절대 안 뜬다. 끄기 전에 같은 캔버스로 꺼내 둔다
            foreach (var popup in m_DebugCheatsPanel.GetComponentsInChildren<Unity.BossRoom.Gameplay.UI.UITooltipPopup>(true))
                popup.transform.SetParent(m_DebugCheatsPanel.transform.parent, false);
            m_DebugCheatsPanel.SetActive(false);
        }
#endif
    }
}
