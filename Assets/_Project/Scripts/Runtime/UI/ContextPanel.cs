using System;
using System.Collections.Generic;
using IdleMart.Configs;
using IdleMart.Core;
using IdleMart.Settings;
using IdleMart.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMart.UI
{
    /// <summary>
    /// Panel for whatever the player clicked: build options for an empty slot, upgrades for shelves
    /// and checkouts, purchase for expansion zones. Redraws when money, level or the object changes.
    /// </summary>
    public sealed class ContextPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Transform optionsRoot;
        [SerializeField] private OptionButton optionPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<OptionButton> _options = new List<OptionButton>();
        private GameBootstrap _game;
        private ToastView _toast;
        private IClickable _target;
        private BuiltObject _watched;
        private int _used;

        private GameServices Services => _game.Services;

        public void Init(GameBootstrap game, ToastView toast)
        {
            _game = game;
            _toast = toast;
            panel.SetActive(false);

            closeButton.onClick.AddListener(Close);
            game.ClickInput.Clicked += Select;
            Services.Wallet.Changed += _ => Refresh();
            Services.StoreChanged += Refresh;
            Services.Progress.LevelUp += _ => Refresh();
        }

        public void Select(IClickable clickable)
        {
            if (clickable is BuildSlot slot && !slot.IsEmpty) clickable = slot.Built;
            if (clickable == null)
            {
                Close();
                return;
            }

            Watch(clickable as BuiltObject);
            var wasOpen = panel.activeSelf && _target != null;
            _target = clickable;
            Refresh();
            if (!wasOpen)
            {
                UiTween.Show(panel);
                AudioService.Play(Sfx.PanelOpen);
            }
        }

        public void Close()
        {
            _target = null;
            Watch(null);
            UiTween.Hide(panel);
        }

        private void Watch(BuiltObject built)
        {
            if (_watched != null) _watched.Changed -= Refresh;
            _watched = built;
            if (_watched != null) _watched.Changed += Refresh;
        }

        private void Refresh()
        {
            if (_target == null) return;
            _used = 0;

            switch (_target)
            {
                case BuildSlot slot: ShowSlot(slot); break;
                case Shelf shelf: ShowShelf(shelf); break;
                case Checkout checkout: ShowCheckout(checkout); break;
                case ExpansionZone zone: ShowZone(zone); break;
                default:
                    Close();
                    return;
            }

            for (var i = _used; i < _options.Count; i++) _options[i].gameObject.SetActive(false);
        }

        // ---------- Content ----------

        private void ShowSlot(BuildSlot slot)
        {
            title.text = slot.Kind == BuildableKind.Checkout ? "Build a checkout" : "Build a shelf";
            body.text = slot.Kind == BuildableKind.Checkout
                ? "Customers pay here. Click it to serve, or hire a cashier."
                : "Choose what to sell here.";

            foreach (var config in Services.Config.buildables)
            {
                if (config.kind != slot.Kind) continue;

                var result = _game.Store.CanBuild(slot, config);
                var subtitle = result == PurchaseResult.LevelTooLow
                    ? $"Unlocks at level {config.unlockLevel}"
                    : config.kind == BuildableKind.Shelf
                        ? $"Sells {config.product.displayName.ToLower()} for {NumberFormat.Money(config.SalePriceAt(1))}"
                        : $"Serves a customer every {config.ServiceTimeAt(1):0.0}s with a cashier";

                AddOption(config.displayName, subtitle, NumberFormat.Money(config.buildCost), result == PurchaseResult.Ok, () =>
                {
                    var buildResult = _game.Store.TryBuild(slot, config);
                    if (Report(buildResult, config.unlockLevel)) Select(slot);
                });
            }
        }

        private void ShowShelf(Shelf shelf)
        {
            title.text = $"{shelf.Config.displayName}  <size=70%>Lv {shelf.Level}</size>";
            body.text = $"Stock: {shelf.Stock}/{shelf.Capacity}    Price: {NumberFormat.Money(shelf.SalePrice)}\n" +
                        "<size=85%>Click the shelf to restock it by hand, or hire stockers.</size>";

            AddUpgrade(shelf, shelf.IsMaxLevel
                ? "Maximum level reached"
                : $"Capacity {shelf.Capacity} → {shelf.Config.CapacityAt(shelf.Level + 1)}, price {NumberFormat.Money(shelf.SalePrice)} → {NumberFormat.Money(shelf.Config.SalePriceAt(shelf.Level + 1))}");

            AddOption("Restock by hand", shelf.Missing > 0 ? $"{shelf.Missing} items missing" : "Shelf is full", "Free", shelf.Missing > 0, () =>
            {
                shelf.OnClicked();
                AudioService.Play(Sfx.Restock);
            });

            AddOption("Sell shelf", "Frees the spot for another product", "+" + NumberFormat.Money(shelf.SellValue), true, () =>
            {
                var slot = shelf.Slot;
                if (!_game.Store.TrySell(shelf)) return;
                AudioService.Play(Sfx.Coin);
                Select(slot);
            });
        }

        private void ShowCheckout(Checkout checkout)
        {
            title.text = $"Checkout  <size=70%>Lv {checkout.Level}</size>";
            body.text = $"Queue: {checkout.QueueLength}    Service: {checkout.ServiceTime:0.0}s\n" +
                        (checkout.HasCashier ? "<size=85%>A cashier serves customers automatically.</size>" : "<size=85%>No cashier: click the checkout to serve each customer.</size>");

            AddUpgrade(checkout, checkout.IsMaxLevel
                ? "Maximum level reached"
                : $"Service time {checkout.ServiceTime:0.0}s → {checkout.Config.ServiceTimeAt(checkout.Level + 1):0.0}s");

            if (!checkout.HasCashier)
            {
                AddOption("Hire cashier", "Serves customers automatically", NumberFormat.Money(checkout.CashierHireCost),
                    Services.Wallet.CanAfford(checkout.CashierHireCost), () =>
                    {
                        if (checkout.TryHireCashier()) AudioService.Play(Sfx.Build);
                        else Fail("Not enough money");
                    });
            }
        }

        private void ShowZone(ExpansionZone zone)
        {
            var config = zone.Config;
            title.text = config.displayName;
            body.text = $"{config.description}\n<size=85%>+{config.extraCustomersPerMinute:0} customers per minute</size>";

            var result = _game.Store.CanUnlock(zone);
            AddOption("Buy expansion", result == PurchaseResult.LevelTooLow ? $"Requires level {config.requiredLevel}" : "Opens immediately",
                NumberFormat.Money(config.cost), result == PurchaseResult.Ok, () =>
                {
                    if (Report(_game.Store.TryUnlock(zone), config.requiredLevel)) Close();
                });
        }

        private void AddUpgrade(BuiltObject built, string subtitle)
        {
            AddOption(built.IsMaxLevel ? "Max level" : $"Upgrade to Lv {built.Level + 1}", subtitle,
                built.IsMaxLevel ? "" : NumberFormat.Money(built.NextUpgradeCost),
                !built.IsMaxLevel && Services.Wallet.CanAfford(built.NextUpgradeCost), () =>
                {
                    if (built.IsMaxLevel) return;
                    if (built.TryUpgrade())
                    {
                        AudioService.Play(Sfx.Build);
                        WorldFx.ShowPuff(built.transform.position);
                    }
                    else Fail("Not enough money");
                });
        }

        private void AddOption(string optionTitle, string subtitle, string price, bool enabled, Action onClick)
        {
            if (_used == _options.Count) _options.Add(Instantiate(optionPrefab, optionsRoot));
            var option = _options[_used++];
            option.gameObject.SetActive(true);
            option.Set(optionTitle, subtitle, price, enabled, onClick);
        }

        /// <summary>Plays feedback for a purchase. Returns true on success.</summary>
        private bool Report(PurchaseResult result, int requiredLevel)
        {
            switch (result)
            {
                case PurchaseResult.Ok:
                    AudioService.Play(Sfx.Build);
                    return true;
                case PurchaseResult.NotEnoughMoney:
                    Fail("Not enough money");
                    return false;
                case PurchaseResult.LevelTooLow:
                    Fail($"Requires store level {requiredLevel}");
                    return false;
                default:
                    return false;
            }
        }

        private void Fail(string message)
        {
            AudioService.Play(Sfx.Error);
            _toast.Show(message);
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject root, TMP_Text titleText, TMP_Text bodyText, Transform options, OptionButton prefab, Button close)
        {
            panel = root;
            title = titleText;
            body = bodyText;
            optionsRoot = options;
            optionPrefab = prefab;
            closeButton = close;
        }
#endif
    }
}
