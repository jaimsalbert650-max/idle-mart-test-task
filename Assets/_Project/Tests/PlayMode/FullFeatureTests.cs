using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleMart.AI;
using IdleMart.Core;
using IdleMart.Save;
using IdleMart.Settings;
using IdleMart.UI;
using IdleMart.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdleMart.Tests
{
    /// <summary>
    /// QA pass over every player-facing feature, driven through the real UI wherever possible:
    /// buttons are pressed via their onClick, settings controls are changed like a player would,
    /// panels are read back from the scene. The player's save and settings are backed up and restored.
    /// </summary>
    public class FullFeatureTests
    {
        private const string BootScene = "Boot";
        private static readonly string[] SettingsKeys = { "settings.music", "settings.sfx", "settings.quality", "settings.fullscreen" };

        private string _saveBackup;
        private readonly Dictionary<string, object> _prefsBackup = new Dictionary<string, object>();
        private int _qualityBackup;
        private readonly List<InputDevice> _devices = new List<InputDevice>();
        private InputSettings.EditorInputBehaviorInPlayMode _inputBehaviorBackup;
        private InputSettings.BackgroundBehavior _backgroundBackup;

        // ---------- Setup ----------

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveBackup = GameBootstrap.SavePath + ".fulltestbackup";
            if (File.Exists(GameBootstrap.SavePath)) File.Copy(GameBootstrap.SavePath, _saveBackup, true);
            else if (File.Exists(_saveBackup)) File.Delete(_saveBackup);

            _prefsBackup.Clear();
            foreach (var key in SettingsKeys)
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                _prefsBackup[key] = key == "settings.music" || key == "settings.sfx" ? PlayerPrefs.GetFloat(key) : (object)PlayerPrefs.GetInt(key);
            }

            _qualityBackup = QualitySettings.GetQualityLevel();
            _inputBehaviorBackup = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBackup = InputSystem.settings.backgroundBehavior;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            foreach (var device in _devices) if (device.added) InputSystem.RemoveDevice(device);
            _devices.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = _inputBehaviorBackup;
            InputSystem.settings.backgroundBehavior = _backgroundBackup;

            // Boot-scene singletons survive scene loads; remove them so every test starts the same way.
            if (SceneLoader.Instance != null) Object.Destroy(SceneLoader.Instance.gameObject);
            if (AudioService.Instance != null) Object.Destroy(AudioService.Instance.gameObject);

            // Unload test objects. The menu has no GameBootstrap, so nothing saves over the restored file later.
            GameBootstrap.StartNewGame = false;
            yield return SceneManager.LoadSceneAsync(SceneLoader.MainMenuScene);

            if (File.Exists(_saveBackup))
            {
                File.Copy(_saveBackup, GameBootstrap.SavePath, true);
                File.Delete(_saveBackup);
            }
            else if (File.Exists(GameBootstrap.SavePath)) File.Delete(GameBootstrap.SavePath);

            foreach (var key in SettingsKeys) PlayerPrefs.DeleteKey(key);
            foreach (var pair in _prefsBackup)
            {
                if (pair.Value is float f) PlayerPrefs.SetFloat(pair.Key, f);
                else PlayerPrefs.SetInt(pair.Key, (int)pair.Value);
            }

            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(_qualityBackup, true);
        }

        // ---------- Helpers ----------

        private static GameBootstrap Game => Object.FindFirstObjectByType<GameBootstrap>();
        private static GameUI Ui => Object.FindFirstObjectByType<GameUI>();

        private static T Field<T>(object target, string name)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null) return (T)field.GetValue(target);
                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static IEnumerator NewGame()
        {
            GameBootstrap.StartNewGame = true;
            yield return SceneManager.LoadSceneAsync(SceneLoader.GameScene);
            yield return null; // Let Start() of the UI run.
        }

        private static IEnumerator LoadGameFromSave()
        {
            GameBootstrap.StartNewGame = false;
            yield return SceneManager.LoadSceneAsync(SceneLoader.GameScene);
            yield return null;
        }

        /// <summary>Waits (in real time) until <paramref name="condition"/> is true or fails after <paramref name="seconds"/>.</summary>
        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            var start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > seconds) Assert.Fail($"Timed out waiting for: {what}");
                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        private static void Press(Button button)
        {
            Assert.IsNotNull(button, "Button missing");
            Assert.IsTrue(button.gameObject.activeInHierarchy, $"Button '{button.name}' is not visible");
            Assert.IsTrue(button.interactable, $"Button '{button.name}' is not interactable");
            button.onClick.Invoke();
        }

        private static BuildSlot Slot(string id) => Game.Store.Slots.First(s => s.SlotId == id);
        private static ExpansionZone Zone(string id) => Game.Store.Zones.First(z => z.Config.id == id);

        private static ContextPanel Context => Field<ContextPanel>(Ui, "contextPanel");
        private static GameObject ContextRoot => Field<GameObject>(Context, "panel");
        private static string ContextTitle => Field<TMP_Text>(Context, "title").text;

        private static List<OptionButton> VisibleOptions(ContextPanel panel) =>
            Field<List<OptionButton>>(panel, "_options").Where(o => o.gameObject.activeSelf).ToList();

        private static string Title(OptionButton option) => Field<TMP_Text>(option, "title").text;
        private static string Subtitle(OptionButton option) => Field<TMP_Text>(option, "subtitle").text;

        private static OptionButton Option(string titleStart)
        {
            var option = VisibleOptions(Context).FirstOrDefault(o => Title(o).StartsWith(titleStart));
            Assert.IsNotNull(option, $"Option '{titleStart}' not shown. Visible: {string.Join(" | ", VisibleOptions(Context).Select(Title))}");
            return option;
        }

        private static void ClickOption(OptionButton option) => Press(Field<Button>(option, "button"));

        private static void ClickOption(string titleStart) => ClickOption(Option(titleStart));

        private static string LastToast()
        {
            var toast = Field<ToastView>(Ui, "toast");
            var queue = Field<Queue<string>>(toast, "_queue");
            return string.Join(" | ", new[] { Field<TMP_Text>(toast, "label").text }.Concat(queue));
        }

        private static void AssertToast(string contains) =>
            StringAssert.Contains(contains, LastToast(), "Expected toast message");

        /// <summary>Gives XP until the store reaches <paramref name="level"/>.</summary>
        private static void ReachLevel(int level)
        {
            var progress = Game.Services.Progress;
            while (progress.Level < level) progress.AddXp(5);
        }

        private Keyboard AddKeyboard()
        {
            // Batch mode has no focused Game view; let device input reach the game anyway.
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            _devices.Add(keyboard);
            keyboard.MakeCurrent();
            return keyboard;
        }

        private Mouse AddMouse()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            _devices.Add(mouse);
            mouse.MakeCurrent();
            return mouse;
        }

        private static IEnumerator TapKey(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        // ---------- Boot, main menu, scene flow ----------

        [UnityTest]
        public IEnumerator Boot_MainMenu_NewGame_Continue_Confirm_Settings()
        {
            if (File.Exists(GameBootstrap.SavePath)) File.Delete(GameBootstrap.SavePath);

            // Boot → loading screen → main menu.
            yield return SceneManager.LoadSceneAsync(BootScene);
            Assert.IsNotNull(SceneLoader.Instance, "SceneLoader missing in Boot");
            Assert.IsNotNull(AudioService.Instance, "AudioService missing in Boot");
            yield return WaitForScene(SceneLoader.MainMenuScene);

            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu);
            Assert.IsFalse(Field<Button>(menu, "continueButton").interactable, "Continue must be disabled without a save");

            // New game without a save goes straight to the game.
            Press(Field<Button>(menu, "newGameButton"));
            yield return WaitForScene(SceneLoader.GameScene);
            Assert.AreEqual(Game.Services.Config.startMoney, Game.Services.Wallet.Money);
            Assert.AreEqual(1f, Time.timeScale);

            // Build something, then pause → "Save & Main Menu".
            Game.Services.Wallet.Add(500);
            Assert.AreEqual(PurchaseResult.Ok, Game.Store.TryBuild(Slot("h1_shelf_01"), Game.Services.Config.FindBuildable("shelf_fruits")));
            var moneyAtSave = Game.Services.Wallet.Money;
            Press(Field<Button>(Ui, "pauseButton"));
            Assert.AreEqual(0f, Time.timeScale, "Pause must stop time");
            Press(Field<Button>(Ui, "menuButton"));
            yield return WaitForScene(SceneLoader.MainMenuScene);
            Assert.AreEqual(1f, Time.timeScale, "Time must run again in the menu");
            Assert.IsTrue(File.Exists(GameBootstrap.SavePath), "Leaving to the menu must save");

            // Continue is available now; New Game asks for confirmation; "Cancel" closes it.
            menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsTrue(Field<Button>(menu, "continueButton").interactable, "Continue must be enabled with a save");
            var confirm = Field<GameObject>(menu, "confirmPanel");
            Press(Field<Button>(menu, "newGameButton"));
            Assert.IsTrue(confirm.activeSelf, "New Game with a save must ask for confirmation");
            Press(Field<Button>(menu, "confirmNo"));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(confirm.activeSelf, "Cancel must close the confirmation");

            // Continue restores the saved store.
            Press(Field<Button>(menu, "continueButton"));
            yield return WaitForScene(SceneLoader.GameScene);
            Assert.IsFalse(Slot("h1_shelf_01").IsEmpty, "Continue must restore the built shelf");
            Assert.AreEqual(moneyAtSave, Game.Services.Wallet.Money);

            // Back to the menu, then confirm a new game: the store starts from scratch.
            Press(Field<Button>(Ui, "pauseButton"));
            Press(Field<Button>(Ui, "menuButton"));
            yield return WaitForScene(SceneLoader.MainMenuScene);
            menu = Object.FindFirstObjectByType<MainMenuController>();
            Press(Field<Button>(menu, "newGameButton"));
            Press(Field<Button>(menu, "confirmYes"));
            yield return WaitForScene(SceneLoader.GameScene);
            Assert.IsTrue(Game.Store.Slots.All(s => s.IsEmpty), "New game must start with an empty store");
            Assert.AreEqual(Game.Services.Config.startMoney, Game.Services.Wallet.Money);

            // Main-menu settings: every control writes through to SettingsService and the audio.
            Press(Field<Button>(Ui, "pauseButton"));
            Press(Field<Button>(Ui, "menuButton"));
            yield return WaitForScene(SceneLoader.MainMenuScene);
            menu = Object.FindFirstObjectByType<MainMenuController>();
            Press(Field<Button>(menu, "settingsButton"));
            var settings = Field<SettingsPanel>(menu, "settingsPanel");
            yield return CheckSettingsPanel(settings);
        }

        private static IEnumerator WaitForScene(string scene)
        {
            yield return WaitFor(() => SceneManager.GetActiveScene().name == scene, 20f, $"scene {scene}");
            // The loader ignores new requests until its fade-out finished.
            if (SceneLoader.Instance != null)
                yield return WaitFor(() => !Field<bool>(SceneLoader.Instance, "_loading"), 10f, "loading screen to close");
            yield return null;
        }

        private static IEnumerator CheckSettingsPanel(SettingsPanel settings)
        {
            var root = Field<GameObject>(settings, "panel");
            Assert.IsTrue(root.activeSelf, "Settings panel did not open");

            var music = Field<Slider>(settings, "musicSlider");
            var sfx = Field<Slider>(settings, "sfxSlider");
            var quality = Field<TMP_Dropdown>(settings, "qualityDropdown");
            var fullscreen = Field<Toggle>(settings, "fullscreenToggle");

            Assert.AreEqual(SettingsService.MusicVolume, music.value, 0.001f, "Music slider must show the stored value");
            Assert.AreEqual(QualitySettings.names.Length, quality.options.Count, "Quality dropdown must list every quality level");

            music.value = 0.3f;
            sfx.value = 0.25f;
            Assert.AreEqual(0.3f, SettingsService.MusicVolume, 0.001f);
            Assert.AreEqual(0.25f, SettingsService.SfxVolume, 0.001f);
            if (AudioService.Instance != null)
            {
                Assert.AreEqual(0.3f * 0.6f, Field<AudioSource>(AudioService.Instance, "musicSource").volume, 0.001f, "Music volume not applied");
                Assert.AreEqual(0.25f, Field<AudioSource>(AudioService.Instance, "sfxSource").volume, 0.001f, "SFX volume not applied");
            }

            var targetQuality = quality.value == 0 ? QualitySettings.names.Length - 1 : 0;
            quality.value = targetQuality;
            Assert.AreEqual(targetQuality, SettingsService.QualityLevel);
            Assert.AreEqual(targetQuality, QualitySettings.GetQualityLevel(), "Quality level not applied");

            var wasFullscreen = SettingsService.Fullscreen;
            fullscreen.isOn = !wasFullscreen;
            Assert.AreEqual(!wasFullscreen, SettingsService.Fullscreen);

            Press(Field<Button>(settings, "closeButton"));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(root.activeSelf, "Done must close the settings");

            // Reopening shows the stored values.
            settings.Show();
            Assert.AreEqual(0.3f, music.value, 0.001f);
            Assert.AreEqual(0.25f, sfx.value, 0.001f);
            Assert.AreEqual(targetQuality, quality.value);
            Assert.AreEqual(!wasFullscreen, fullscreen.isOn);
            settings.Hide();
        }

        [UnityTest]
        public IEnumerator MainMenu_CorruptSave_HidesContinue_AndGameStartsClean()
        {
            File.WriteAllText(GameBootstrap.SavePath, "{ this is not json");
            yield return SceneManager.LoadSceneAsync(SceneLoader.MainMenuScene);
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsFalse(Field<Button>(menu, "continueButton").interactable, "Corrupt save must not offer Continue");

            LogAssert.ignoreFailingMessages = true; // The corrupt save logs a warning by design.
            yield return LoadGameFromSave();
            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(Game.Store.Slots.All(s => s.IsEmpty));
            Assert.AreEqual(Game.Services.Config.startMoney, Game.Services.Wallet.Money);
        }

        // ---------- HUD ----------

        [UnityTest]
        public IEnumerator Hud_ShowsMoneyLevelXpAndRating()
        {
            yield return NewGame();
            var hud = Field<HudView>(Ui, "hud");
            var services = Game.Services;

            services.Wallet.Add(1234);
            yield return WaitFor(() => Field<TMP_Text>(hud, "money").text == NumberFormat.Money(services.Wallet.Money), 5f, "money counter to catch up");
            Assert.AreEqual("Lv 1", Field<TMP_Text>(hud, "level").text);
            Assert.AreEqual("3.5", Field<TMP_Text>(hud, "rating").text);

            services.Progress.AddXp(10);
            Assert.AreEqual(services.Progress.Progress01, Field<Image>(hud, "xpFill").fillAmount, 0.001f);

            ReachLevel(services.Progress.Table.MaxLevel);
            StringAssert.Contains("(max)", Field<TMP_Text>(hud, "level").text);

            for (var i = 0; i < 15; i++) Game.Store.ReportComplaint();
            Assert.AreEqual(services.Rating.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), Field<TMP_Text>(hud, "rating").text);
            Assert.Less(Field<TMP_Text>(hud, "rating").color.g, 0.9f, "Low rating should be shown in red");

            yield return WaitSeconds(0.6f);
            StringAssert.EndsWith("/min", Field<TMP_Text>(hud, "income").text);
        }

        // ---------- Building through the context panel ----------

        [UnityTest]
        public IEnumerator BuildSlot_Panel_LockedPoorAndSuccessfulBuild()
        {
            yield return NewGame();
            var services = Game.Services;
            var slot = Slot("h1_shelf_01");

            Context.Select(slot);
            Assert.IsTrue(ContextRoot.activeSelf, "Panel must open for an empty slot");
            Assert.AreEqual("Build a shelf", ContextTitle);
            var shelfTypes = services.Config.buildables.Count(b => b.kind == Configs.BuildableKind.Shelf);
            Assert.AreEqual(shelfTypes, VisibleOptions(Context).Count, "Every shelf type must be listed");
            Assert.IsFalse(VisibleOptions(Context).Any(o => Title(o) == "Checkout"), "Checkouts must not be offered on shelf slots");

            // Locked by level.
            StringAssert.StartsWith("Unlocks at level 2", Subtitle(Option("Bread stand")));
            ClickOption("Bread stand");
            Assert.IsTrue(slot.IsEmpty);
            AssertToast("Requires store level 2");

            // Not enough money.
            services.Wallet.Set(10);
            ClickOption("Fruit stand");
            Assert.IsTrue(slot.IsEmpty);
            yield return WaitSeconds(2.5f); // Let the previous toast go.
            AssertToast("Not enough money");

            // Success: money spent, panel switches to the new shelf.
            services.Wallet.Set(1000);
            ClickOption("Fruit stand");
            Assert.IsFalse(slot.IsEmpty);
            Assert.IsInstanceOf<Shelf>(slot.Built);
            Assert.AreEqual(1000 - services.Config.FindBuildable("shelf_fruits").buildCost, services.Wallet.Money);
            StringAssert.StartsWith("Fruit stand", ContextTitle);

            // Checkout slot shows only checkouts; close button hides the panel.
            Context.Select(Slot("h1_checkout_01"));
            Assert.AreEqual("Build a checkout", ContextTitle);
            Assert.AreEqual(1, VisibleOptions(Context).Count);
            ClickOption("Checkout");
            Assert.IsInstanceOf<Checkout>(Slot("h1_checkout_01").Built);
            Press(Field<Button>(Context, "closeButton"));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(ContextRoot.activeSelf, "Close must hide the panel");

            // Clicking empty space (null) closes it too.
            Context.Select(slot);
            Context.Select(null);
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(ContextRoot.activeSelf);
        }

        [UnityTest]
        public IEnumerator Shelf_UpgradeToMax_RestockAndSell()
        {
            yield return NewGame();
            var services = Game.Services;
            services.Wallet.Set(1_000_000);
            var slot = Slot("h1_shelf_01");
            Context.Select(slot);
            ClickOption("Fruit stand");
            var shelf = (Shelf)slot.Built;

            // Upgrade through the panel up to the maximum level.
            while (!shelf.IsMaxLevel)
            {
                var level = shelf.Level;
                var cost = shelf.NextUpgradeCost;
                var money = services.Wallet.Money;
                ClickOption($"Upgrade to Lv {level + 1}");
                Assert.AreEqual(level + 1, shelf.Level);
                Assert.AreEqual(money - cost, services.Wallet.Money);
                Assert.AreEqual(shelf.Capacity, shelf.Stock, "Upgrade must refill to the new capacity");
            }

            Assert.AreEqual(shelf.Config.maxLevel, shelf.Level);
            Assert.AreEqual("Max level", Title(VisibleOptions(Context)[0]));
            var before = services.Wallet.Money;
            ClickOption("Max level");
            Assert.AreEqual(before, services.Wallet.Money, "Max level option must not charge");
            Assert.AreEqual("MAX", Field<TMP_Text>(shelf, "levelBadge").text);
            Assert.IsTrue(Field<TMP_Text>(shelf, "levelBadge").gameObject.activeSelf);

            // Restock by hand.
            for (var i = 0; i < 5; i++) shelf.TryTakeItem();
            StringAssert.Contains("5 items missing", Subtitle(Option("Restock by hand")));
            ClickOption("Restock by hand");
            Assert.AreEqual(shelf.Capacity, shelf.Stock);
            Assert.AreEqual("Shelf is full", Subtitle(Option("Restock by hand")));

            // Sell returns half the investment and frees the slot.
            var sellValue = shelf.SellValue;
            var invested = shelf.Config.buildCost;
            for (var l = 1; l < shelf.Config.maxLevel; l++) invested += shelf.Config.UpgradeCost(l);
            Assert.AreEqual(invested / 2, sellValue);
            before = services.Wallet.Money;
            ClickOption("Sell shelf");
            Assert.IsTrue(slot.IsEmpty);
            Assert.AreEqual(before + sellValue, services.Wallet.Money);
            Assert.AreEqual("Build a shelf", ContextTitle, "Panel must offer building again after selling");
            yield return null;
            Assert.IsTrue(shelf == null, "Sold shelf must be destroyed");
        }

        [UnityTest]
        public IEnumerator Upgrade_WithoutMoney_ShowsToast()
        {
            yield return NewGame();
            var services = Game.Services;
            Context.Select(Slot("h1_shelf_01"));
            ClickOption("Fruit stand");
            services.Wallet.Set(0);
            ClickOption("Upgrade to Lv 2");
            Assert.AreEqual(1, Slot("h1_shelf_01").Built.Level);
            AssertToast("Not enough money");
        }

        // ---------- Checkout ----------

        [UnityTest]
        public IEnumerator Checkout_HireCashier_Upgrade_AndManualServe()
        {
            yield return NewGame();
            var services = Game.Services;
            services.Wallet.Set(100_000);
            Context.Select(Slot("h1_shelf_01"));
            ClickOption("Fruit stand");
            Context.Select(Slot("h1_checkout_01"));
            ClickOption("Checkout");
            var checkout = (Checkout)Slot("h1_checkout_01").Built;
            StringAssert.StartsWith("Checkout", ContextTitle);

            // Manual serving: wait for a customer at the counter, then click the checkout.
            Time.timeScale = 4f;
            var front = typeof(Checkout).GetMethod("FrontIsReady", BindingFlags.Instance | BindingFlags.NonPublic);
            yield return WaitFor(() => (bool)front.Invoke(checkout, null), 60f, "a customer waiting at the checkout");
            Assert.IsTrue(Field<GameObject>(checkout, "needsServiceMarker").activeSelf, "Waiting customer must show the service marker");
            Time.timeScale = 1f;
            var xp = services.Progress.Xp;
            var queue = checkout.QueueLength;
            var money = services.Wallet.Money;
            checkout.OnClicked(); // What ClickInput does for a click on the checkout.
            Assert.AreEqual(queue - 1, checkout.QueueLength, "Click must serve the front customer");
            Assert.Greater(services.Progress.Xp, xp);
            Assert.Greater(services.Wallet.Money, money);

            // Hire a cashier from the panel.
            Context.Select(checkout);
            money = services.Wallet.Money;
            ClickOption("Hire cashier");
            Assert.IsTrue(checkout.HasCashier);
            Assert.AreEqual(money - checkout.CashierHireCost, services.Wallet.Money);
            Assert.IsFalse(VisibleOptions(Context).Any(o => Title(o) == "Hire cashier"), "Hire option must disappear");
            Assert.IsFalse(Field<GameObject>(checkout, "needsServiceMarker").activeSelf);

            // Clicking a staffed checkout does not serve by hand.
            queue = checkout.QueueLength;
            checkout.OnClicked();
            Assert.AreEqual(queue, checkout.QueueLength);

            // Upgrades make service faster, up to the max level.
            var time = checkout.ServiceTime;
            while (!checkout.IsMaxLevel) ClickOption($"Upgrade to Lv {checkout.Level + 1}");
            Assert.Less(checkout.ServiceTime, time);
            Assert.AreEqual("Max level", Title(VisibleOptions(Context)[0]));

            // Second checkout slot works independently (one cashier per checkout).
            Context.Select(Slot("h1_checkout_02"));
            ClickOption("Checkout");
            var second = (Checkout)Slot("h1_checkout_02").Built;
            Assert.IsFalse(second.HasCashier);
            services.Wallet.Set(0);
            ClickOption("Hire cashier");
            Assert.IsFalse(second.HasCashier);
            AssertToast("Not enough money");
        }

        // ---------- Staff panel ----------

        [UnityTest]
        public IEnumerator StaffPanel_HireStockersUpToCap_AndAdCampaign()
        {
            yield return NewGame();
            var game = Game;
            var services = game.Services;
            var staffPanel = Field<StaffPanel>(Ui, "staffPanel");
            var root = Field<GameObject>(staffPanel, "panel");

            Press(Field<Button>(Ui, "staffButton"));
            Assert.IsTrue(root.activeSelf, "Staff button must open the panel");

            var hire = Field<OptionButton>(staffPanel, "hireOption");
            var campaign = Field<OptionButton>(staffPanel, "campaignOption");

            // Not enough money.
            services.Wallet.Set(0);
            ClickOption(hire);
            Assert.AreEqual(0, game.Staff.StockerCount);
            AssertToast("Not enough money");

            // Hire up to the level cap.
            services.Wallet.Set(100_000);
            var cost = game.Staff.NextStockerCost;
            ClickOption(hire);
            Assert.AreEqual(1, game.Staff.StockerCount);
            Assert.AreEqual(100_000 - cost, services.Wallet.Money);
            Assert.AreEqual(game.Staff.MaxStockers, game.Staff.StockerCount);
            StringAssert.Contains("More stockers at level 3", Subtitle(hire));
            ClickOption(hire);
            Assert.AreEqual(1, game.Staff.StockerCount, "Cap must hold");
            yield return WaitSeconds(2.5f);
            AssertToast("Reach level 3 to hire more");

            // Leveling raises the cap; the panel follows.
            ReachLevel(3);
            Assert.AreEqual(2, game.Staff.MaxStockers);
            Assert.AreEqual("Keeps your shelves full", Subtitle(hire));
            ClickOption(hire);
            Assert.AreEqual(2, game.Staff.StockerCount);
            StringAssert.Contains("2/2", Field<TMP_Text>(staffPanel, "body").text);

            // Ad campaign: paid once, doubles the flow, cannot be stacked.
            var rate = game.Customers.CustomersPerMinute;
            var campaignCost = game.Customers.CampaignCost;
            var money = services.Wallet.Money;
            ClickOption(campaign);
            Assert.AreEqual(CustomerSpawner.CampaignDuration, game.Customers.CampaignRemaining, 0.5f);
            Assert.AreEqual(money - campaignCost, services.Wallet.Money);
            Assert.AreEqual(rate * CustomerSpawner.CampaignMultiplier, game.Customers.CustomersPerMinute, 0.01f);
            StringAssert.StartsWith("Running:", Subtitle(campaign));
            money = services.Wallet.Money;
            ClickOption(campaign);
            Assert.AreEqual(money, services.Wallet.Money, "A running campaign must not be bought again");

            // The campaign ends after its duration (fast-forwarded).
            Time.timeScale = 20f;
            yield return WaitFor(() => game.Customers.CampaignRemaining <= 0f, 15f, "campaign to end");
            Time.timeScale = 1f;
            yield return WaitSeconds(0.6f);
            StringAssert.StartsWith("x2 customers", Subtitle(campaign));

            // Staff button toggles the panel closed.
            Press(Field<Button>(Ui, "staffButton"));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(root.activeSelf);

            // Close button.
            Press(Field<Button>(Ui, "staffButton"));
            Press(Field<Button>(staffPanel, "closeButton"));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(root.activeSelf);
        }

        // ---------- Expansions and level-ups ----------

        [UnityTest]
        public IEnumerator Expansions_ParkingAndSecondHall()
        {
            yield return NewGame();
            var game = Game;
            var services = game.Services;
            var parking = Zone("parking");
            var hall2 = Zone("hall2");

            Assert.IsFalse(parking.IsUnlocked);
            Assert.IsFalse(hall2.IsUnlocked);
            Assert.IsTrue(game.Store.Slots.Where(s => s.SlotId.StartsWith("h2_")).All(s => !s.gameObject.activeSelf), "Hall 2 slots must be hidden");
            Assert.IsTrue(Field<List<GameObject>>(hall2, "blockers").All(b => b.activeSelf), "Divider must stand");

            // Level too low.
            services.Wallet.Set(100_000);
            Context.Select(parking);
            Assert.AreEqual("Parking lot", ContextTitle);
            StringAssert.StartsWith("Requires level 2", Subtitle(Option("Buy expansion")));
            ClickOption("Buy expansion");
            Assert.IsFalse(parking.IsUnlocked);
            AssertToast("Requires store level 2");

            // Level-up toast lists what got unlocked.
            yield return WaitSeconds(2.5f);
            ReachLevel(2);
            AssertToast("Level 2!");
            AssertToast("Parking lot");
            AssertToast("Bread stand");
            Assert.AreEqual("Opens immediately", Subtitle(Option("Buy expansion")), "Panel must refresh on level-up");

            // Not enough money.
            services.Wallet.Set(0);
            ClickOption("Buy expansion");
            Assert.IsFalse(parking.IsUnlocked);

            // Buy parking: more customers, sign hidden, panel closed.
            services.Wallet.Set(100_000);
            var rate = game.Customers.CustomersPerMinute;
            ClickOption("Buy expansion");
            Assert.IsTrue(parking.IsUnlocked);
            Assert.AreEqual(100_000 - parking.Config.cost, services.Wallet.Money);
            Assert.Greater(game.Customers.CustomersPerMinute, rate);
            Assert.IsFalse(Field<GameObject>(parking, "saleSign").activeSelf);
            Assert.IsTrue(Field<List<GameObject>>(parking, "revealed").All(r => r.activeSelf));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(ContextRoot.activeSelf);

            // Second hall: level 4, removes the divider, enables new slots.
            Context.Select(hall2);
            ClickOption("Buy expansion");
            Assert.IsFalse(hall2.IsUnlocked);
            ReachLevel(4);
            ClickOption("Buy expansion");
            Assert.IsTrue(hall2.IsUnlocked);
            Assert.IsTrue(Field<List<GameObject>>(hall2, "blockers").All(b => !b.activeSelf), "Divider must be removed");
            var newSlots = game.Store.Slots.Where(s => s.SlotId.StartsWith("h2_")).ToList();
            Assert.IsTrue(newSlots.All(s => s.gameObject.activeSelf && s.IsAvailable));

            Context.Select(Slot("h2_shelf_01"));
            ClickOption("Snack shelf");
            Assert.IsInstanceOf<Shelf>(Slot("h2_shelf_01").Built);
            Context.Select(Slot("h2_checkout_01"));
            ClickOption("Checkout");
            Assert.IsInstanceOf<Checkout>(Slot("h2_checkout_01").Built);

            // An unlocked zone cannot be bought again.
            Assert.AreEqual(PurchaseResult.NotAllowed, game.Store.CanUnlock(hall2));
        }

        [UnityTest]
        public IEnumerator Customers_ReachSecondHall_AfterDividerRemoved()
        {
            yield return NewGame();
            var game = Game;
            game.Services.Wallet.Set(100_000);
            ReachLevel(4);
            Assert.AreEqual(PurchaseResult.Ok, game.Store.TryUnlock(Zone("hall2")));
            // Only hall 2 sells something, so customers must walk through the removed divider.
            Assert.AreEqual(PurchaseResult.Ok, game.Store.TryBuild(Slot("h2_shelf_01"), game.Services.Config.FindBuildable("shelf_fruits")));
            Assert.AreEqual(PurchaseResult.Ok, game.Store.TryBuild(Slot("h1_checkout_01"), game.Services.Config.FindBuildable("checkout")));
            Assert.IsTrue(game.Store.Checkouts.First().TryHireCashier());
            var shelf = (Shelf)Slot("h2_shelf_01").Built;
            var stock = shelf.Stock;

            Time.timeScale = 4f;
            yield return WaitFor(() => shelf.Stock < stock, 60f, "a customer to take an item in hall 2");
            yield return WaitFor(() => game.Services.Progress.Xp > 0, 60f, "the hall 2 purchase to be paid");
        }

        // ---------- Rating and hints ----------

        [UnityTest]
        public IEnumerator Hints_GuideFirstSteps_AndWarnAboutRating()
        {
            yield return NewGame();
            var game = Game;
            var ui = Ui;
            var hintPanel = Field<GameObject>(ui, "hintPanel");
            var hintText = Field<TMP_Text>(ui, "hintText");
            game.Services.Wallet.Set(100_000);

            yield return WaitFor(() => hintPanel.activeSelf, 2f, "first hint");
            StringAssert.Contains("Fruit stand", hintText.text);

            game.Store.TryBuild(Slot("h1_shelf_01"), game.Services.Config.FindBuildable("shelf_fruits"));
            yield return WaitFor(() => hintText.text.Contains("Checkout"), 2f, "checkout hint");

            game.Store.TryBuild(Slot("h1_checkout_01"), game.Services.Config.FindBuildable("checkout"));
            yield return WaitFor(() => hintText.text.Contains("hire a cashier"), 2f, "cashier hint");

            game.Store.Checkouts.First().TryHireCashier();
            yield return WaitFor(() => hintText.text.Contains("stocker"), 2f, "stocker hint");

            game.Staff.TryHireStocker();
            yield return WaitFor(() => !hintPanel.activeSelf, 2f, "hints to disappear");

            // Rating drops with complaints and lowers the customer flow; a hint warns the player.
            var flow = game.Customers.CustomersPerMinute;
            for (var i = 0; i < 12; i++) game.Store.ReportComplaint();
            Assert.Less(game.Services.Rating.Value, 3f);
            Assert.Less(game.Customers.CustomersPerMinute, flow, "Low rating must reduce customer flow");
            yield return WaitFor(() => hintPanel.activeSelf && hintText.text.Contains("unhappy"), 2f, "unhappy hint");

            // Served customers raise it again.
            var rating = game.Services.Rating.Value;
            game.Services.Rating.Satisfied();
            Assert.Greater(game.Services.Rating.Value, rating);
        }

        // ---------- Pause menu and Esc ----------

        [UnityTest]
        public IEnumerator PauseMenu_ResumeSettingsAndEscKey()
        {
            yield return NewGame();
            var ui = Ui;
            var pausePanel = Field<GameObject>(ui, "pausePanel");
            var settings = Field<SettingsPanel>(ui, "settingsPanel");
            var settingsRoot = Field<GameObject>(settings, "panel");

            Assert.IsFalse(pausePanel.activeSelf);
            Press(Field<Button>(ui, "pauseButton"));
            Assert.IsTrue(pausePanel.activeSelf);
            Assert.AreEqual(0f, Time.timeScale);

            // Settings from the pause menu.
            Press(Field<Button>(ui, "settingsButton"));
            yield return CheckSettingsPanel(settings);
            Press(Field<Button>(ui, "settingsButton"));
            Assert.IsTrue(settingsRoot.activeSelf);

            // Resume closes pause and settings and restarts time.
            Press(Field<Button>(ui, "resumeButton"));
            Assert.AreEqual(1f, Time.timeScale);
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(pausePanel.activeSelf);
            Assert.IsFalse(settingsRoot.activeSelf);

            // Esc toggles pause.
            var keyboard = AddKeyboard();
            yield return TapKey(keyboard, Key.Escape);
            Assert.AreEqual(0f, Time.timeScale, "Esc must pause");
            Assert.IsTrue(pausePanel.activeSelf);
            yield return TapKey(keyboard, Key.Escape);
            Assert.AreEqual(1f, Time.timeScale, "Esc again must resume");

            // Esc right after resuming (while the panel is still fading out) must pause again.
            Press(Field<Button>(ui, "pauseButton"));
            Press(Field<Button>(ui, "resumeButton"));
            yield return TapKey(keyboard, Key.Escape);
            Assert.AreEqual(0f, Time.timeScale, "Esc during the resume fade-out must pause again");
            Assert.IsTrue(pausePanel.activeSelf);
            yield return TapKey(keyboard, Key.Escape);
            Assert.AreEqual(1f, Time.timeScale);

            // Gameplay time stops while paused (customers do not move, campaign does not tick).
            var game = Game;
            game.Services.Wallet.Set(100_000);
            game.Customers.TryStartCampaign();
            Press(Field<Button>(ui, "pauseButton"));
            var remaining = game.Customers.CampaignRemaining;
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(remaining, game.Customers.CampaignRemaining, "Campaign must not tick while paused");
            Press(Field<Button>(ui, "resumeButton"));
        }

        [UnityTest]
        public IEnumerator PauseMenu_MainMenuButton_WithoutLoader_SavesAndOpensMenu()
        {
            yield return NewGame();
            Game.Services.Wallet.Add(777);
            var money = Game.Services.Wallet.Money;
            Press(Field<Button>(Ui, "pauseButton"));
            Press(Field<Button>(Ui, "menuButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneLoader.MainMenuScene, 10f, "main menu");
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            var data = new SaveService(new FileSaveStorage(GameBootstrap.SavePath)).Load();
            Assert.IsNotNull(data);
            Assert.AreEqual(money, data.money);
        }

        // ---------- Save, autosave, offline income ----------

        [UnityTest]
        public IEnumerator SaveLoad_RoundTrip_RestoresEverything()
        {
            yield return NewGame();
            var game = Game;
            var services = game.Services;
            services.Wallet.Set(1_000_000);
            ReachLevel(4);
            var xp = services.Progress.Xp;

            Assert.AreEqual(PurchaseResult.Ok, game.Store.TryUnlock(Zone("parking")));
            Assert.AreEqual(PurchaseResult.Ok, game.Store.TryUnlock(Zone("hall2")));
            game.Store.TryBuild(Slot("h1_shelf_01"), services.Config.FindBuildable("shelf_fruits"));
            game.Store.TryBuild(Slot("h2_shelf_03"), services.Config.FindBuildable("shelf_snacks"));
            game.Store.TryBuild(Slot("h1_checkout_01"), services.Config.FindBuildable("checkout"));
            game.Store.TryBuild(Slot("h2_checkout_01"), services.Config.FindBuildable("checkout"));
            var fruit = (Shelf)Slot("h1_shelf_01").Built;
            fruit.TryUpgrade();
            fruit.TryUpgrade();
            fruit.TryTakeItem();
            fruit.TryTakeItem();
            var fruitStock = fruit.Stock;
            ((Checkout)Slot("h1_checkout_01").Built).TryUpgrade();
            ((Checkout)Slot("h1_checkout_01").Built).TryHireCashier();
            game.Staff.TryHireStocker();
            game.Staff.TryHireStocker();
            for (var i = 0; i < 4; i++) game.Store.ReportComplaint();
            var rating = services.Rating.Value;
            var money = services.Wallet.Money;
            game.Save();

            yield return LoadGameFromSave();
            game = Game;
            services = game.Services;
            Assert.AreEqual(money, services.Wallet.Money);
            Assert.AreEqual(xp, services.Progress.Xp);
            Assert.AreEqual(4, services.Progress.Level);
            Assert.AreEqual(rating, services.Rating.Value, 0.001f);
            Assert.IsTrue(Zone("parking").IsUnlocked);
            Assert.IsTrue(Zone("hall2").IsUnlocked);
            Assert.IsTrue(Field<List<GameObject>>(Zone("hall2"), "blockers").All(b => !b.activeSelf));
            fruit = (Shelf)Slot("h1_shelf_01").Built;
            Assert.AreEqual(3, fruit.Level);
            Assert.AreEqual(fruitStock, fruit.Stock);
            Assert.AreEqual("shelf_snacks", Slot("h2_shelf_03").Built.Config.id);
            var checkout = (Checkout)Slot("h1_checkout_01").Built;
            Assert.AreEqual(2, checkout.Level);
            Assert.IsTrue(checkout.HasCashier);
            Assert.IsFalse(((Checkout)Slot("h2_checkout_01").Built).HasCashier);
            Assert.AreEqual(2, game.Staff.StockerCount);
            Assert.AreEqual(4, game.Store.Slots.Count(s => !s.IsEmpty));
            Assert.AreEqual(0, game.OfflineEarnings, "Instant reload must not grant offline income");
            Assert.IsFalse(Field<GameObject>(Ui, "offlinePanel").activeSelf);
        }

        [UnityTest]
        public IEnumerator Autosave_WritesTheSave()
        {
            yield return NewGame();
            var game = Game;
            game.Services.Wallet.Add(4321);
            if (File.Exists(GameBootstrap.SavePath)) File.Delete(GameBootstrap.SavePath);
            SetField(game, "_autosaveTimer", 29.9f);
            yield return WaitFor(() => File.Exists(GameBootstrap.SavePath), 3f, "autosave");
            Assert.AreEqual(game.Services.Wallet.Money, new SaveService(new FileSaveStorage(GameBootstrap.SavePath)).Load().money);
        }

        [UnityTest]
        public IEnumerator OfflineIncome_PopupAndCollect()
        {
            var data = new SaveData
            {
                money = 100,
                xp = 0,
                incomePerSecond = 2.0,
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600,
                rating = 4f
            };
            data.built.Add(new BuiltObjectData { slotId = "h1_shelf_01", buildableId = "shelf_fruits", level = 1 });
            new SaveService(new FileSaveStorage(GameBootstrap.SavePath)).Save(data);

            yield return LoadGameFromSave();
            var game = Game;
            var expected = (long)Math.Floor(2.0 * 3600 * game.Services.Config.offlineEfficiency);
            Assert.AreEqual(expected, game.OfflineEarnings, 1);
            Assert.AreEqual(100 + game.OfflineEarnings, game.Services.Wallet.Money);
            Assert.AreEqual(4f, game.Services.Rating.Value, 0.001f);

            var popup = Field<GameObject>(Ui, "offlinePanel");
            Assert.IsTrue(popup.activeSelf, "Offline popup must show");
            StringAssert.Contains(NumberFormat.Money(game.OfflineEarnings), Field<TMP_Text>(Ui, "offlineText").text);
            StringAssert.Contains("1h", Field<TMP_Text>(Ui, "offlineText").text);
            Press(Field<Button>(Ui, "offlineCollectButton"));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(popup.activeSelf, "Collect must close the popup");

            // The cap limits very long absences.
            data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 30 * 24 * 3600;
            new SaveService(new FileSaveStorage(GameBootstrap.SavePath)).Save(data);
            yield return LoadGameFromSave();
            var cap = Game.Services.Config.offlineCapSeconds;
            Assert.AreEqual((long)Math.Floor(2.0 * cap * Game.Services.Config.offlineEfficiency), Game.OfflineEarnings);
        }

        [UnityTest]
        public IEnumerator Save_WithUnknownSlotsAndTooManyStockers_LoadsSafely()
        {
            var data = new SaveData { money = 50, xp = 0, savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), stockers = 9 };
            data.built.Add(new BuiltObjectData { slotId = "no_such_slot", buildableId = "shelf_fruits", level = 1 });
            data.built.Add(new BuiltObjectData { slotId = "h1_shelf_02", buildableId = "no_such_item", level = 1 });
            data.built.Add(new BuiltObjectData { slotId = "h2_shelf_01", buildableId = "shelf_fruits", level = 1 }); // Zone locked.
            data.built.Add(new BuiltObjectData { slotId = "h1_shelf_01", buildableId = "shelf_fruits", level = 99, stock = 999 });
            new SaveService(new FileSaveStorage(GameBootstrap.SavePath)).Save(data);

            yield return LoadGameFromSave();
            var game = Game;
            Assert.AreEqual(1, game.Store.Slots.Count(s => !s.IsEmpty));
            var shelf = (Shelf)Slot("h1_shelf_01").Built;
            Assert.AreEqual(shelf.Config.maxLevel, shelf.Level, "Level must be clamped");
            Assert.AreEqual(shelf.Capacity, shelf.Stock, "Stock must be clamped");
            Assert.AreEqual(game.Staff.MaxStockers, game.Staff.StockerCount, "Stockers must be clamped to the level cap");
        }

        // ---------- AI robustness ----------

        [UnityTest]
        public IEnumerator SellingShelf_WhileCustomersAndStockersUseIt_IsSafe()
        {
            yield return NewGame();
            var game = Game;
            game.Services.Wallet.Set(100_000);
            game.Store.TryBuild(Slot("h1_shelf_01"), game.Services.Config.FindBuildable("shelf_fruits"));
            game.Store.TryBuild(Slot("h1_checkout_01"), game.Services.Config.FindBuildable("checkout"));
            game.Store.Checkouts.First().TryHireCashier();
            game.Staff.TryHireStocker();
            var shelf = (Shelf)Slot("h1_shelf_01").Built;
            while (shelf.TryTakeItem()) { }

            Time.timeScale = 4f;
            yield return WaitFor(() => shelf.ReservedByStocker && game.Customers.ActiveCount > 0, 30f, "stocker and customer heading to the shelf");
            Context.Select(shelf);
            ClickOption("Sell shelf");
            Assert.IsTrue(Slot("h1_shelf_01").IsEmpty);

            // Rebuild another product in the same slot; AI must recover without errors.
            yield return WaitSeconds(1f);
            Context.Select(Slot("h1_shelf_01"));
            ClickOption("Fruit stand");
            var newShelf = (Shelf)Slot("h1_shelf_01").Built;
            while (newShelf.TryTakeItem()) { }
            yield return WaitFor(() => newShelf.Stock > 0, 40f, "stocker to serve the rebuilt shelf");
        }

        // ---------- Mouse, keyboard, camera ----------

        [UnityTest]
        public IEnumerator Camera_KeyboardPanAndZoom_StayInBounds()
        {
            yield return NewGame();
            var controller = Object.FindFirstObjectByType<CameraController>();
            var min = Field<Vector2>(controller, "boundsMin");
            var max = Field<Vector2>(controller, "boundsMax");
            var keyboard = AddKeyboard();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.W));
            yield return WaitSeconds(3f);
            var focus = Field<Vector3>(controller, "_focus");
            Assert.AreEqual(max.x, focus.x, 0.01f, "Pan right must stop at the bound");
            Assert.AreEqual(max.y, focus.z, 0.01f, "Pan up must stop at the bound");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow, Key.DownArrow));
            yield return WaitSeconds(3f);
            focus = Field<Vector3>(controller, "_focus");
            Assert.AreEqual(min.x, focus.x, 0.01f, "Pan left must stop at the bound");
            Assert.AreEqual(min.y, focus.z, 0.01f, "Pan down must stop at the bound");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            // Paused: the camera does not move.
            Press(Field<Button>(Ui, "pauseButton"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(min.x, Field<Vector3>(controller, "_focus").x, 0.01f, "Camera must not pan while paused");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Press(Field<Button>(Ui, "resumeButton"));

            // Mouse wheel zoom is clamped.
            var mouse = AddMouse();
            var minDistance = Field<float>(controller, "minDistance");
            var maxDistance = Field<float>(controller, "maxDistance");
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            for (var i = 0; i < 60; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, scroll = new Vector2(0f, 120f) });
                yield return null;
            }

            Assert.AreEqual(minDistance, Field<float>(controller, "_distance"), 0.01f, "Zoom in must stop at the minimum");
            for (var i = 0; i < 60; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, scroll = new Vector2(0f, -120f) });
                yield return null;
            }

            Assert.AreEqual(maxDistance, Field<float>(controller, "_distance"), 0.01f, "Zoom out must stop at the maximum");
        }

        [UnityTest]
        public IEnumerator MouseClick_OnSlot_OpensBuildPanel_AndOnShelf_Restocks()
        {
            yield return NewGame();
            var game = Game;
            var mouse = AddMouse();
            var camera = Camera.main;
            var slot = Slot("h1_shelf_02");

            IEnumerator ClickAt(Vector3 world)
            {
                Vector2 screen = camera.WorldToScreenPoint(world);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
            }

            yield return ClickAt(slot.transform.position + Vector3.up * 0.05f);
            Assert.IsTrue(ContextRoot.activeSelf, "Clicking an empty slot must open the build panel");
            Assert.AreEqual("Build a shelf", ContextTitle);

            game.Services.Wallet.Set(1000);
            ClickOption("Fruit stand");
            var shelf = (Shelf)slot.Built;
            for (var i = 0; i < 3; i++) shelf.TryTakeItem();
            Context.Close();
            yield return WaitSeconds(0.4f);

            var target = shelf.GetComponentsInChildren<Collider>().Select(c => c.bounds.center).DefaultIfEmpty(shelf.transform.position + Vector3.up * 0.5f).First();
            yield return ClickAt(target);
            Assert.AreEqual(shelf.Capacity, shelf.Stock, "Clicking a shelf must restock it");
            Assert.IsTrue(ContextRoot.activeSelf);
            StringAssert.StartsWith("Fruit stand", ContextTitle);
        }
    }
}
