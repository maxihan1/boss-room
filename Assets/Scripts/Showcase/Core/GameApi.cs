// 전용 서버가 게임 API(anymmo-webserver)를 부르는 통로 — 서버 토큰을 받아 두고 401 이면 한 번 다시 받는다
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.BossRoom.Showcase
{
    public struct ApiResponse
    {
        public long Code;
        public string Text;
        public bool Ok => Code >= 200 && Code < 300;
    }

    public static class GameApi
    {
        // 서버 환경 변수 — AnyMMO 게임 서버와 같은 이름
        public static string BaseUrl => Environment.GetEnvironmentVariable("ANYRPG_API_SERVER")?.TrimEnd('/');
        static string Secret => Environment.GetEnvironmentVariable("ANYRPG_API_SECRET");
        public static bool Enabled => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(Secret);

        static string s_Token;

        [Serializable] class TokenResponse { public int accountId; public string token; }
        [Serializable] class ServerLoginRequest { public string SharedSecret; }

        public static async Task<ApiResponse> Send(string method, string path, string json, bool auth = true)
        {
            if (auth && s_Token == null) await ServerLogin();
            var res = await SendOnce(method, path, json, auth);
            if (auth && res.Code == 401)
            {
                await ServerLogin();
                res = await SendOnce(method, path, json, auth);
            }
            if (!res.Ok) Debug.LogWarning($"[api] {method} {path} → {res.Code} {Trim(res.Text)}");
            return res;
        }

        public static Task<ApiResponse> Post(string path, object body, bool auth = true) =>
            Send("POST", path, body == null ? "{}" : JsonUtility.ToJson(body), auth);

        static async Task ServerLogin()
        {
            var res = await SendOnce("POST", "/api/serverlogin", JsonUtility.ToJson(new ServerLoginRequest { SharedSecret = Secret }), false);
            if (!res.Ok) throw new InvalidOperationException($"게임 API 서버 로그인 실패 {res.Code} {Trim(res.Text)}");
            s_Token = JsonUtility.FromJson<TokenResponse>(res.Text).token;
        }

        public static async Task<ApiResponse> SendOnce(string method, string path, string json, bool auth)
        {
            using var req = new UnityWebRequest(BaseUrl + path, method);
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 15;
            if (auth && s_Token != null) req.SetRequestHeader("Authorization", "Bearer " + s_Token);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return new ApiResponse { Code = req.responseCode, Text = req.downloadHandler?.text ?? req.error };
        }

        static string Trim(string s) => s == null ? "" : s.Length > 200 ? s.Substring(0, 200) : s;
    }
}
