using IdleMart.Economy;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class IncomeTrackerTests
    {
        [Test]
        public void PerSecond_BeforeWindowIsFull_DividesByTrackedTime()
        {
            var clock = new FakeClock();
            var tracker = new IncomeTracker(clock, windowSeconds: 10);

            tracker.Record(30);
            clock.Advance(5);
            tracker.Record(20);

            Assert.AreEqual(10.0, tracker.PerSecond, 1e-9);
        }

        [Test]
        public void PerSecond_AfterWindowIsFull_DividesByWindow()
        {
            var clock = new FakeClock();
            var tracker = new IncomeTracker(clock, windowSeconds: 10);

            clock.Advance(5);
            tracker.Record(50);
            clock.Advance(5);

            Assert.AreEqual(5.0, tracker.PerSecond, 1e-9);
        }

        [Test]
        public void OldRecords_DropOutOfWindow()
        {
            var clock = new FakeClock();
            var tracker = new IncomeTracker(clock, windowSeconds: 10);

            tracker.Record(100);
            clock.Advance(11);
            tracker.Record(10);

            Assert.AreEqual(1.0, tracker.PerSecond, 1e-9);
        }

        [Test]
        public void NoRecords_ReturnsZero()
        {
            Assert.AreEqual(0.0, new IncomeTracker(new FakeClock()).PerSecond);
        }
    }
}
