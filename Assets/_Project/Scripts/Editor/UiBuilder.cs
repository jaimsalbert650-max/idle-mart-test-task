using System.Linq;
using IdleMart.Core;
using IdleMart.Settings;
using IdleMart.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Builds all UI from code with one consistent style: the Boot scene (loading screen + audio),
    /// the Main Menu scene, the shared Settings panel and the in-game UI of the Game scene.
    /// </summary>
    public static class UiBuilder
    {
        private const string UiPrefabs = ContentBuilder.Prefabs + "/UI";
        private const string BootScene = ContentBuilder.Root + "/Scenes/Boot.unity";
        private const string MenuScene = ContentBuilder.Root + "/Scenes/MainMenu.unity";

        // Palette
        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.22f, 0.94f);
        private static readonly Color PanelLight = new Color(0.2f, 0.23f, 0.32f, 1f);
        private static readonly Color Accent = new Color(0.25f, 0.62f, 0.4f);
        private static readonly Color Warning = new Color(0.86f, 0.36f, 0.3f);
        private static readonly Color Neutral = new Color(0.32f, 0.36f, 0.48f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.29f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.97f);
        private static readonly Color MutedText = new Color(0.72f, 0.75f, 0.84f);

        private static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        [MenuItem("Idle Mart/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            ConfigurePlayer();
            ContentBuilder.BuildAll();
            MusicGenerator.Generate();
            BuildSharedPrefabs();
            BuildBootScene();
            BuildMenuScene();
            SceneBuilder.Build();
            BuildGameUi();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SetBuildOrder();
            Debug.Log("Idle Mart: everything built.");
        }

        [MenuItem("Idle Mart/Build UI (menus + game UI)", priority = 3)]
        public static void BuildUiOnly()
        {
            BuildSharedPrefabs();
            BuildBootScene();
            BuildMenuScene();
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            BuildGameUi();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SetBuildOrder();
        }

        /// <summary>Player settings the game relies on (an idle game must keep running in the background).</summary>
        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Idle Mart";
            PlayerSettings.companyName = "IdleMartStudio";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        // ---------- Primitives ----------

        private static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Anchor(this RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(this RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        private static Image Box(Transform parent, string name, Color color, bool rounded = true)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = RoundedSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.5f;
            }

            return image;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var rect = Rect(parent, name);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button TextButton(Transform parent, string name, string text, Color color, Vector2 size, float fontSize = 34f)
        {
            var image = Box(parent, name, color);
            ((RectTransform)image.transform).sizeDelta = size;
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;

            var label = Label(image.transform, "Label", text, fontSize, TextColor, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch();
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = size.y;
            element.preferredWidth = size.x;
            return button;
        }

        private static VerticalLayoutGroup Column(GameObject go, float spacing, int padding, TextAnchor align = TextAnchor.UpperCenter)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static void FitHeight(GameObject go)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Centered modal window with a dimmed background. Returns (root, content column).</summary>
        private static (GameObject root, Transform content) Modal(Transform parent, string name, string title, float width)
        {
            var root = Box(parent, name, new Color(0f, 0f, 0f, 0.55f), rounded: false);
            root.rectTransform.Stretch();

            var window = Box(root.transform, "Window", PanelColor);
            window.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0f));
            Column(window.gameObject, 22f, 36);
            FitHeight(window.gameObject);

            var header = Label(window.transform, "Title", title, 48f, Gold, TextAlignmentOptions.Center, true);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            return (root.gameObject, window.transform);
        }

        // ---------- Shared prefabs ----------

        private static void BuildSharedPrefabs()
        {
            System.IO.Directory.CreateDirectory(UiPrefabs);
            BuildOptionButtonPrefab();
            BuildSettingsPrefab();
            AssetDatabase.SaveAssets();
        }

        private static void BuildOptionButtonPrefab()
        {
            var root = Box(null, "OptionButton", Accent);
            root.rectTransform.sizeDelta = new Vector2(480f, 96f);
            var button = root.gameObject.AddComponent<Button>();
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            var title = Label(root.transform, "Title", "Title", 30f, TextColor, TextAlignmentOptions.BottomLeft, true);
            title.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(-180f, -10f));
            var subtitle = Label(root.transform, "Subtitle", "Subtitle", 21f, new Color(0.92f, 0.95f, 0.92f, 0.85f), TextAlignmentOptions.TopLeft);
            subtitle.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(20f, -2f), new Vector2(-180f, -8f));
            subtitle.enableWordWrapping = true;

            var tag = Box(root.transform, "PriceTag", new Color(0f, 0f, 0f, 0.3f));
            tag.rectTransform.Anchor(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(150f, 60f));
            var price = Label(tag.transform, "Price", "$0", 28f, Gold, TextAlignmentOptions.Center, true);
            price.rectTransform.Stretch();

            var option = root.gameObject.AddComponent<OptionButton>();
            option.EditorSetup(button, title, subtitle, price, root);

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{UiPrefabs}/OptionButton.prefab");
            Object.DestroyImmediate(root.gameObject);
        }

        private static Slider SliderRow(Transform parent, string label)
        {
            var row = Rect(parent, label + "Row");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var text = Label(row, "Label", label, 30f, TextColor);
            text.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var resources = new DefaultControls.Resources { standard = RoundedSprite, background = RoundedSprite, knob = KnobSprite };
            var sliderGo = DefaultControls.CreateSlider(resources);
            sliderGo.transform.SetParent(row, false);
            ((RectTransform)sliderGo.transform).Anchor(new Vector2(0.42f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 28f));
            sliderGo.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Accent;
            sliderGo.transform.Find("Background").GetComponent<Image>().color = PanelLight;
            return sliderGo.GetComponent<Slider>();
        }

        private static void BuildSettingsPrefab()
        {
            var holder = CreateCanvas("Holder", 0);
            var (root, content) = Modal(holder.transform, "SettingsPanel", "Settings", 720f);

            var music = SliderRow(content, "Music");
            var sfx = SliderRow(content, "Sound effects");

            var qualityRow = Rect(content, "QualityRow");
            qualityRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            Label(qualityRow, "Label", "Graphics", 30f, TextColor).rectTransform.Anchor(Vector2.zero, new Vector2(0.4f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var dropdownGo = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources { standard = RoundedSprite, background = RoundedSprite, dropdown = RoundedSprite, checkmark = KnobSprite });
            dropdownGo.transform.SetParent(qualityRow, false);
            ((RectTransform)dropdownGo.transform).Anchor(new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dropdown = dropdownGo.GetComponent<TMP_Dropdown>();
            dropdown.captionText.fontSize = 28f;
            dropdown.itemText.fontSize = 26f;
            dropdownGo.GetComponent<Image>().color = PanelLight;
            dropdown.captionText.color = TextColor;
            var template = dropdown.template;
            template.sizeDelta = new Vector2(0f, 300f);
            template.Find("Viewport/Content/Item").GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);
            template.Find("Viewport/Content").GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);

            var toggleRow = Rect(content, "FullscreenRow");
            toggleRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            Label(toggleRow, "Label", "Fullscreen", 30f, TextColor).rectTransform.Anchor(Vector2.zero, new Vector2(0.4f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var toggleGo = DefaultControls.CreateToggle(new DefaultControls.Resources { standard = RoundedSprite, checkmark = KnobSprite });
            toggleGo.transform.SetParent(toggleRow, false);
            ((RectTransform)toggleGo.transform).Anchor(new Vector2(0.42f, 0.5f), new Vector2(0.42f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            var background = toggleGo.transform.Find("Background");
            ((RectTransform)background).Anchor(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(48f, 48f));
            background.GetComponent<Image>().color = PanelLight;
            ((RectTransform)background.Find("Checkmark")).sizeDelta = new Vector2(30f, 30f);
            background.Find("Checkmark").GetComponent<Image>().color = Accent;
            Object.DestroyImmediate(toggleGo.transform.Find("Label").gameObject);

            var close = TextButton(content, "Close", "Done", Accent, new Vector2(0f, 80f));

            var panel = root.AddComponent<SettingsPanel>();
            panel.EditorSetup(root, music, sfx, dropdown, toggleGo.GetComponent<Toggle>(), close);

            root.transform.SetParent(null, false);
            PrefabUtility.SaveAsPrefabAsset(root, $"{UiPrefabs}/SettingsPanel.prefab");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(holder.gameObject);
        }

        private static SettingsPanel InstantiateSettings(Transform canvas)
        {
            var prefab = ContentBuilder.Load<GameObject>($"{UiPrefabs}/SettingsPanel.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            ((RectTransform)instance.transform).Stretch();
            return instance.GetComponent<SettingsPanel>();
        }

        // ---------- Boot ----------

        private static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);

            // Loading screen (persistent).
            var loaderGo = new GameObject("SceneLoader");
            var canvas = CreateCanvas("LoadingCanvas", 100);
            canvas.transform.SetParent(loaderGo.transform, false);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();

            var background = Box(canvas.transform, "Background", new Color(0.12f, 0.15f, 0.23f), rounded: false);
            background.rectTransform.Stretch();
            var title = Label(canvas.transform, "Title", "IDLE MART", 110f, Gold, TextAlignmentOptions.Center, true);
            title.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 160f));
            var loading = Label(canvas.transform, "Loading", "Loading...", 36f, MutedText, TextAlignmentOptions.Center);
            loading.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(800f, 60f));

            var barBack = Box(canvas.transform, "ProgressBack", PanelLight);
            barBack.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(700f, 34f));
            var fill = Box(barBack.transform, "Fill", Accent);
            fill.rectTransform.Stretch(4f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.3f;

            var loader = loaderGo.AddComponent<SceneLoader>();
            loader.EditorSetup(group, fill);

            // Audio (persistent).
            var audioGo = new GameObject("AudioService");
            var musicSource = audioGo.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            var sfxSource = audioGo.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            string[] clips = { "ui_click", "build", "error", "restock", "coin", "panel_open", "level_up" };
            audioGo.AddComponent<AudioService>().EditorSetup(musicSource, sfxSource,
                ContentBuilder.Load<AudioClip>(MusicGenerator.OutputPath),
                clips.Select(c => ContentBuilder.Load<AudioClip>($"{ContentBuilder.Root}/Audio/{c}.ogg")).ToArray());

            new GameObject("BootLoader").AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, BootScene);
        }

        // ---------- Main menu ----------

        private static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.15f, 0.23f);

            CreateEventSystem();
            var canvas = CreateCanvas("MenuCanvas", 0);

            var stripe = Box(canvas.transform, "Stripe", new Color(0.25f, 0.62f, 0.4f, 0.25f), rounded: false);
            stripe.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(0f, 260f));

            var title = Label(canvas.transform, "Title", "IDLE MART", 150f, Gold, TextAlignmentOptions.Center, true);
            title.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(1400f, 200f));
            var subtitle = Label(canvas.transform, "Subtitle", "Supermarket Tycoon", 44f, TextColor, TextAlignmentOptions.Center);
            subtitle.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 70f));

            var column = Rect(canvas.transform, "Buttons").Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 70f), new Vector2(520f, 0f));
            Column(column.gameObject, 22f, 0);
            FitHeight(column.gameObject);

            var cont = TextButton(column, "Continue", "Continue", Accent, new Vector2(520f, 96f), 40f);
            var newGame = TextButton(column, "NewGame", "New Game", Neutral, new Vector2(520f, 96f), 40f);
            var settings = TextButton(column, "Settings", "Settings", Neutral, new Vector2(520f, 96f), 40f);
            var quit = TextButton(column, "Quit", "Quit", Warning, new Vector2(520f, 96f), 40f);

            var credits = Label(canvas.transform, "Credits", "Test task for Midnight.Works  •  Art & sounds: Kenney (CC0)", 24f, MutedText, TextAlignmentOptions.Center);
            credits.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1400f, 40f));

            var settingsPanel = InstantiateSettings(canvas.transform);

            var (confirm, content) = Modal(canvas.transform, "ConfirmNewGame", "Start over?", 720f);
            var text = Label(content, "Text", "Your current store will be lost.", 32f, TextColor, TextAlignmentOptions.Center);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var yes = TextButton(content, "Yes", "Start new game", Warning, new Vector2(0f, 84f));
            var no = TextButton(content, "No", "Cancel", Neutral, new Vector2(0f, 84f));

            new GameObject("MainMenu").AddComponent<MainMenuController>()
                .EditorSetup(cont, newGame, settings, quit, settingsPanel, confirm, yes, no);

            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        // ---------- Game UI ----------

        private static void BuildGameUi()
        {
            var existing = Object.FindFirstObjectByType<GameUI>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            if (Object.FindFirstObjectByType<EventSystem>() == null) CreateEventSystem();

            var canvas = CreateCanvas("GameUI", 10);
            var game = Object.FindFirstObjectByType<GameBootstrap>();
            var optionPrefab = ContentBuilder.Load<GameObject>($"{UiPrefabs}/OptionButton.prefab").GetComponent<OptionButton>();

            // HUD: money + level (top-left).
            var moneyBox = Box(canvas.transform, "MoneyBox", PanelColor);
            moneyBox.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(380f, 120f));
            var money = Label(moneyBox.transform, "Money", "$0", 58f, Gold, TextAlignmentOptions.Left, true);
            money.rectTransform.Anchor(new Vector2(0f, 0.35f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -6f), new Vector2(-40f, 0f));
            var income = Label(moneyBox.transform, "Income", "$0/min", 26f, MutedText);
            income.rectTransform.Anchor(Vector2.zero, new Vector2(1f, 0.38f), new Vector2(0f, 0f), new Vector2(26f, 12f), new Vector2(-40f, 0f));

            var levelBox = Box(canvas.transform, "LevelBox", PanelColor);
            levelBox.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -24f), new Vector2(300f, 120f));
            var level = Label(levelBox.transform, "Level", "Lv 1", 44f, TextColor, TextAlignmentOptions.Center, true);
            level.rectTransform.Anchor(new Vector2(0f, 0.4f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), Vector2.zero);
            var xpBack = Box(levelBox.transform, "XpBack", PanelLight);
            xpBack.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-40f, 26f));
            var xpFill = Box(xpBack.transform, "XpFill", Accent);
            xpFill.rectTransform.Stretch(3f);
            xpFill.type = Image.Type.Filled;
            xpFill.fillMethod = Image.FillMethod.Horizontal;

            var hud = canvas.gameObject.AddComponent<HudView>();
            hud.EditorSetup(money, income, level, xpFill);

            // Top-right buttons.
            var pauseButton = TextButton(canvas.transform, "PauseButton", "II", Neutral, new Vector2(96f, 96f), 40f);
            ((RectTransform)pauseButton.transform).Anchor(Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -24f), new Vector2(96f, 96f));
            var staffButton = TextButton(canvas.transform, "StaffButton", "Staff", Accent, new Vector2(200f, 96f), 36f);
            ((RectTransform)staffButton.transform).Anchor(Vector2.one, Vector2.one, Vector2.one, new Vector2(-140f, -24f), new Vector2(200f, 96f));

            // Context panel (right side).
            var context = Box(canvas.transform, "ContextPanel", PanelColor);
            context.rectTransform.Anchor(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(540f, 0f));
            Column(context.gameObject, 14f, 26, TextAnchor.LowerCenter);
            FitHeight(context.gameObject);
            var contextTitle = Label(context.transform, "Title", "Title", 40f, Gold, TextAlignmentOptions.Left, true);
            contextTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            var contextBody = Label(context.transform, "Body", "Body", 26f, TextColor);
            contextBody.enableWordWrapping = true;
            var options = Rect(context.transform, "Options");
            Column(options.gameObject, 10f, 0);
            var close = TextButton(context.transform, "Close", "X", Warning, new Vector2(56f, 56f), 30f);
            close.GetComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)close.transform).Anchor(Vector2.one, Vector2.one, Vector2.one, new Vector2(-16f, -16f), new Vector2(56f, 56f));
            var contextPanel = context.gameObject.AddComponent<ContextPanel>();
            contextPanel.EditorSetup(context.gameObject, contextTitle, contextBody, options, optionPrefab, close);

            // Staff panel (below the staff button).
            var staff = Box(canvas.transform, "StaffPanel", PanelColor);
            staff.rectTransform.Anchor(Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -140f), new Vector2(540f, 0f));
            Column(staff.gameObject, 14f, 26);
            FitHeight(staff.gameObject);
            var staffTitle = Label(staff.transform, "Title", "Staff", 40f, Gold, TextAlignmentOptions.Left, true);
            staffTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            var staffBody = Label(staff.transform, "Body", "Body", 26f, TextColor);
            staffBody.enableWordWrapping = true;
            var hire = ((GameObject)PrefabUtility.InstantiatePrefab(optionPrefab.gameObject, staff.transform)).GetComponent<OptionButton>();
            var staffClose = TextButton(staff.transform, "Close", "X", Warning, new Vector2(56f, 56f), 30f);
            staffClose.GetComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)staffClose.transform).Anchor(Vector2.one, Vector2.one, Vector2.one, new Vector2(-16f, -16f), new Vector2(56f, 56f));
            var staffPanel = staff.gameObject.AddComponent<StaffPanel>();
            staffPanel.EditorSetup(staff.gameObject, staffBody, hire, staffClose);

            // Toast (top-center) and hint (bottom-center).
            var toastBox = Box(canvas.transform, "Toast", new Color(0.1f, 0.1f, 0.14f, 0.92f));
            toastBox.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(900f, 80f));
            var toastText = Label(toastBox.transform, "Text", "Message", 32f, TextColor, TextAlignmentOptions.Center, true);
            toastText.rectTransform.Stretch(10f);
            // The view lives on the canvas: coroutines cannot start on the (initially hidden) toast object.
            var toast = canvas.gameObject.AddComponent<ToastView>();
            toast.EditorSetup(toastBox.gameObject, toastText);

            var hintBox = Box(canvas.transform, "Hint", new Color(0.1f, 0.1f, 0.14f, 0.85f));
            hintBox.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 24f), new Vector2(1000f, 76f));
            var hintText = Label(hintBox.transform, "Text", "Hint", 28f, TextColor, TextAlignmentOptions.Center);
            hintText.rectTransform.Stretch(10f);
            hintBox.raycastTarget = false;

            // Pause menu.
            var (pause, pauseContent) = Modal(canvas.transform, "PauseMenu", "Paused", 620f);
            var resume = TextButton(pauseContent, "Resume", "Resume", Accent, new Vector2(0f, 90f));
            var settingsButton = TextButton(pauseContent, "Settings", "Settings", Neutral, new Vector2(0f, 90f));
            var menu = TextButton(pauseContent, "MainMenu", "Save & Main Menu", Warning, new Vector2(0f, 90f));

            // Offline income popup.
            var (offline, offlineContent) = Modal(canvas.transform, "OfflinePopup", "Welcome back!", 720f);
            var offlineText = Label(offlineContent, "Text", "", 34f, TextColor, TextAlignmentOptions.Center);
            offlineText.gameObject.AddComponent<LayoutElement>().preferredHeight = 200f;
            var collect = TextButton(offlineContent, "Collect", "Collect", Accent, new Vector2(0f, 90f));

            var settingsPanel = InstantiateSettings(canvas.transform);

            var ui = canvas.gameObject.AddComponent<GameUI>();
            ui.EditorSetup(game, hud, contextPanel, staffPanel, settingsPanel, toast, staffButton, pauseButton,
                pause, resume, settingsButton, menu, offline, offlineText, collect, hintBox.gameObject, hintText);
        }

        private static void SetBuildOrder()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScene, true),
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(SceneBuilder.ScenePath, true)
            };
        }
    }
}
