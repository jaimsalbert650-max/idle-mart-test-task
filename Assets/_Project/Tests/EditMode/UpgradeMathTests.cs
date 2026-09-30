using IdleMart.Economy;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class UpgradeMathTests
    {
        [TestCase(100, 1.5f, 0, 100)]
        [TestCase(100, 1.5f, 1, 150)]
        [TestCase(100, 1.5f, 2, 225)]
        [TestCase(40, 1.15f, 3, 61)]
        public void Cost_GrowsExponentially(long baseCost, float growth, int level, long expected)
        {
            Assert.AreEqual(expected, UpgradeMath.Cost(baseCost, growth, level));
        }

        [Test]
        public void Value_AddsLinearStepPerLevel()
        {
            Assert.AreEqual(10f, UpgradeMath.Value(10f, 2f, 0), 1e-5);
            Assert.AreEqual(16f, UpgradeMath.Value(10f, 2f, 3), 1e-5);
        }
    }
}
