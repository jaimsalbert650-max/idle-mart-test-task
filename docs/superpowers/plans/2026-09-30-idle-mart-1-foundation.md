# Idle Mart — Plan 1: Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the Unity 6000.3.17f1 URP project with dev MCP access and implement all pure game logic (wallet, income tracking, upgrade math, offline income, levels, save/load) with EditMode tests.

**Architecture:** Plain C# classes with no MonoBehaviour dependencies live in the `IdleMart.Runtime` assembly under `Assets/_Project/Scripts/Runtime`. Time is abstracted behind `IClock` and persistence behind `ISaveStorage` so everything is unit-testable. Scene/MonoBehaviour code (Plans 2–3) will consume these classes via the `GameBootstrap` composition root.

**Tech Stack:** Unity 6000.3.17f1, URP, C# 9, Unity Test Framework (NUnit), JsonUtility. Dev-only: `com.ivanmurzak.unity.mcp` (removed before delivery).

Spec: `docs/superpowers/specs/2026-09-30-idle-mart-design.md`

---

## Conventions

- Unity editor: `D:\проэкты\6000.3.17f1\Editor\Unity.exe` (referred to as `$UNITY`).
- Project: `D:\Unity Projects\test`.
- Run EditMode tests (editor for this project must be **closed**, otherwise use the MCP test-runner tool):

```powershell
& "D:\проэкты\6000.3.17f1\Editor\Unity.exe" -batchmode -projectPath "D:\Unity Projects\test" -runTests -testPlatform EditMode -testResults "D:\Unity Projects\test\Logs\editmode-results.xml" -logFile "D:\Unity Projects\test\Logs\editmode.log"
Select-Xml -Path "D:\Unity Projects\test\Logs\editmode-results.xml" -XPath "/test-run" | ForEach-Object { $_.Node | Select-Object result,total,passed,failed }
```

- Namespaces: `IdleMart.<Folder>`; XML doc comment on every public type.
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## File Structure

```
Assets/_Project/Scripts/Runtime/
  IdleMart.Runtime.asmdef
  Core/IClock.cs              — time abstraction (+ SystemClock)
  Economy/Wallet.cs           — player money, the only mutator of money
  Economy/IncomeTracker.cs    — rolling income-per-second
  Economy/UpgradeMath.cs      — exponential cost/value curves
  Economy/OfflineIncome.cs    — offline earnings formula
  Progression/LevelTable.cs   — XP thresholds → level
  Progression/PlayerProgress.cs — XP/level state + events
  Save/SaveData.cs            — serialisable DTOs
  Save/ISaveStorage.cs        — storage abstraction
  Save/FileSaveStorage.cs     — atomic file storage
  Save/SaveService.cs         — (de)serialise, validate, versioning
Assets/_Project/Tests/EditMode/
  IdleMart.Tests.EditMode.asmdef
  FakeClock.cs, InMemorySaveStorage.cs
  WalletTests.cs, IncomeTrackerTests.cs, UpgradeMathTests.cs,
  OfflineIncomeTests.cs, LevelTableTests.cs, PlayerProgressTests.cs,
  SaveServiceTests.cs
```

---

### Task 1: Create the URP project

**Files:**
- Create: `D:\Unity Projects\test\{Assets,Packages,ProjectSettings}` (generated)
- Create: `.gitignore`

- [ ] **Step 1: Find the URP blank template shipped with the editor**

```powershell
Get-ChildItem "D:\проэкты\6000.3.17f1\Editor\Data\Resources\PackageManager\ProjectTemplates" -Filter "*urp*blank*.tgz" | Select-Object -ExpandProperty FullName
```
Expected: one path like `...\com.unity.template.urp-blank-17.x.y.tgz`. If none is found, go to Step 2b.

- [ ] **Step 2a: Create the project from the template** (Unity refuses a non-empty target, so create in a temp folder and move the content in)

```powershell
$tpl = (Get-ChildItem "D:\проэкты\6000.3.17f1\Editor\Data\Resources\PackageManager\ProjectTemplates" -Filter "*urp*blank*.tgz" | Select-Object -First 1).FullName
& "D:\проэкты\6000.3.17f1\Editor\Unity.exe" -batchmode -quit -createProject "D:\Unity Projects\_idlemart_tmp" -cloneFromTemplate $tpl -logFile "D:\Unity Projects\_idlemart_create.log"
Get-ChildItem "D:\Unity Projects\_idlemart_tmp" -Force | Where-Object Name -notin "Library","Temp","Logs" | Move-Item -Destination "D:\Unity Projects\test"
Remove-Item "D:\Unity Projects\_idlemart_tmp","D:\Unity Projects\_idlemart_create.log" -Recurse -Force
```

- [ ] **Step 2b (only if no template): create empty project and add URP to `Packages/manifest.json`** — same move procedure without `-cloneFromTemplate`, then add `"com.unity.render-pipelines.universal": "17.3.0"` to dependencies; the URP asset is created in Plan 2 Task 1.

- [ ] **Step 3: Verify version**

```powershell
Get-Content "D:\Unity Projects\test\ProjectSettings\ProjectVersion.txt"
```
Expected: `m_EditorVersion: 6000.3.17f1`

- [ ] **Step 4: Add Unity `.gitignore`**

```gitignore
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
/[Rr]ecordings/
.vs/
.idea/
.vscode/
*.csproj
*.sln
*.suo
*.user
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db
*.apk
*.aab
crashlytics-build.properties
/Assets/Plugins/NuGet/
/Assets/Plugins/NuGet.meta
```
(`Assets/Plugins/NuGet` holds the dev-only MCP DLLs; it must never reach the repo.)

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "chore: create Unity 6000.3.17f1 URP project"
```

### Task 2: Add dev-only MCP and pin its port

**Files:**
- Modify: `Packages/manifest.json`
- Create: `UserSettings/AI-Game-Developer-Config.json` (git-ignored)

- [ ] **Step 1: Add the package and OpenUPM registry to `Packages/manifest.json`**

Add to `dependencies`:
```json
"com.ivanmurzak.unity.mcp": "0.90.0",
"com.ivanmurzak.unity.mcp.navigation": "1.0.17",
"com.ivanmurzak.unity.mcp.probuilder": "1.2.31",
```
Add at top level:
```json
"scopedRegistries": [
  { "name": "package.openupm.com", "url": "https://package.openupm.com", "scopes": ["com.ivanmurzak", "extensions.unity"] }
]
```

- [ ] **Step 2: Pin the MCP port to 27361 (the port in `~/.claude.json`)** — copy the config from an existing project and change only the host:

```powershell
New-Item -ItemType Directory -Force "D:\Unity Projects\test\UserSettings" | Out-Null
(Get-Content "D:\Unity Projects\Burnout-Moto\UserSettings\AI-Game-Developer-Config.json" -Raw) -replace 'http://localhost:\d+','http://localhost:27361' | Set-Content -Encoding utf8 "D:\Unity Projects\test\UserSettings\AI-Game-Developer-Config.json"
```

- [ ] **Step 3: Resolve packages** (batchmode import, then check log for errors)

```powershell
& "D:\проэкты\6000.3.17f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\Unity Projects\test" -logFile "D:\Unity Projects\test\Logs\import.log"
Select-String -Path "D:\Unity Projects\test\Logs\import.log" -Pattern "error CS|Package.*error|Exiting batchmode" | Select-Object -Last 5
```
Expected: `Exiting batchmode successfully now!`

- [ ] **Step 4: Open the editor and verify MCP** — launch the editor normally, then from Claude call any `mcp__unity__*` tool (e.g. list scenes). Expected: response, not ECONNREFUSED. (Claude Code may need `/mcp` reconnect.)

- [ ] **Step 5: Commit** (manifest only; UserSettings is ignored)

```powershell
git add Packages/manifest.json Packages/packages-lock.json; git commit -m "chore: add dev-only MCP packages"
```

### Task 3: Assemblies and test helpers

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/IdleMart.Runtime.asmdef`
- Create: `Assets/_Project/Scripts/Runtime/Core/IClock.cs`
- Create: `Assets/_Project/Tests/EditMode/IdleMart.Tests.EditMode.asmdef`
- Create: `Assets/_Project/Tests/EditMode/FakeClock.cs`

- [ ] **Step 1: Runtime asmdef**

```json
{
    "name": "IdleMart.Runtime",
    "rootNamespace": "IdleMart",
    "references": [],
    "autoReferenced": true
}
```

- [ ] **Step 2: `IClock.cs`**

```csharp
using System;

namespace IdleMart.Core
{
    /// <summary>Source of time. Abstracted so game logic can be tested with a fake clock.</summary>
    public interface IClock
    {
        /// <summary>Monotonic seconds since an arbitrary start (for durations inside a session).</summary>
        double Now { get; }

        /// <summary>Wall-clock UTC time in Unix seconds (for offline income across sessions).</summary>
        long UtcUnixSeconds { get; }
    }

    /// <summary>Real clock backed by <see cref="System.Diagnostics.Stopwatch"/> and <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public sealed class SystemClock : IClock
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

        public double Now => _stopwatch.Elapsed.TotalSeconds;
        public long UtcUnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
```

- [ ] **Step 3: Tests asmdef**

```json
{
    "name": "IdleMart.Tests.EditMode",
    "rootNamespace": "IdleMart.Tests",
    "references": ["IdleMart.Runtime", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 4: `FakeClock.cs`**

```csharp
using IdleMart.Core;

namespace IdleMart.Tests
{
    /// <summary>Manually advanced clock for deterministic tests.</summary>
    public sealed class FakeClock : IClock
    {
        public double Now { get; private set; }
        public long UtcUnixSeconds { get; set; } = 1_700_000_000;

        public void Advance(double seconds)
        {
            Now += seconds;
            UtcUnixSeconds += (long)seconds;
        }
    }
}
```

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Project; git commit -m "chore: add runtime and edit-mode test assemblies"
```

### Task 4: Wallet

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Economy/Wallet.cs`
- Test: `Assets/_Project/Tests/EditMode/WalletTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
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
```

- [ ] **Step 2: Run tests** — Expected: compile error, `Wallet` not found.

- [ ] **Step 3: Implement `Wallet.cs`**

```csharp
using System;

namespace IdleMart.Economy
{
    /// <summary>
    /// Player's money. The only place where the balance is changed,
    /// so every change goes through validation and raises <see cref="Changed"/>.
    /// </summary>
    public sealed class Wallet
    {
        /// <summary>Raised with the new balance after every change.</summary>
        public event Action<long> Changed;

        public long Money { get; private set; }

        public Wallet(long startMoney = 0)
        {
            if (startMoney < 0) throw new ArgumentOutOfRangeException(nameof(startMoney));
            Money = startMoney;
        }

        public bool CanAfford(long amount) => amount >= 0 && Money >= amount;

        /// <summary>Spends money if the balance allows it.</summary>
        /// <returns>True if the money was spent.</returns>
        public bool TrySpend(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAfford(amount)) return false;

            Money -= amount;
            Changed?.Invoke(Money);
            return true;
        }

        public void Add(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;

            Money += amount;
            Changed?.Invoke(Money);
        }

        /// <summary>Overwrites the balance (used when loading a save).</summary>
        public void Set(long money)
        {
            if (money < 0) throw new ArgumentOutOfRangeException(nameof(money));
            Money = money;
            Changed?.Invoke(Money);
        }
    }
}
```

- [ ] **Step 4: Run tests** — Expected: 5 passed.

- [ ] **Step 5: Commit** — `git add Assets/_Project; git commit -m "feat: add Wallet"`

### Task 5: UpgradeMath

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Economy/UpgradeMath.cs`
- Test: `Assets/_Project/Tests/EditMode/UpgradeMathTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using IdleMart.Economy;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class UpgradeMathTests
    {
        [TestCase(100, 1.5f, 0, 100)]
        [TestCase(100, 1.5f, 1, 150)]
        [TestCase(100, 1.5f, 2, 225)]
        [TestCase(40, 1.15f, 3, 61)]
        public void Cost_GrowsExponentially(long baseCost, float growth, int level, long expected)
        {
            Assert.AreEqual(expected, UpgradeMath.Cost(baseCost, growth, level));
        }

        [Test]
        public void Value_AddsLinearStepPerLevel()
        {
            Assert.AreEqual(10f, UpgradeMath.Value(10f, 2f, 0), 1e-5);
            Assert.AreEqual(16f, UpgradeMath.Value(10f, 2f, 3), 1e-5);
        }
    }
}
```

- [ ] **Step 2: Run tests** — Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
using System;

namespace IdleMart.Economy
{
    /// <summary>Balance formulas shared by all upgradable objects.</summary>
    public static class UpgradeMath
    {
        /// <summary>Price of the next upgrade: <c>baseCost * growth^level</c>, rounded.</summary>
        public static long Cost(long baseCost, float growth, int level)
        {
            return (long)Math.Round(baseCost * Math.Pow(growth, level), MidpointRounding.AwayFromZero);
        }

        /// <summary>Stat value at a level: <c>baseValue + perLevel * level</c>.</summary>
        public static float Value(float baseValue, float perLevel, int level)
        {
            return baseValue + perLevel * level;
        }
    }
}
```

- [ ] **Step 4: Run tests** — Expected: all pass.
- [ ] **Step 5: Commit** — `git commit -am "feat: add UpgradeMath"` (after `git add Assets/_Project`)

### Task 6: IncomeTracker

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Economy/IncomeTracker.cs`
- Test: `Assets/_Project/Tests/EditMode/IncomeTrackerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using IdleMart.Economy;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class IncomeTrackerTests
    {
        [Test]
        public void PerSecond_IsSumInWindowDividedByWindow()
        {
            var clock = new FakeClock();
            var tracker = new IncomeTracker(clock, windowSeconds: 10);

            tracker.Record(30);
            clock.Advance(5);
            tracker.Record(20);

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
```

- [ ] **Step 2: Run tests** — Expected: compile error.
- [ ] **Step 3: Implement**

```csharp
using System.Collections.Generic;
using IdleMart.Core;

namespace IdleMart.Economy
{
    /// <summary>
    /// Rolling average of income over a time window.
    /// Used for the HUD "$/sec" and as the rate for offline income.
    /// </summary>
    public sealed class IncomeTracker
    {
        private readonly IClock _clock;
        private readonly double _windowSeconds;
        private readonly Queue<(double time, long amount)> _records = new Queue<(double, long)>();
        private long _sum;

        public IncomeTracker(IClock clock, double windowSeconds = 60)
        {
            _clock = clock;
            _windowSeconds = windowSeconds;
        }

        public double PerSecond
        {
            get
            {
                Prune();
                return _sum / _windowSeconds;
            }
        }

        public void Record(long amount)
        {
            _records.Enqueue((_clock.Now, amount));
            _sum += amount;
        }

        private void Prune()
        {
            var threshold = _clock.Now - _windowSeconds;
            while (_records.Count > 0 && _records.Peek().time < threshold)
                _sum -= _records.Dequeue().amount;
        }
    }
}
```

- [ ] **Step 4: Run tests** — Expected: pass.
- [ ] **Step 5: Commit** — `git add Assets/_Project; git commit -m "feat: add IncomeTracker"`

### Task 7: OfflineIncome

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Economy/OfflineIncome.cs`
- Test: `Assets/_Project/Tests/EditMode/OfflineIncomeTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
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
```

- [ ] **Step 2: Run tests** — Expected: compile error.
- [ ] **Step 3: Implement**

```csharp
using System;

namespace IdleMart.Economy
{
    /// <summary>Money earned while the game was closed.</summary>
    public static class OfflineIncome
    {
        /// <summary>Absences shorter than this give nothing (no popup on quick restarts).</summary>
        public const long MinSeconds = 60;

        public static long Calculate(double perSecond, long elapsedSeconds, long capSeconds, float efficiency)
        {
            if (elapsedSeconds < MinSeconds || perSecond <= 0) return 0;

            var seconds = Math.Min(elapsedSeconds, capSeconds);
            return (long)Math.Floor(perSecond * seconds * efficiency);
        }
    }
}
```

- [ ] **Step 4: Run tests** — Expected: pass.
- [ ] **Step 5: Commit** — `git add Assets/_Project; git commit -m "feat: add OfflineIncome"`

### Task 8: LevelTable and PlayerProgress

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Progression/LevelTable.cs`
- Create: `Assets/_Project/Scripts/Runtime/Progression/PlayerProgress.cs`
- Test: `Assets/_Project/Tests/EditMode/LevelTableTests.cs`, `PlayerProgressTests.cs`

- [ ] **Step 1: Failing tests**

`LevelTableTests.cs`:
```csharp
using IdleMart.Progression;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class LevelTableTests
    {
        // Reach level 2 at 10 XP, level 3 at 30 XP, level 4 at 60 XP.
        private readonly LevelTable _table = new LevelTable(new[] { 10, 30, 60 });

        [TestCase(0, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(59, 3)]
        [TestCase(60, 4)]
        [TestCase(999, 4)]
        public void LevelFor_UsesCumulativeThresholds(int xp, int level)
        {
            Assert.AreEqual(level, _table.LevelFor(xp));
        }

        [Test]
        public void MaxLevel_IsThresholdCountPlusOne()
        {
            Assert.AreEqual(4, _table.MaxLevel);
        }

        [TestCase(0, 0f)]
        [TestCase(20, 0.5f)]
        [TestCase(999, 1f)]
        public void Progress01_IsFractionOfCurrentLevel(int xp, float expected)
        {
            Assert.AreEqual(expected, _table.Progress01(xp), 1e-5);
        }
    }
}
```

`PlayerProgressTests.cs`:
```csharp
using System.Collections.Generic;
using IdleMart.Progression;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class PlayerProgressTests
    {
        [Test]
        public void AddXp_CrossingThresholds_RaisesLevelUpForEachLevel()
        {
            var progress = new PlayerProgress(new LevelTable(new[] { 10, 30 }));
            var levels = new List<int>();
            progress.LevelUp += levels.Add;

            progress.AddXp(35);

            Assert.AreEqual(3, progress.Level);
            CollectionAssert.AreEqual(new[] { 2, 3 }, levels);
        }

        [Test]
        public void SetXp_DoesNotRaiseLevelUp()
        {
            var progress = new PlayerProgress(new LevelTable(new[] { 10 }));
            var raised = false;
            progress.LevelUp += _ => raised = true;

            progress.SetXp(50);

            Assert.AreEqual(2, progress.Level);
            Assert.IsFalse(raised);
        }
    }
}
```

- [ ] **Step 2: Run tests** — Expected: compile error.
- [ ] **Step 3: Implement `LevelTable.cs`**

```csharp
using System;

namespace IdleMart.Progression
{
    /// <summary>
    /// Maps total XP to a store level.
    /// <c>thresholds[i]</c> is the total XP needed to reach level <c>i + 2</c>.
    /// </summary>
    public sealed class LevelTable
    {
        private readonly int[] _thresholds;

        public LevelTable(int[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0) throw new ArgumentException("At least one threshold required.", nameof(thresholds));
            for (var i = 1; i < thresholds.Length; i++)
                if (thresholds[i] <= thresholds[i - 1]) throw new ArgumentException("Thresholds must be strictly increasing.", nameof(thresholds));
            _thresholds = (int[])thresholds.Clone();
        }

        public int MaxLevel => _thresholds.Length + 1;

        public int LevelFor(int xp)
        {
            var level = 1;
            while (level - 1 < _thresholds.Length && xp >= _thresholds[level - 1]) level++;
            return level;
        }

        /// <summary>0..1 progress inside the current level (1 at max level). Used by the XP bar.</summary>
        public float Progress01(int xp)
        {
            var level = LevelFor(xp);
            if (level >= MaxLevel) return 1f;

            var from = level == 1 ? 0 : _thresholds[level - 2];
            var to = _thresholds[level - 1];
            return (float)(xp - from) / (to - from);
        }
    }
}
```

- [ ] **Step 4: Implement `PlayerProgress.cs`**

```csharp
using System;

namespace IdleMart.Progression
{
    /// <summary>Player XP and store level. Raises <see cref="LevelUp"/> once per gained level.</summary>
    public sealed class PlayerProgress
    {
        private readonly LevelTable _table;

        public event Action<int> XpChanged;
        public event Action<int> LevelUp;

        public int Xp { get; private set; }
        public int Level { get; private set; } = 1;
        public float Progress01 => _table.Progress01(Xp);
        public LevelTable Table => _table;

        public PlayerProgress(LevelTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public void AddXp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Xp += amount;
            XpChanged?.Invoke(Xp);

            var newLevel = _table.LevelFor(Xp);
            while (Level < newLevel)
            {
                Level++;
                LevelUp?.Invoke(Level);
            }
        }

        /// <summary>Restores XP from a save without level-up events.</summary>
        public void SetXp(int xp)
        {
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
            Xp = xp;
            Level = _table.LevelFor(xp);
            XpChanged?.Invoke(Xp);
        }
    }
}
```

- [ ] **Step 5: Run tests** — Expected: pass.
- [ ] **Step 6: Commit** — `git add Assets/_Project; git commit -m "feat: add level table and player progress"`

### Task 9: Save system

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Save/SaveData.cs`, `ISaveStorage.cs`, `FileSaveStorage.cs`, `SaveService.cs`
- Create: `Assets/_Project/Tests/EditMode/InMemorySaveStorage.cs`
- Test: `Assets/_Project/Tests/EditMode/SaveServiceTests.cs`

- [ ] **Step 1: `InMemorySaveStorage.cs` + failing tests**

```csharp
using IdleMart.Save;

namespace IdleMart.Tests
{
    /// <summary>Save storage kept in memory for tests.</summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public string Content;
        public bool Exists => Content != null;
        public string Read() => Content;
        public void Write(string content) => Content = content;
        public void Delete() => Content = null;
    }
}
```

```csharp
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
```

- [ ] **Step 2: Run tests** — Expected: compile error.

- [ ] **Step 3: `SaveData.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace IdleMart.Save
{
    /// <summary>Everything persisted between sessions. Plain DTO for <see cref="UnityEngine.JsonUtility"/>.</summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bump when the format changes and add a migration in <see cref="SaveService"/>.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long money;
        public int xp;
        public long savedAtUnix;
        public double incomePerSecond;
        public int stockers;
        public List<BuiltObjectData> built = new List<BuiltObjectData>();
        public List<string> unlockedZones = new List<string>();
    }

    /// <summary>An object placed in a build slot.</summary>
    [Serializable]
    public sealed class BuiltObjectData
    {
        public string slotId;
        public string buildableId;
        public int level;
        public bool hasCashier;
    }
}
```

- [ ] **Step 4: `ISaveStorage.cs`**

```csharp
namespace IdleMart.Save
{
    /// <summary>Where the save text lives. File on device, memory in tests.</summary>
    public interface ISaveStorage
    {
        bool Exists { get; }
        string Read();
        void Write(string content);
        void Delete();
    }
}
```

- [ ] **Step 5: `FileSaveStorage.cs`**

```csharp
using System.IO;

namespace IdleMart.Save
{
    /// <summary>
    /// Stores the save in a file. Writes go to a temp file first and then replace the
    /// real one, so a crash during saving never leaves a half-written save.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _path;

        public FileSaveStorage(string path)
        {
            _path = path;
        }

        public bool Exists => File.Exists(_path);

        public string Read() => File.ReadAllText(_path);

        public void Write(string content)
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, content);

            if (File.Exists(_path)) File.Replace(temp, _path, null);
            else File.Move(temp, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
```

- [ ] **Step 6: `SaveService.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMart.Save
{
    /// <summary>Serialises <see cref="SaveData"/> and validates it on load.</summary>
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;

        public SaveService(ISaveStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public bool HasSave => _storage.Exists;

        public void Save(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
            _storage.Write(JsonUtility.ToJson(data));
        }

        /// <summary>Loads the save, or returns null if there is none or it cannot be used.</summary>
        public SaveData Load()
        {
            if (!_storage.Exists) return null;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(_storage.Read());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save file is corrupt, starting a new game. {e.Message}");
                return null;
            }

            if (data == null || data.version < 1 || data.version > SaveData.CurrentVersion)
            {
                Debug.LogWarning($"Save version {data?.version} is not supported, starting a new game.");
                return null;
            }

            // Future migrations: if (data.version < 2) { ...; data.version = 2; }

            data.built ??= new List<BuiltObjectData>();
            data.unlockedZones ??= new List<string>();
            return data;
        }

        public void Delete() => _storage.Delete();
    }
}
```

- [ ] **Step 7: Run tests** — Expected: all EditMode tests pass (≈25).
- [ ] **Step 8: Commit** — `git add Assets/_Project; git commit -m "feat: add save system with atomic file storage"`

---

## Next plans
- Plan 2 — World: URP look, Kenney assets, store scene, configs (ScriptableObjects), build slots, expansion zones, NavMesh AI (customers, stockers, cashiers), `GameBootstrap`.
- Plan 3 — UI & delivery: Boot/loading, main menu, settings, HUD & panels, offline popup, tweener, audio, optional features, README, MCP removal, GitHub.
