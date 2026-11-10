// 친구 신청 · 파티 · 길드 초대를 받으면 화면 가운데 뜨는 수락 · 거절 창
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static class InviteDialog
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init() => SocialState.Invited += Show;

        static void Show(Invite invite)
        {
            if (GameHud.Instance == null) return;
            var root = UIKit.Rect(GameHud.Instance.CanvasRoot, "Invite", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIKit.Stretch(root);
            UIKit.Shade(root);
            var title = invite.Kind switch { "friend" => "친구 신청", "party" => "파티 초대", _ => "길드 초대" };
            var panel = UIKit.Panel(root, title, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 360));
            UIKit.Label(panel, invite.Text, 30, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(660, 90), TMPro.TextAlignmentOptions.Center);
            void Answer(bool yes)
            {
                ShowcaseClient.Send($"{invite.Kind}_{(yes ? "accept" : "decline")}", new Req { Name = invite.From });
                Object.Destroy(root.gameObject);
            }
            UIKit.Button(panel, "수락", new Vector2(0.5f, 0f), new Vector2(-150, 70), new Vector2(240, 80), () => Answer(true), 30);
            UIKit.Button(panel, "거절", new Vector2(0.5f, 0f), new Vector2(150, 70), new Vector2(240, 80), () => Answer(false), 30);
        }
    }
}
