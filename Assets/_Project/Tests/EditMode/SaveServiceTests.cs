using IdleMart.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleMart.Tests
{
    public class SaveServiceTests
    {
        private InMemorySaveStorage _storage;
        private SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _service = new SaveService(_storage);
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            var data = new SaveData { money = 1234, xp = 56, savedAtUnix = 1_700_000_000, incomePerSecond = 2.5, stockers = 2 };
            data.built.Add(new BuiltObjectData { slotId = "hall1_shelf_01", buildableId = "shelf_fruits", level = 3, hasCashier = false });
            data.unlockedZones.Add("hall2");

            _service.Save(data);
            var loaded = _service.Load();

            Assert.AreEqual(1234, loaded.money);
            Assert.AreEqual(56, loaded.xp);
            Assert.AreEqual(1_700_000_000, loaded.savedAtUnix);
            Assert.AreEqual(2.5, loaded.incomePerSecond, 1e-9);
            Assert.AreEqual(2, loaded.stockers);
            Assert.AreEqual("shelf_fruits", loaded.built[0].buildableId);
            Assert.AreEqual(3, loaded.built[0].level);
            CollectionAssert.AreEqual(new[] { "hall2" }, loaded.unlockedZones);
        }

        [Test]
        public void Load_NoSave_ReturnsNull()
        {
            Assert.IsNull(_service.Load());
            Assert.IsFalse(_service.HasSave);
        }

        [Test]
        public void Load_CorruptJson_ReturnsNull()
        {
            _storage.Content = "{ not json";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save"));
            Assert.IsNull(_service.Load());
        }

        [Test]
        public void Load_NewerVersion_ReturnsNull()
        {
            _storage.Content = "{\"version\":" + (SaveData.CurrentVersion + 1) + "}";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save"));
            Assert.IsNull(_service.Load());
        }

        [Test]
        public void Load_MissingLists_AreNeverNull()
        {
            _storage.Content = "{\"version\":1,\"money\":5}";
            var loaded = _service.Load();
            Assert.IsNotNull(loaded.built);
            Assert.IsNotNull(loaded.unlockedZones);
        }
    }
}
