using System;
using UnityEngine;
using TMPro;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Simple popup panel to display information to players.
    /// </summary>
    public class PopupPanel : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI m_TitleText;
        [SerializeField]
        TextMeshProUGUI m_MainText;
        [SerializeField]
        GameObject m_ConfirmButton;
        [SerializeField]
        GameObject m_LoadingSpinner;
        [SerializeField]
        CanvasGroup m_CanvasGroup;

        public bool IsDisplaying => m_IsDisplaying;

        bool m_IsDisplaying;

        bool m_ClosableByUser;

        void Awake()
        {
            Hide();
        }

        public void OnConfirmClick()
        {
            if (m_ClosableByUser)
            {
                Hide();
            }
        }

        public void SetupPopupPanel(string titleText, string mainText, bool closeableByUser = true)
        {
            m_TitleText.text = titleText;
            m_MainText.text = mainText;
            m_ClosableByUser = closeableByUser;
            m_ConfirmButton.SetActive(m_ClosableByUser);
            m_LoadingSpinner.SetActive(!m_ClosableByUser);
            Show();
        }

        void Show()
        {
            gameObject.SetActive(true);
            m_CanvasGroup.alpha = 1f;
            m_CanvasGroup.blocksRaycasts = true;
            m_IsDisplaying = true;
        }

        public void Hide()
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.blocksRaycasts = false;
            m_IsDisplaying = false;
            // 투명하게만 두면 닫은 창(과 그 「확인」 버튼)이 켜진 채 남아, 화면을 읽는 쪽(자동 테스트 · 접근성)은 창이 아직 떠 있다고 본다
            gameObject.SetActive(false);
        }
    }
}
