using System.Collections;
using System.IO;
using System.Linq;
using IdleMart.Core;
using IdleMart.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleMart.Tests
{
    /// <summary>
    /// End-to-end checks on the real Game scene: building, customers buying and paying,
    /// stockers restocking, and save/load. The player's own save is backed up and restored.
    /// </summary>
    public class GameLoopTests
    {
        private string _backup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _backup = GameBootstrap.SavePath + ".testbackup";
            if (File.Exists(GameBootstrap.SavePath)) File.Copy(GameBootstrap.SavePath, _backup, true);

            GameBootstrap.StartNewGame = true;
            yield return SceneManager.LoadSceneAsync(SceneLoader.GameScene);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneLoader.GameScene); // Unload game objects cleanly.
            if (File.Exists(_backup)) File.Copy(_backup, GameBootstrap.SavePath, true);
            else if (File.Exists(GameBootstrap.SavePath)) File.Delete(GameBootstrap.SavePath);
            if (File.Exists(_backup)) File.Delete(_backup);
        }

        private static GameBootstrap Game => Object.FindFirstObjectByType<GameBootstrap>();

        private static void Build(string slotId, string buildableId)
        {
            var game = Game;
            var result = game.Store.TryBuild(game.Store.Slots.First(s => s.SlotId == slotId), game.Services.Config.FindBuildable(buildableId));
            Assert.AreEqual(PurchaseResult.Ok, result, $"Building {buildableId} in {slotId}");
        }

        [UnityTest]
        public IEnumerator CustomersBuyAndPay_WithCashier()
        {
            var game = Game;
            game.Services.Wallet.Add(1000);
            Build("h1_shelf_01", "shelf_fruits");
            Build("h1_checkout_01", "checkout");
            Assert.IsTrue(game.Store.Checkouts.First().TryHireCashier());
            var moneyBefore = game.Services.Wallet.Money;

            Time.timeScale = 4f;
            for (var t = 0f; t < 25f && game.Services.Progress.Xp == 0; t += Time.unscaledDeltaTime) yield return null;

            Assert.Greater(game.Services.Progress.Xp, 0, "No sale happened");
            Assert.Greater(game.Services.Wallet.Money, moneyBefore);
        }

        [UnityTest]
        public IEnumerator Stocker_RefillsEmptyShelf()
        {
            var game = Game;
            game.Services.Wallet.Add(1000);
            Build("h1_shelf_01", "shelf_fruits");
            var shelf = game.Store.Shelves.First();
            while (shelf.TryTakeItem()) { }
            Assert.IsTrue(game.Staff.TryHireStocker());

            Time.timeScale = 4f;
            for (var t = 0f; t < 25f && shelf.Stock == 0; t += Time.unscaledDeltaTime) yield return null;

            Assert.Greater(shelf.Stock, 0, "Stocker did not restock");
        }

        [UnityTest]
        public IEnumerator SaveAndReload_RestoresStore()
        {
            var game = Game;
            game.Services.Wallet.Add(500);
            Build("h1_shelf_01", "shelf_fruits");
            Build("h1_checkout_01", "checkout");
            var money = game.Services.Wallet.Money;
            game.Save();

            GameBootstrap.StartNewGame = false;
            yield return SceneManager.LoadSceneAsync(SceneLoader.GameScene);
            yield return null;

            game = Game;
            Assert.AreEqual(2, game.Store.Slots.Count(s => !s.IsEmpty));
            Assert.AreEqual(money, game.Services.Wallet.Money);
        }
    }
}
