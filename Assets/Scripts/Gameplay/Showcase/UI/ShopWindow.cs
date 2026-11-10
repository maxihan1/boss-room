// 상점 창 — 규칙표에서 값이 붙은 아이템만 판다. 파는 것은 가방 창에서 한다
using System.Linq;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class ShopWindow : Window
    {
        RectTransform m_List;
        TMPro.TextMeshProUGUI m_Gold;

        public static void Toggle(Transform canvas) => Open<ShopWindow>(canvas, "상점", new Vector2(1100, 820));

        protected override void Render(ProfileView p)
        {
            if (m_List == null)
            {
                m_Gold = UIKit.Label(Body, "", 30, UIKit.Gold, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(900, 40), TMPro.TextAlignmentOptions.MidlineRight);
                m_List = UIKit.ScrollList(Body, new Vector2(0.5f, 1f), new Vector2(0, -420), new Vector2(980, 560));
                foreach (var def in GameData.Items.Where(i => i.Price > 0))
                {
                    var row = UIKit.Row(m_List, 96);
                    UIKit.Label(row, $"<color={GameData.GradeColor(def.Grade)}>{def.Name}</color>", 30, UIKit.Cream, new Vector2(0f, 0.5f), new Vector2(250, 16), new Vector2(460, 40));
                    UIKit.Label(row, def.Desc, 22, UIKit.Dim, new Vector2(0f, 0.5f), new Vector2(250, -20), new Vector2(460, 32));
                    UIKit.Label(row, $"{UIKit.Number(def.Price)} G", 28, UIKit.Gold, new Vector2(1f, 0.5f), new Vector2(-300, 0), new Vector2(140, 40), TMPro.TextAlignmentOptions.MidlineRight);
                    var name = def.Name;
                    UIKit.Button(row, "구매", new Vector2(1f, 0.5f), new Vector2(-110, 0), new Vector2(150, 70), () => ShowcaseClient.Send("buy", new Req { Name = name, Count = 1 }), 26);
                }
            }
            m_Gold.text = $"보유 골드 <color=#FFFFFF>{UIKit.Number(p.Gold)}</color>";
        }
    }
}
