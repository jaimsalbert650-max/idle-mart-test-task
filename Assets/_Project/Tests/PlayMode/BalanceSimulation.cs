using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using IdleMart.Configs;
using IdleMart.Core;
using IdleMart.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleMart.Tests
{
    /// <summary>
    /// Balancing tool, not a regular test: a bot plays like an active player (serves, restocks, buys the cheapest
    /// useful thing) for 15 game minutes at x20 speed and writes a per-minute report to Logs/balance.txt.
    /// Run it explicitly from the Test Runner.
    /// </summary>
    [Explicit("Balancing tool, ~1 minute real time")]
    public class BalanceSimulation
    {
        private const float GameMinutes = 15f;

        [UnityTest]
        public IEnumerator ActivePlayer_FifteenMinutes()
        {
            var backup = GameBootstrap.SavePath + ".simbackup";
            if (File.Exists(GameBootstrap.SavePath)) File.Copy(GameBootstrap.SavePath, backup, true);
            GameBootstrap.StartNewGame = true;
            yield return SceneManager.LoadSceneAsync(SceneLoader.GameScene);
            yield return null;

            var game = Object.FindFirstObjectByType<GameBootstrap>();
            var report = new StringBuilder("min | lvl | money | $/min | built | staff\n");
            Time.timeScale = 20f;
            var nextReport = 60f;
            var nextAction = 0f;

            while (Time.time < GameMinutes * 60f)
            {
                if (Time.time >= nextAction)
                {
                    nextAction = Time.time + 2f;
                    Act(game);
                }

                if (Time.time >= nextReport)
                {
                    var s = game.Services;
                    report.AppendLine($"{nextReport / 60f,3:0} | {s.Progress.Level,3} | {s.Wallet.Money,6} | {s.Income.PerSecond * 60,5:0} | {game.Store.Slots.Count(x => !x.IsEmpty),5} | {game.Staff.StockerCount}");
                    nextReport += 60f;
                }

                yield return null;
            }

            Time.timeScale = 1f;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/balance.txt", report.ToString());
            Debug.Log(report);

            if (File.Exists(backup)) File.Copy(backup, GameBootstrap.SavePath, true);
            if (File.Exists(backup)) File.Delete(backup);
        }

        private static void Act(GameBootstrap game)
        {
            var s = game.Services;
            var store = game.Store;

            // Manual work an active player does.
            foreach (var checkout in store.Checkouts.Where(c => !c.HasCashier)) checkout.OnClicked();
            foreach (var shelf in store.Shelves.Where(x => x.Stock == 0)) shelf.OnClicked();

            // Spend on the cheapest useful thing.
            foreach (var checkout in store.Checkouts.Where(c => !c.HasCashier)) checkout.TryHireCashier();
            if (game.Staff.CanHireMore && store.Shelves.Count() >= 2) game.Staff.TryHireStocker();
            foreach (var zone in store.Zones) store.TryUnlock(zone);

            ReplaceWeakestShelf(game);
            if (!store.Checkouts.Any()) TryBuildCheapest(game, BuildableKind.Checkout);
            TryBuildCheapest(game, BuildableKind.Shelf);
            if (store.Shelves.Count() >= 5 * store.Checkouts.Count()) TryBuildCheapest(game, BuildableKind.Checkout);

            var upgrade = store.Slots.Where(x => !x.IsEmpty && !x.Built.IsMaxLevel).Select(x => x.Built).OrderBy(b => b.NextUpgradeCost).FirstOrDefault();
            if (upgrade != null && s.Wallet.Money > upgrade.NextUpgradeCost * 2) upgrade.TryUpgrade();
        }

        /// <summary>When every shelf slot is taken, swap the cheapest product for the best unlocked one.</summary>
        private static void ReplaceWeakestShelf(GameBootstrap game)
        {
            var store = game.Store;
            if (store.Slots.Any(x => x.IsEmpty && x.IsAvailable && x.Kind == BuildableKind.Shelf)) return;

            var best = game.Services.Config.buildables
                .Where(b => b.kind == BuildableKind.Shelf && b.unlockLevel <= game.Services.Progress.Level)
                .OrderByDescending(b => b.product.basePrice).FirstOrDefault();
            var weakest = store.Shelves.OrderBy(x => x.Product.basePrice).FirstOrDefault();
            if (best == null || weakest == null || weakest.Product.basePrice >= best.product.basePrice) return;
            if (game.Services.Wallet.Money + weakest.SellValue < best.buildCost * 2) return;

            var slot = weakest.Slot;
            store.TrySell(weakest);
            store.TryBuild(slot, best);
        }

        private static void TryBuildCheapest(GameBootstrap game, BuildableKind kind)
        {
            var slot = game.Store.Slots.FirstOrDefault(x => x.IsEmpty && x.IsAvailable && x.Kind == kind);
            if (slot == null) return;

            // Prefer the newest unlocked product (higher price), fall back to anything affordable.
            var config = game.Services.Config.buildables
                .Where(b => b.kind == kind && game.Store.CanBuild(slot, b) == PurchaseResult.Ok)
                .OrderByDescending(b => b.unlockLevel)
                .FirstOrDefault();
            if (config != null) game.Store.TryBuild(slot, config);
        }
    }
}
