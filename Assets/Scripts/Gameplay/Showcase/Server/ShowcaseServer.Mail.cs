// 서버 쪽 우편함 — S3 에서 채운다
using System.Threading.Tasks;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static partial class ShowcaseServer
    {
        static void RegisterMailHandlers() { }
        static int UnreadMail(Profile p) => 0;
    }
}
