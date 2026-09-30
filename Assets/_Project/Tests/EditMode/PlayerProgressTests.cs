using System.Collections.Generic;
using IdleMart.Progression;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class PlayerProgressTests
    {
        [Test]
        public void AddXp_CrossingThresholds_RaisesLevelUpForEachLevel()
        {
            var progress = new PlayerProgress(new LevelTable(new[] { 10, 30 }));
            var levels = new List<int>();
            progress.LevelUp += levels.Add;

            progress.AddXp(35);

            Assert.AreEqual(3, progress.Level);
            CollectionAssert.AreEqual(new[] { 2, 3 }, levels);
        }

        [Test]
        public void SetXp_DoesNotRaiseLevelUp()
        {
            var progress = new PlayerProgress(new LevelTable(new[] { 10 }));
            var raised = false;
            progress.LevelUp += _ => raised = true;

            progress.SetXp(50);

            Assert.AreEqual(2, progress.Level);
            Assert.IsFalse(raised);
        }
    }
}
