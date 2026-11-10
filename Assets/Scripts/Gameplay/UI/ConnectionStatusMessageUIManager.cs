using System;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Subscribes to connection status messages to display them through the popup panel.
    /// </summary>
    public class ConnectionStatusMessageUIManager : MonoBehaviour
    {
        DisposableGroup m_Subscriptions;

        PopupPanel m_CurrentReconnectPopup;

        [Inject]
        void InjectDependencies(ISubscriber<ConnectStatus> connectStatusSub, ISubscriber<ReconnectMessage> reconnectMessageSub)
        {
            m_Subscriptions = new DisposableGroup();
            m_Subscriptions.Add(connectStatusSub.Subscribe(OnConnectStatus));
            m_Subscriptions.Add(reconnectMessageSub.Subscribe(OnReconnectMessage));
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (m_Subscriptions != null)
            {
                m_Subscriptions.Dispose();
            }
        }

        void OnConnectStatus(ConnectStatus status)
        {
            switch (status)
            {
                case ConnectStatus.Undefined:
                case ConnectStatus.UserRequestedDisconnect:
                    break;
                case ConnectStatus.ServerFull:
                    PopupManager.ShowPopupPanel("접속 실패", "서버 인원이 가득 찼습니다. 잠시 뒤 다시 시도해 주세요");
                    break;
                case ConnectStatus.Success:
                    break;
                case ConnectStatus.LoggedInAgain:
                    PopupManager.ShowPopupPanel("접속 실패", "같은 계정으로 이미 접속 중입니다. 다른 기기의 접속을 끊고 다시 시도해 주세요");
                    break;
                case ConnectStatus.IncompatibleBuildType:
                    PopupManager.ShowPopupPanel("접속 실패", "게임 버전이 서버와 다릅니다. 최신 버전으로 업데이트해 주세요");
                    break;
                case ConnectStatus.GenericDisconnect:
                    PopupManager.ShowPopupPanel("연결 끊김", "서버와 연결이 끊어졌습니다");
                    break;
                case ConnectStatus.HostEndedSession:
                    PopupManager.ShowPopupPanel("연결 끊김", "서버가 게임을 종료했습니다");
                    break;
                case ConnectStatus.Reconnecting:
                    break;
                case ConnectStatus.StartHostFailed:
                    PopupManager.ShowPopupPanel("접속 실패", "방을 열지 못했습니다");
                    break;
                case ConnectStatus.StartClientFailed:
                    PopupManager.ShowPopupPanel("접속 실패", "서버에 연결하지 못했습니다. 네트워크를 확인해 주세요");
                    break;
                case ConnectStatus.LoginFailed:
                    PopupManager.ShowPopupPanel("로그인 실패", "아이디 또는 비밀번호가 맞지 않습니다");
                    break;
                case ConnectStatus.AccountSuspended:
                    PopupManager.ShowPopupPanel("이용 정지 계정", "운영 정책에 따라 이용이 정지된 계정입니다\n사유: " + Unity.BossRoom.Showcase.ShowcaseLogin.LastDetail);
                    break;
                case ConnectStatus.ServerMaintenance:
                    PopupManager.ShowPopupPanel("서버 점검 중", Unity.BossRoom.Showcase.ShowcaseLogin.LastDetail);
                    break;
                case ConnectStatus.ServerError:
                    PopupManager.ShowPopupPanel("접속 실패", "서버에 문제가 생겼습니다. 잠시 뒤 다시 시도해 주세요");
                    break;
                default:
                    Debug.LogWarning($"New ConnectStatus {status} has been added, but no connect message defined for it.");
                    break;
            }
        }

        void OnReconnectMessage(ReconnectMessage message)
        {
            if (message.CurrentAttempt == message.MaxAttempt)
            {
                CloseReconnectPopup();
            }
            else if (m_CurrentReconnectPopup != null)
            {
                m_CurrentReconnectPopup.SetupPopupPanel("연결 끊김", $"다시 연결하는 중…\n시도 {message.CurrentAttempt + 1}/{message.MaxAttempt}", closeableByUser: false);
            }
            else
            {
                m_CurrentReconnectPopup = PopupManager.ShowPopupPanel("연결 끊김", $"다시 연결하는 중…\n시도 {message.CurrentAttempt + 1}/{message.MaxAttempt}", closeableByUser: false);
            }
        }

        void CloseReconnectPopup()
        {
            if (m_CurrentReconnectPopup != null)
            {
                m_CurrentReconnectPopup.Hide();
                m_CurrentReconnectPopup = null;
            }
        }
    }
}
