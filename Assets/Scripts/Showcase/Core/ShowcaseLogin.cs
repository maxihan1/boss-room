// 클라이언트가 로그인 창에서 받은 아이디 · 비밀번호와 서버가 돌려준 거절 설명을 장면 사이에 들고 다닌다
namespace Unity.BossRoom.Showcase
{
    public static class ShowcaseLogin
    {
        public static string UserName = "";
        public static string Password = "";
        // 정지 사유 · 점검 문구처럼 거절 번호만으로는 모자란 설명
        public static string LastDetail = "";
        // 접속할 게임 서버 — 시연 서버는 443 wss
        public static string ServerHost = "anyrpg.maxihan.com";
        public static int ServerPort = 443;
        // 로그인 전 공지 · 점검을 읽는 공개 주소(인증 없음)
        public static string ApiBase = "https://anyrpg-api.maxihan.com";
    }
}
