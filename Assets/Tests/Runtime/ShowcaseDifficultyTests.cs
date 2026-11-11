// 시연판 혼자 난이도 — 접속 인원에 따라 적이 주는 피해 배율을 확인한다
using NUnit.Framework;
using Unity.BossRoom.Gameplay.Showcase;

namespace Unity.BossRoom.Tests.Runtime
{
    public class ShowcaseDifficultyTests
    {
        [TestCase(0, 0.5f)]
        [TestCase(1, 0.5f)]
        [TestCase(2, 0.75f)]
        [TestCase(3, 1f)]
        [TestCase(4, 1f)]
        public void EnemyDamageShrinksWhenFewPlayers(int players, float expected)
        {
            Assert.AreEqual(expected, GameData.EnemyDamageScale(players));
        }
    }
}
