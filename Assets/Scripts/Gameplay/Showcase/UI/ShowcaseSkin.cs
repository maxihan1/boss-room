// 시연 화면이 쓰는 그림 · 글꼴 묶음 — Boss Room UI 재료를 모아 Resources 에서 바로 꺼내 쓴다(에디터 설정 스크립트가 채운다)
using TMPro;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class ShowcaseSkin : ScriptableObject
    {
        public Sprite Panel;
        public Sprite TitleBar;
        public Sprite Button;
        public Sprite ButtonDisabled;
        public Sprite Input;
        public Sprite Soft;
        public Sprite HealthBar;
        public Sprite HealthBarBg;
        public Sprite Logo;
        // 탱커 · 궁수 · 마법사 · 도적 차례
        public Sprite[] ClassIcons;
        public TMP_FontAsset Body;
        public TMP_FontAsset Title;

        static ShowcaseSkin s_Instance;
        public static ShowcaseSkin I => s_Instance != null ? s_Instance : s_Instance = Resources.Load<ShowcaseSkin>("ShowcaseSkin");
    }
}
