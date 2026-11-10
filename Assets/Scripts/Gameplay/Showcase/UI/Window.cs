// 가방 · 상점 · 퀘스트 같은 창의 공통 틀 — 한 번에 하나만 열리고, 뒤를 누르거나 닫기를 누르면 닫힌다. 캐릭터 정보가 바뀌면 다시 그린다
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public abstract class Window : MonoBehaviour
    {
        static Window s_Open;
        protected RectTransform Body;

        public static T Open<T>(Transform canvas, string title, Vector2 size) where T : Window
        {
            if (s_Open != null)
            {
                var same = s_Open is T;
                s_Open.Close();
                if (same) return null; // 같은 버튼을 다시 누르면 닫기
            }
            var root = UIKit.Rect(canvas, "Window " + title, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIKit.Stretch(root);
            var w = root.gameObject.AddComponent<T>();
            UIKit.Shade(root, w.Close);
            w.Body = UIKit.Panel(root, title, new Vector2(0.5f, 0.5f), new Vector2(0, -20), size);
            UIKit.Button(w.Body, "닫기", new Vector2(1f, 0f), new Vector2(-110, 56), new Vector2(170, 70), w.Close, 28);
            s_Open = w;
            ShowcaseClient.ProfileChanged += w.OnProfile;
            if (ShowcaseClient.Profile != null) w.Render(ShowcaseClient.Profile);
            return w;
        }

        void OnProfile(ProfileView p) => Render(p);

        protected abstract void Render(ProfileView p);

        public void Close()
        {
            ShowcaseClient.ProfileChanged -= OnProfile;
            if (s_Open == this) s_Open = null;
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy() => ShowcaseClient.ProfileChanged -= OnProfile;
    }
}
