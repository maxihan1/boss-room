// 메인 메뉴의 로그인 창 — 아이디 · 비밀번호를 받아 시연 게임 서버(443 wss)로 접속한다. 거절 사유는 접속 안내 창이 보여 준다
using System.Collections;
using TMPro;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Showcase;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class LoginScreen : MonoBehaviour
    {
        const string k_SavedUser = "showcase.user";

        TMP_InputField m_User;
        TMP_InputField m_Pass;
        Button m_Start;
        TextMeshProUGUI m_Status;

        public static LoginScreen Create()
        {
            var canvas = UIKit.Canvas("Showcase Login", 100);
            var screen = canvas.gameObject.AddComponent<LoginScreen>();
            screen.Build(canvas.transform);
            return screen;
        }

        void Build(Transform root)
        {
            NoticeBar.Create(root);

            var panel = UIKit.Panel(root, "로그인", new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(720, 400));
            m_User = UIKit.Input(panel, "아이디", new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(560, 84));
            m_Pass = UIKit.Input(panel, "비밀번호", new Vector2(0.5f, 1f), new Vector2(0, -216), new Vector2(560, 84), password: true);
            m_Start = UIKit.Button(panel, "게임 시작", new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(560, 96), OnStart, 40);
            m_Status = UIKit.Label(panel, "", 24, UIKit.Dim, new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(620, 36), TextAlignmentOptions.Center);

            UIKit.Label(root, "v0.1 · Unity Boss Room 기반", 22, new Color(1, 1, 1, 0.45f), new Vector2(1f, 0f), new Vector2(-190, 30), new Vector2(360, 30), TextAlignmentOptions.MidlineRight);

            m_User.text = PlayerPrefs.GetString(k_SavedUser, ShowcaseLogin.UserName);
            m_Pass.text = ShowcaseLogin.Password;
            m_User.onSubmit.AddListener(_ => m_Pass.ActivateInputField());
            m_Pass.onSubmit.AddListener(_ => OnStart());
        }

        void OnStart()
        {
            var user = m_User.text.Trim();
            if (user.Length == 0 || m_Pass.text.Length == 0)
            {
                m_Status.color = UIKit.Red;
                m_Status.text = "아이디와 비밀번호를 입력해 주세요";
                return;
            }
            PlayerPrefs.SetString(k_SavedUser, user);
            ShowcaseLogin.UserName = user;
            ShowcaseLogin.Password = m_Pass.text;
            m_Start.interactable = false;
            m_Status.color = UIKit.Dim;
            m_Status.text = "서버에 접속하는 중…";
            FindAnyObjectByType<ConnectionManager>().StartClientIp(user, ShowcaseLogin.ServerHost, ShowcaseLogin.ServerPort);
            StartCoroutine(WaitForResult());
        }

        // 접속에 실패하면 네트워크가 꺼진다 — 그때 버튼을 다시 살린다(사유는 접속 안내 창이 띄운다)
        IEnumerator WaitForResult()
        {
            yield return new WaitForSeconds(1f);
            while (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.ShutdownInProgress)) yield return null;
            m_Start.interactable = true;
            m_Status.text = "";
        }
    }
}
