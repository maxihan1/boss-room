// 서버 쪽 채팅 · 친구 · 파티 · 길드 — S4 에서 채운다
using System.Threading.Tasks;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static void RegisterSocialHandlers() { }
        static Task AfterLoad(Profile p) => Task.CompletedTask;
        static Task OnLeft(Profile p) => Task.CompletedTask;
        static string GuildName(Profile p) => "";
        static float PartyBonus(Profile p) => 1f;
    }
}
