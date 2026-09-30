using System;
using IdleMart.Economy;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class WalletTests
    {
        [Test]
        public void TrySpend_WithEnoughMoney_DeductsAndRaisesChanged()
        {
            var wallet = new Wallet(100);
            long? raised = null;
            wallet.Changed += m => raised = m;

            Assert.IsTrue(wallet.TrySpend(40));
            Assert.AreEqual(60, wallet.Money);
            Assert.AreEqual(60, raised);
        }

        [Test]
        public void TrySpend_NotEnoughMoney_ReturnsFalseAndKeepsBalance()
        {
            var wallet = new Wallet(10);
            var raised = false;
            wallet.Changed += _ => raised = true;

            Assert.IsFalse(wallet.TrySpend(11));
            Assert.AreEqual(10, wallet.Money);
            Assert.IsFalse(raised);
        }

        [Test]
        public void Add_IncreasesMoney()
        {
            var wallet = new Wallet();
            wallet.Add(25);
            Assert.AreEqual(25, wallet.Money);
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            var wallet = new Wallet(10);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(-5));
        }

        [Test]
        public void CanAfford_ChecksBalance()
        {
            var wallet = new Wallet(50);
            Assert.IsTrue(wallet.CanAfford(50));
            Assert.IsFalse(wallet.CanAfford(51));
        }
    }
}
