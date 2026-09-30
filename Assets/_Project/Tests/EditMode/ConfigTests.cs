using IdleMart.Configs;
using NUnit.Framework;
using UnityEngine;

namespace IdleMart.Tests
{
    public class ConfigTests
    {
        private BuildableConfig _shelf;
        private GameConfig _game;

        [SetUp]
        public void SetUp()
        {
            var product = ScriptableObject.CreateInstance<ProductConfig>();
            product.basePrice = 10;

            _shelf = ScriptableObject.CreateInstance<BuildableConfig>();
            _shelf.product = product;
            _shelf.buildCost = 100;
            _shelf.upgradeCostGrowth = 2f;
            _shelf.baseCapacity = 6;
            _shelf.capacityPerLevel = 2;
            _shelf.pricePerLevel = 0.5f;
            _shelf.baseServiceTime = 2f;
            _shelf.serviceTimePerLevel = 0.5f;

            _game = ScriptableObject.CreateInstance<GameConfig>();
            _game.baseStockers = 1;
            _game.stockersPerLevel = 0.5f;
            _game.stockerBaseHireCost = 100;
            _game.stockerHireCostGrowth = 2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_shelf.product);
            Object.DestroyImmediate(_shelf);
            Object.DestroyImmediate(_game);
        }

        [Test]
        public void Shelf_StatsGrowWithLevel()
        {
            Assert.AreEqual(6, _shelf.CapacityAt(1));
            Assert.AreEqual(10, _shelf.CapacityAt(3));
            Assert.AreEqual(10, _shelf.SalePriceAt(1));
            Assert.AreEqual(20, _shelf.SalePriceAt(3));
        }

        [Test]
        public void UpgradeCost_IsExponential()
        {
            Assert.AreEqual(200, _shelf.UpgradeCost(1));
            Assert.AreEqual(400, _shelf.UpgradeCost(2));
        }

        [Test]
        public void ServiceTime_NeverDropsBelowMinimum()
        {
            Assert.AreEqual(2f, _shelf.ServiceTimeAt(1), 1e-5);
            Assert.AreEqual(1.5f, _shelf.ServiceTimeAt(2), 1e-5);
            Assert.AreEqual(0.4f, _shelf.ServiceTimeAt(20), 1e-5);
        }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(5, 3)]
        public void MaxStockers_StartsAtOneAndGrows(int level, int expected)
        {
            Assert.AreEqual(expected, _game.MaxStockers(level));
        }

        [Test]
        public void StockerHireCost_GrowsPerHire()
        {
            Assert.AreEqual(100, _game.StockerHireCost(0));
            Assert.AreEqual(400, _game.StockerHireCost(2));
        }
    }
}
