using IdleMart.Economy;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class OfflineIncomeTests
    {
        [Test]
        public void Earnings_AreRateTimesElapsedTimesEfficiency()
        {
            Assert.AreEqual(300, OfflineIncome.Calculate(perSecond: 2, elapsedSeconds: 300, capSeconds: 7200, efficiency: 0.5f));
        }

        [Test]
        public void Elapsed_IsCapped()
        {
            Assert.AreEqual(7200, OfflineIncome.Calculate(1, 100_000, 7200, 1f));
        }

        [Test]
        public void NegativeElapsed_GivesZero()
        {
            Assert.AreEqual(0, OfflineIncome.Calculate(5, -60, 7200, 1f));
        }

        [Test]
        public void ShortAbsence_BelowMinimum_GivesZero()
        {
            Assert.AreEqual(0, OfflineIncome.Calculate(5, OfflineIncome.MinSeconds - 1, 7200, 1f));
        }
    }
}
