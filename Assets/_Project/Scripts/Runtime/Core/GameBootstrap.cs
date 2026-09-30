using System;
using System.IO;
using IdleMart.AI;
using IdleMart.Configs;
using IdleMart.Economy;
using IdleMart.Save;
using IdleMart.World;
using UnityEngine;

namespace IdleMart.Core
{
    /// <summary>
    /// Composition root of the game scene. Creates the services, wires scene systems together,
    /// loads the save (or starts a new game), grants offline income and autosaves.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float AutosaveInterval = 30f;

        /// <summary>Set by the main menu before loading the game scene.</summary>
        public static bool StartNewGame;

        [SerializeField] private GameConfig config;
        [SerializeField] private Store store;
        [SerializeField] private CustomerSpawner customerSpawner;
        [SerializeField] private StaffService staffService;
        [SerializeField] private ClickInput clickInput;

        private SaveService _saveService;
        private float _autosaveTimer;
        private double _loadedIncomeRate;
        private bool _quitting;

        public GameServices Services { get; private set; }
        public Store Store => store;
        public StaffService Staff => staffService;
        public CustomerSpawner Customers => customerSpawner;
        public ClickInput ClickInput => clickInput;

        /// <summary>Money earned while away; the UI shows a popup when this is above zero.</summary>
        public long OfflineEarnings { get; private set; }
        public TimeSpan OfflineDuration { get; private set; }

        public static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private void Awake()
        {
            Services = new GameServices(config, new SystemClock());
            _saveService = new SaveService(new FileSaveStorage(SavePath));

            store.Init(Services);
            customerSpawner.Init(Services, store);
            staffService.Init(Services, store);

            if (StartNewGame) _saveService.Delete();
            StartNewGame = false;

            var data = _saveService.Load();
            if (data != null) Restore(data);
        }

        private void Update()
        {
            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= AutosaveInterval) Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        // Note: no save in OnDestroy — during scene teardown the store objects may already be destroyed,
        // which would overwrite a good save with an empty store. Leaving to the menu saves explicitly.
        private void OnApplicationQuit()
        {
            Save();
            _quitting = true;
        }

        public void Save()
        {
            if (Services == null || _quitting) return;
            _autosaveTimer = 0f;

            var rate = Services.Income.PerSecond;
            var data = new SaveData
            {
                money = Services.Wallet.Money,
                xp = Services.Progress.Xp,
                savedAtUnix = Services.Clock.UtcUnixSeconds,
                // Right after loading the tracker is still empty, keep the previous rate.
                incomePerSecond = rate > 0 ? rate : _loadedIncomeRate,
                stockers = staffService.StockerCount
            };
            store.CaptureTo(data);

            try
            {
                _saveService.Save(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Saving failed: {e.Message}");
            }
        }

        private void Restore(SaveData data)
        {
            Services.Wallet.Set(data.money);
            Services.Progress.SetXp(data.xp);
            store.RestoreFrom(data);
            staffService.Restore(data.stockers);
            _loadedIncomeRate = data.incomePerSecond;

            var elapsed = Services.Clock.UtcUnixSeconds - data.savedAtUnix;
            OfflineEarnings = OfflineIncome.Calculate(data.incomePerSecond, elapsed, config.offlineCapSeconds, config.offlineEfficiency);
            OfflineDuration = TimeSpan.FromSeconds(Math.Min(Math.Max(elapsed, 0), config.offlineCapSeconds));
            if (OfflineEarnings > 0) Services.Wallet.Add(OfflineEarnings);
        }

#if UNITY_EDITOR
        public void EditorSetup(GameConfig gameConfig, Store gameStore, CustomerSpawner spawner, StaffService staff, ClickInput input)
        {
            config = gameConfig;
            store = gameStore;
            customerSpawner = spawner;
            staffService = staff;
            clickInput = input;
        }
#endif
    }
}
