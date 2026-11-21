using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to when a client is attempting to connect to a server. Starts the client when
    /// entering. If successful, transitions to the ClientConnected state. If not, transitions to the Offline state.
    /// </summary>
    class ClientConnectingState : OnlineState
    {
        protected ConnectionMethodBase m_ConnectionMethod;

        public ClientConnectingState Configure(ConnectionMethodBase baseConnectionMethod)
        {
            m_ConnectionMethod = baseConnectionMethod;
            return this;
        }

        public override void Enter()
        {
#pragma warning disable 4014
            ConnectClientAsync();
#pragma warning restore 4014
        }

        public override void Exit() { }

        public override void OnClientConnected(ulong _)
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
        }

        public override void OnClientDisconnect(ulong _)
        {
            // client ID is for sure ours here
            StartingClientFailed();
        }

        void StartingClientFailed()
        {
            var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
            if (string.IsNullOrEmpty(disconnectReason))
            {
                m_ConnectStatusPublisher.Publish(ConnectStatus.StartClientFailed);
            }
            else
            {
                var connectStatus = ConnectReason.Decode(disconnectReason);
                m_ConnectStatusPublisher.Publish(connectStatus);
            }

            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        internal void ConnectClientAsync()
        {
            try
            {
                m_ConnectionMethod.SetupClientConnection();

                if (m_ConnectionMethod is ConnectionMethodIP)
                {
                    if (!m_ConnectionManager.NetworkManager.StartClient())
                    {
                        throw new Exception("NetworkManager StartClient failed");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error connecting client, see following exception");
                Debug.LogException(e);
                // 다시 연결하는 중(ClientReconnectingState)이면 한 번의 실패로 세고 남은 시도를 이어 간다 — 바로 StartingClientFailed 로 가면
                // 네트워크가 막 돌아오는 순간 StartClient 가 실패할 때 남은 시도 없이 「접속 실패」 창과 로그인 화면으로 끝났다
                OnClientDisconnect(0);
                throw;
            }
        }
    }
}
