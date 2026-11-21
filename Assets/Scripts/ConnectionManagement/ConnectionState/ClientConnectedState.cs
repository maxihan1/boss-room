using Unity.BossRoom.UnityServices.Sessions;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a connected client. When being disconnected, transitions to the
    /// ClientReconnecting state if no reason is given, or to the Offline state.
    /// </summary>
    class ClientConnectedState : OnlineState
    {
        [Inject]
        protected MultiplayerServicesFacade m_MultiplayerServicesFacade;

        public override void Enter()
        {
            if (m_MultiplayerServicesFacade.CurrentUnitySession != null)
            {
                m_MultiplayerServicesFacade.BeginTracking();
            }
        }

        public override void Exit() { }

        public override void OnClientDisconnect(ulong _)
        {
            var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
            // 와이파이가 끊기는 등 통신이 끊기면 NGO 가 평문 사유를 넣는다 — 그것도 다시 연결한다. 서버가 일부러 끊은 것(JSON 사유)만 바로 안내하고 나간다
            if (!ConnectReason.IsServerReason(disconnectReason))
            {
                m_ConnectStatusPublisher.Publish(ConnectStatus.Reconnecting);
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientReconnecting);
            }
            else
            {
                var connectStatus = ConnectReason.Decode(disconnectReason);
                m_ConnectStatusPublisher.Publish(connectStatus);
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
            }
        }
    }
}
