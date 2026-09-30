# Idle Mart — Design Spec

Test task for Midnight.Works (Unity Developer). Simple 3D Idle Tycoon, supermarket setting.

## Constraints (from the task)
- Unity **6000.3.17f1**, URP.
- Only free assets (Kenney CC0 kits: Furniture, Food, Mini Characters, City).
- **No third-party libraries** (no Zenject, DOTween, etc.) — only native Unity packages
  (uGUI, TextMeshPro, AI Navigation, Input System, Test Framework).
- Dev-only MCP packages (`com.ivanmurzak.unity.mcp*`) are removed before delivery.
- Delivery: GitHub repository with README.

## Gameplay
Camera + click control (no player character). Top-down/isometric camera with pan (drag/WASD) and zoom (wheel), clamped to store bounds.

Core loop: customer enters → picks a product type → walks to a shelf with that product → takes one item (shelf stock −1) → queues at a checkout → pays → player gets money + XP → money buys shelves, checkouts, staff, upgrades, expansions → more customers.

If no shelf has the wanted product in stock, the customer shows a bubble ("No milk!") and leaves without buying.

## Systems
1. **Game core** — `Boot` scene creates services; `Game` scene runs the loop; `CustomerSpawner` spawns customers at an interval derived from store level + parking upgrades.
2. **Build system** — predefined `BuildSlot`s in the store. Clicking an empty slot opens a build panel filtered by slot type (shelf slot / checkout slot / storage slot). Built objects are upgradable (levels raise capacity, item price, speed).
3. **Expansion** — `ExpansionZone`s (second hall, parking, bigger storage) purchasable after a required store level; on purchase the blocking wall/fence is removed and the zone's slots become active; NavMesh areas are pre-baked and gated by obstacles.
4. **AI** — NavMeshAgent + explicit state machines:
   - Customer: Enter → ChooseProduct → WalkToShelf → Pick → Queue → Pay → Leave.
   - Stocker (hired): FindShelfNeedingStock → WalkToStorage → Carry → Restock → Idle.
   - Cashier (hired, assigned to a checkout): serves queue automatically.
   - Checkout without cashier serves the front customer only when the player clicks it.
5. **Resources** — money (long) and XP. `EconomyService` owns balance, `CanAfford/Spend/Add`, raises `MoneyChanged`.
6. **Progression** — store level from XP thresholds (`LevelConfig`). Levels unlock products, expansion zones, staff limits. **Offline income**: on load, income rate × elapsed time, capped (e.g. 2 h), shown in a popup.
7. **Save** — `SaveService` serialises a `SaveData` DTO with `JsonUtility` to `Application.persistentDataPath/save.json`; versioned; autosave every 30 s, on pause and on quit; write via temp file + replace to avoid corruption. Corrupt/missing file → new game.

## UI/UX (uGUI + TextMeshPro)
- Loading screen (async scene load with progress bar).
- Main menu: Continue (if save exists) / New Game (confirm overwrite) / Settings / Quit.
- Settings: music volume, SFX volume, quality level, fullscreen; stored in PlayerPrefs; available from menu and in-game pause.
- HUD: money, store level, XP bar, buttons Build / Staff / Pause.
- Panels: build panel, object upgrade popup, staff hire panel, expansion purchase popup, offline income popup, floating "+$" texts.
- Own minimal tweener (coroutine based) for UI animations.

## Architecture
- `GameBootstrap` — composition root; creates plain C# services and injects dependencies manually into scene components (no DI framework, no singletons except the bootstrap holder).
- Services: `EconomyService`, `ProgressionService`, `SaveService`, `BuildService`, `StaffService`, `CustomerSpawner`, `SettingsService`, `SceneLoader`.
- Data in ScriptableObjects: `ProductConfig`, `BuildableConfig` (cost, upgrade curve, prefab, slot type), `LevelConfig`, `ExpansionConfig`, `EconomyConfig`. New buildable/product = new asset, no code change.
- Systems communicate via C# events.
- Pure logic (economy, progression, offline income, save DTO mapping) kept out of MonoBehaviours so it is unit-testable.
- Folder layout: `Assets/_Project/{Scripts/{Core,Economy,Progression,Save,Build,AI,UI,Settings},Configs,Prefabs,Scenes,Art}`.
- Assembly definitions: `IdleMart.Runtime`, `IdleMart.Tests.EditMode`.

## Testing
- EditMode tests: economy (spend/afford/add), progression thresholds and unlocks, offline income cap, save round-trip and version fallback.
- Manual play-through in the editor via MCP: build → customers buy → hire → expand → save/reload → offline popup.

## Optional (only if time remains)
- Marketing campaign: timed boost to customer flow for money.
- AI speech bubbles (already needed for "out of stock"; extend with a few random lines).

## Out of scope
Player character, multiplayer, IAP/ads, localisation (UI text is English only).
