// 클라이언트가 로그인 전에 공지 · 점검 상태를 읽는다 — 인증 없는 공개 API
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.BossRoom.Showcase
{
    public static class ServerStatusClient
    {
        public static ServerStatus Last;

        public static async Task<ServerStatus> Fetch()
        {
            using var req = UnityWebRequest.Get(ShowcaseLogin.ApiBase + "/api/serverstatus");
            req.timeout = 10;
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success) return Last;
            Last = JsonUtility.FromJson<ServerStatus>(req.downloadHandler.text);
            return Last;
        }
    }
}
