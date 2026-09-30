using IdleMart.Progression;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class LevelTableTests
    {
        // Reach level 2 at 10 XP, level 3 at 30 XP, level 4 at 60 XP.
        private readonly LevelTable _table = new LevelTable(new[] { 10, 30, 60 });

        [TestCase(0, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(59, 3)]
        [TestCase(60, 4)]
        [TestCase(999, 4)]
        public void LevelFor_UsesCumulativeThresholds(int xp, int level)
        {
            Assert.AreEqual(level, _table.LevelFor(xp));
        }

        [Test]
        public void MaxLevel_IsThresholdCountPlusOne()
        {
            Assert.AreEqual(4, _table.MaxLevel);
        }

        [TestCase(0, 0f)]
        [TestCase(20, 0.5f)]
        [TestCase(999, 1f)]
        public void Progress01_IsFractionOfCurrentLevel(int xp, float expected)
        {
            Assert.AreEqual(expected, _table.Progress01(xp), 1e-5);
        }
    }
}
