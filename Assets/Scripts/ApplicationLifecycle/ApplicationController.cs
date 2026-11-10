using System;
using System.Collections;
using Unity.BossRoom.ApplicationLifecycle.Messages;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.UnityServices;
using Unity.BossRoom.UnityServices.Auth;
using Unity.BossRoom.UnityServices.Sessions;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Unity.BossRoom.ApplicationLifecycle
{
    /// <summary>
    /// An entry point to the application, where we bind all the common dependencies to the root DI scope.
    /// </summary>
    public class ApplicationController : LifetimeScope
    {
        [SerializeField]
        UpdateRunner m_UpdateRunner;
        [SerializeField]
        ConnectionManager m_ConnectionManager;
        [SerializeField]
        NetworkManager m_NetworkManager;

        LocalSession m_LocalSession;
        MultiplayerServicesFacade m_MultiplayerServicesFacade;

        IDisposable m_Subscriptions;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(m_UpdateRunner);
            builder.RegisterComponent(m_ConnectionManager);
            builder.RegisterComponent(m_NetworkManager);

            // The following singletons represent the local representations of the Session that we're in and the user that we are
            // They can persist longer than the lifetime of the UI in MainMenu where we set up the Session that we create or join
            builder.Register<LocalSessionUser>(Lifetime.Singleton);
            builder.Register<LocalSession>(Lifetime.Singleton);

            builder.Register<ProfileManager>(Lifetime.Singleton);

            builder.Register<PersistentGameState>(Lifetime.Singleton);

            // These message channels are essential and persist for the lifetime of the Session and relay services
            // Registering as instance to prevent code stripping on iOS
            builder.RegisterInstance(new MessageChannel<QuitApplicationMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<UnityServiceErrorMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<ConnectStatus>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<DoorStateChangedEventMessage>()).AsImplementedInterfaces();

            // These message channels are essential and persist for the lifetime of the Session and relay services
            // They are networked so that the clients can subscribe to those messages that are published by the server
            builder.RegisterComponent(new NetworkedMessageChannel<LifeStateChangedEventMessage>()).AsImplementedInterfaces();
            builder.RegisterComponent(new NetworkedMessageChannel<ConnectionEventMessage>()).AsImplementedInterfaces();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterComponent(new NetworkedMessageChannel<CheatUsedMessage>()).AsImplementedInterfaces();
#endif

            // This message channel is essential and persists for the lifetime of the Session and relay services
            builder.RegisterInstance(new MessageChannel<ReconnectMessage>()).AsImplementedInterfaces();

            // Buffered message channels hold the latest received message in buffer and pass to any new subscribers
            builder.RegisterInstance(new BufferedMessageChannel<SessionListFetchedMessage>()).AsImplementedInterfaces();

            // All the Session service stuff, bound here so that it persists through scene loads
            builder.Register<AuthenticationServiceFacade>(Lifetime.Singleton); //a manager entity that allows us to do anonymous authentication with unity services

            // MultiplayerServicesFacade is registered as entrypoint because it wants a callback after container is built to do it's initialization
            builder.RegisterEntryPoint<MultiplayerServicesFacade>(Lifetime.Singleton).AsSelf();
        }

        private void Start()
        {
            m_LocalSession = Container.Resolve<LocalSession>();
            m_MultiplayerServicesFacade = Container.Resolve<MultiplayerServicesFacade>();

            var quitApplicationSub = Container.Resolve<ISubscriber<QuitApplicationMessage>>();

            var subHandles = new DisposableGroup();
            subHandles.Add(quitApplicationSub.Subscribe(QuitGame));
            m_Subscriptions = subHandles;

            Application.wantsToQuit += OnWantToQuit;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(m_UpdateRunner.gameObject);
            Application.targetFrameRate = 120;

            // 시연 서버 · 화면 없는 시험 접속기는 메인 메뉴를 건너뛴다 — 명령줄 인자로 고른다
            var port = int.TryParse(ArgValue("-port"), out var p) ? p : 7770;
            // 로컬 시험용 — 접속할 게임 서버와 계정을 명령줄로 덮어쓴다
            var gameServer = ArgValue("-gameServer");
            if (gameServer != null)
            {
                var hp = gameServer.Split(':');
                Unity.BossRoom.Showcase.ShowcaseLogin.ServerHost = hp[0];
                Unity.BossRoom.Showcase.ShowcaseLogin.ServerPort = hp.Length > 1 ? int.Parse(hp[1]) : 443;
            }
            Unity.BossRoom.Showcase.ShowcaseLogin.UserName = ArgValue("-user") ?? "";
            Unity.BossRoom.Showcase.ShowcaseLogin.Password = ArgValue("-pass") ?? "";
            if (ArgValue("-tlsCa") is { } caFile) Unity.BossRoom.Showcase.ShowcaseLogin.TestCa = System.IO.File.ReadAllText(caFile);
            if (HasArg("-server"))
            {
                Application.targetFrameRate = 30;
                StartCoroutine(AfterFirstFrame(() => m_ConnectionManager.StartServerIp("0.0.0.0", port)));
                return;
            }
            if (ArgValue("-actions") != null) Unity.BossRoom.Gameplay.Showcase.ClientScript.Run(ArgValue("-actions"));
            if (HasArg("-autoplay") || ArgValue("-shots") != null)
            {
                var pilot = new GameObject("ShowcaseAutopilot").AddComponent<ShowcaseAutopilot>();
                pilot.AutoPlay = HasArg("-autoplay");
                pilot.Seat = int.TryParse(ArgValue("-seat"), out var seat) ? seat : 0;
                pilot.ShotDir = ArgValue("-shots");
                pilot.UiTour = HasArg("-uiTour");
            }
            if (float.TryParse(ArgValue("-quitAfter"), out var quitAfter))
            {
                StartCoroutine(QuitAfter(quitAfter));
            }
            if (HasArg("-host"))
            {
                StartCoroutine(AfterFirstFrame(() => m_ConnectionManager.StartHostIp(ArgValue("-name") ?? "host", "127.0.0.1", port)));
                return;
            }
            var connect = ArgValue("-connect");
            if (connect != null)
            {
                var hostPort = connect.Split(':');
                var name = ArgValue("-name") ?? "bot";
                StartCoroutine(AfterFirstFrame(() => m_ConnectionManager.StartClientIp(name, hostPort[0], hostPort.Length > 1 ? int.Parse(hostPort[1]) : port)));
                return;
            }
            SceneManager.LoadScene("MainMenu");
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // ConnectionManager.Start 가 콜백을 걸고 난 뒤에 부르려고 한 프레임 미룬다
        static IEnumerator AfterFirstFrame(Action action)
        {
            yield return null;
            action();
        }

        static IEnumerator QuitAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Debug.Log($"[smoke] {seconds}초 지나 끝냄 — 장면 {SceneManager.GetActiveScene().name}");
            Application.Quit(0);
        }

        protected override void OnDestroy()
        {
            if (m_Subscriptions != null)
            {
                m_Subscriptions.Dispose();
            }

            if (m_MultiplayerServicesFacade != null)
            {
                m_MultiplayerServicesFacade.EndTracking();
            }

            base.OnDestroy();
        }

        /// <summary>
        ///     In builds, if we are in a Session and try to send a Leave request on application quit, it won't go through if we're quitting on the same frame.
        ///     So, we need to delay just briefly to let the request happen (though we don't need to wait for the result).
        /// </summary>
        private IEnumerator LeaveBeforeQuit()
        {
            // We want to quit anyways, so if anything happens while trying to leave the Session, log the exception then carry on
            try
            {
                m_MultiplayerServicesFacade.EndTracking();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            yield return null;
            Application.Quit();
        }

        private bool OnWantToQuit()
        {
            Application.wantsToQuit -= OnWantToQuit;

            var canQuit = m_LocalSession != null && string.IsNullOrEmpty(m_LocalSession.SessionID);
            if (!canQuit)
            {
                StartCoroutine(LeaveBeforeQuit());
            }

            return canQuit;
        }

        private void QuitGame(QuitApplicationMessage msg)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
