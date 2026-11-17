// 시연 기능(가방 · 상점 · 우편 · 채팅 …)이 같이 쓰는 서버 ↔ 클라이언트 통로 — NGO 이름 붙은 메시지에 「종류 + JSON」을 실어 보낸다. 프리팹을 고치지 않아도 된다
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static class ShowcaseNet
    {
        const string k_Channel = "showcase";
        static readonly Dictionary<string, Action<ulong, string>> s_ServerHandlers = new();
        static readonly Dictionary<string, Action<string>> s_ClientHandlers = new();
        static NetworkManager s_Registered;

        public static void OnServer(string type, Action<ulong, string> handler) => s_ServerHandlers[type] = handler;
        public static void OnClient(string type, Action<string> handler) => s_ClientHandlers[type] = handler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted += () => Register(nm);
            nm.OnClientStarted += () => Register(nm);
        }

        static void Register(NetworkManager nm)
        {
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(k_Channel);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(k_Channel, Receive);
            s_Registered = nm;
        }

        static void Receive(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string type);
            reader.ReadValueSafe(out string json);
            var nm = NetworkManager.Singleton;
            try
            {
                if (nm.IsServer && sender != NetworkManager.ServerClientId)
                {
                    if (s_ServerHandlers.TryGetValue(type, out var h)) h(sender, json);
                }
                else if (s_ClientHandlers.TryGetValue(type, out var c))
                {
                    c(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(new Exception($"[showcase] {type} 처리 실패", e));
            }
        }

        // 서버가 한 클라이언트에 보낸 것을 엿듣는다 — 시험 주소가 「봇이 본 알림」을 돌려주는 데 쓴다
        public static event Action<ulong, string, string> Sent;

        // 시험 주소가 봇 대신 요청을 넣는다 — 봇이 보낸 것과 똑같이 서버 처리기를 탄다
        public static bool InvokeServer(string type, ulong clientId, string json)
        {
            if (!s_ServerHandlers.TryGetValue(type, out var h)) return false;
            h(clientId, json);
            return true;
        }

        // 클라이언트 → 서버
        public static void ToServer(string type, object payload = null) => Send(NetworkManager.ServerClientId, type, payload);

        // 서버 → 한 클라이언트. 호스트 자신이면 바로 처리한다
        public static void ToClient(ulong clientId, string type, object payload)
        {
            var nm = NetworkManager.Singleton;
            Sent?.Invoke(clientId, type, Json(payload));
            if (nm.IsHost && clientId == nm.LocalClientId)
            {
                if (s_ClientHandlers.TryGetValue(type, out var c)) c(Json(payload));
                return;
            }
            Send(clientId, type, payload);
        }

        public static void ToAll(string type, object payload)
        {
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds) ToClient(id, type, payload);
        }

        static void Send(ulong target, string type, object payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            var json = Json(payload);
            // 호스트가 자기 자신에게 보내는 요청은 메시지를 거치지 않는다
            if (nm.IsHost && target == NetworkManager.ServerClientId)
            {
                if (s_ServerHandlers.TryGetValue(type, out var h)) h(nm.LocalClientId, json);
                return;
            }
            var size = FastBufferWriter.GetWriteSize(type) + FastBufferWriter.GetWriteSize(json);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe(json);
            nm.CustomMessagingManager.SendNamedMessage(k_Channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        static string Json(object payload) => payload == null ? "{}" : payload as string ?? JsonUtility.ToJson(payload);
    }
}
