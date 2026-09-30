using System;
using IdleMart.Core;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(9999, "9,999")]
        [TestCase(12345, "12.3K")]
        [TestCase(150000, "150K")]
        [TestCase(1250000, "1.25M")]
        [TestCase(-12345, "-12.3K")]
        public void Short_UsesSuffixes(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Short(value));
        }

        [Test]
        public void Money_AddsDollar()
        {
            Assert.AreEqual("$1,500", NumberFormat.Money(1500));
        }

        [Test]
        public void Duration_PicksLargestUnit()
        {
            Assert.AreEqual("1h 20m", NumberFormat.Duration(TimeSpan.FromMinutes(80)));
            Assert.AreEqual("5m", NumberFormat.Duration(TimeSpan.FromMinutes(5)));
            Assert.AreEqual("45s", NumberFormat.Duration(TimeSpan.FromSeconds(45)));
        }
    }
}
