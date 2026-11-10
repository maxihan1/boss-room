// 시험용 — 명령줄 -actions "buy:이름;equip:이름;sell:이름;claim:퀘스트id;potion" 을 캐릭터 정보를 받은 뒤 1초 간격으로 서버에 보낸다
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public class ClientScript : MonoBehaviour
    {
        string[] m_Steps;

        public static void Run(string actions)
        {
            var go = new GameObject("ShowcaseClientScript");
            DontDestroyOnLoad(go);
            go.AddComponent<ClientScript>().m_Steps = actions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        IEnumerator Start()
        {
            while (ShowcaseClient.Profile == null) yield return null;
            yield return new WaitForSeconds(1f);
            foreach (var step in m_Steps)
            {
                var parts = step.Split(new[] { ':' }, 2);
                var verb = parts[0].Trim();
                var arg = parts.Length > 1 ? parts[1].Trim() : "";
                var p = ShowcaseClient.Profile;
                long IdOf(string name) => p.Items.FirstOrDefault(i => i.Name == name)?.Id ?? 0;
                Debug.Log($"[script] {verb} {arg}");
                switch (verb)
                {
                    case "buy": ShowcaseClient.Send("buy", new Req { Name = arg, Count = 1 }); break;
                    case "equip": ShowcaseClient.Send("equip", new Req { Id = IdOf(arg) }); break;
                    case "unequip": ShowcaseClient.Send("unequip", new Req { Id = IdOf(arg) }); break;
                    case "sell": ShowcaseClient.Send("sell", new Req { Id = IdOf(arg) }); break;
                    case "claim": ShowcaseClient.Send("quest_claim", new Req { Name = arg }); break;
                    case "potion": ShowcaseClient.Send("potion"); break;
                    default: ShowcaseClient.Send(verb, new Req { Name = arg, Text = arg }); break;
                }
                yield return new WaitForSeconds(1.2f);
            }
            var last = ShowcaseClient.Profile;
            Debug.Log($"[script] 끝 — Lv.{last.Level} 골드 {last.Gold} 무기 {last.Items.FirstOrDefault(i => i.Id == last.Weapon)?.Name ?? "-"} 가방 {string.Join(",", last.Items.Select(i => i.Name))}");
        }
    }
}
