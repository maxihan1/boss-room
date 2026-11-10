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
                    case "wait": yield return new WaitForSeconds(float.Parse(arg)); break;
                    case "say": ShowcaseClient.Send("chat", new Req { Name = "전체", Text = arg }); break;
                    case "psay": ShowcaseClient.Send("chat", new Req { Name = "파티", Text = arg }); break;
                    case "gsay": ShowcaseClient.Send("chat", new Req { Name = "길드", Text = arg }); break;
                    default: ShowcaseClient.Send(verb, new Req { Name = arg, Text = arg }); break;
                }
                yield return new WaitForSeconds(1.2f);
            }
            yield return new WaitForSeconds(2f);
            var s = SocialState.Last;
            Debug.Log($"[script] 소셜 — 친구 {string.Join(",", s.Friends.Select(f => f.Name + (f.Online ? "(접속)" : "")))} · 파티 {string.Join(",", s.Party.Select(m => m.Name))} · 길드 {s.GuildName}({s.Guild.Count}명)");
            foreach (var line in SocialState.Chat) Debug.Log($"[chat] [{line.Channel}] {line.From}: {line.Text}");
            var last = ShowcaseClient.Profile;
            Debug.Log($"[script] 끝 — Lv.{last.Level} 골드 {last.Gold} 무기 {last.Items.FirstOrDefault(i => i.Id == last.Weapon)?.Name ?? "-"} 가방 {string.Join(",", last.Items.Select(i => i.Name))}");
        }
    }
}
