using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleMart.AI;
using IdleMart.Configs;
using IdleMart.World;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Generates materials, the character animator, prefabs and balance configs from the Kenney models.
    /// Re-running it is safe: prefabs are overwritten in place (GUIDs kept) and existing configs
    /// keep their tuned values — only missing configs are created.
    /// </summary>
    public static class ContentBuilder
    {
        public const string Root = "Assets/_Project";
        public const string Art = Root + "/Art/Kenney";
        public const string Materials = Root + "/Art/Materials";
        public const string Animation = Root + "/Art/Animation";
        public const string Prefabs = Root + "/Prefabs";
        public const string Configs = Root + "/Configs";

        private const string CharacterClipSource = Art + "/mini-characters/character-male-a.fbx";
        private const string EmployeeModel = Art + "/mini-market/character-employee.fbx";

        [MenuItem("Idle Mart/Build Content (prefabs + configs)", priority = 1)]
        public static void BuildAll()
        {
            EnsureFolders();
            NormalizeFurnitureScale();
            var controller = BuildAnimator();
            BuildPrefabs(controller);
            BuildConfigs();
            AssetDatabase.SaveAssets();
            Debug.Log("Idle Mart: content built.");
        }

        public static GameObject Model(string relative) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/{relative}.fbx");

        public static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { Materials, Animation, Prefabs, Prefabs + "/Buildables", Prefabs + "/Characters", Prefabs + "/Fx", Configs, Configs + "/Products", Configs + "/Buildables", Configs + "/Expansions" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        public const string UiArt = Root + "/Art/UI";

        /// <summary>Kenney Future as a static TMP font (ASCII), with the default TMP font as fallback for other glyphs.</summary>
        public static TMP_FontAsset UiFont()
        {
            var path = $"{UiArt}/KenneyFuture SDF.asset";
            var existing = Load<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = Load<Font>($"{UiArt}/Kenney Future.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            asset.name = "KenneyFuture SDF";
            var ascii = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            asset.TryAddCharacters(ascii);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };

            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = "KenneyFuture Material";
            asset.atlasTextures[0].name = "KenneyFuture Atlas";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Imports a UI PNG as a sliced sprite (border in pixels: left, bottom, right, top).</summary>
        public static Sprite UiSprite(string file, Vector4 border = default)
        {
            var path = $"{UiArt}/{file}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }

            return Load<Sprite>(path);
        }

        /// <summary>Furniture Kit is authored ~10x larger than the Mini series; bring it to the same scale.</summary>
        private static void NormalizeFurnitureScale()
        {
            foreach (var file in Directory.GetFiles($"{Art}/furniture-kit", "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                if (Mathf.Approximately(importer.globalScale, 0.1f)) continue;
                importer.globalScale = 0.1f;
                importer.SaveAndReimport();
            }
        }

        // ---------- Materials ----------

        public static Material ColorMaterial(string name, Color color, bool unlit = true)
        {
            var path = $"{Materials}/{name}.mat";
            var material = Load<Material>(path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ParticleMaterial()
        {
            var path = $"{Materials}/Puff.mat";
            var material = Load<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------- Animator ----------

        private static AnimatorController BuildAnimator()
        {
            // Make locomotion clips loop.
            var importer = (ModelImporter)AssetImporter.GetAtPath(CharacterClipSource);
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
                clip.loopTime = clip.name is "idle" or "walk" or "sprint";
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            var all = AssetDatabase.LoadAllAssetsAtPath(CharacterClipSource).OfType<AnimationClip>().ToList();
            AnimationClip Clip(string clipName) => all.First(c => c.name == clipName);

            var path = $"{Animation}/Character.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sm = controller.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = Clip("idle");
            sm.defaultState = idle;

            var walk = sm.AddState("Walk");
            walk.motion = Clip("walk");

            var toWalk = idle.AddTransition(walk);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.1f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var toIdle = walk.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AddAction(controller, sm, idle, "PickUp", Clip("pick-up"));
            AddAction(controller, sm, idle, "Interact", Clip("interact-right"));
            AddAction(controller, sm, idle, "Yes", Clip("emote-yes"));
            AddAction(controller, sm, idle, "No", Clip("emote-no"));

            return controller;
        }

        private static void AddAction(AnimatorController controller, AnimatorStateMachine sm, AnimatorState idle, string trigger, AnimationClip clip)
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            var state = sm.AddState(trigger);
            state.motion = clip;

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.1f;
        }

        // ---------- Prefabs ----------

        private static void BuildPrefabs(AnimatorController controller)
        {
            var barBack = ColorMaterial("BarBack", new Color(0.12f, 0.12f, 0.15f));
            var barFill = ColorMaterial("BarFill", Color.white);
            var coin = ColorMaterial("Coin", new Color(1f, 0.8f, 0.15f));

            foreach (var (id, model) in ShelfModels)
                SavePrefab(BuildShelf(id, Model(model), barBack, barFill), $"{Prefabs}/Buildables/{id}.prefab");
            SavePrefab(BuildCheckout(coin), $"{Prefabs}/Buildables/checkout.prefab");

            foreach (var character in Directory.GetFiles($"{Art}/mini-characters", "character-*.fbx"))
            {
                var name = Path.GetFileNameWithoutExtension(character);
                SavePrefab(BuildCustomer(Load<GameObject>(character.Replace('\\', '/')), controller), $"{Prefabs}/Characters/Customer_{name}.prefab");
            }

            SavePrefab(BuildStocker(controller), $"{Prefabs}/Characters/Stocker.prefab");
            SavePrefab(BuildCashier(controller), $"{Prefabs}/Characters/Cashier.prefab");

            SavePrefab(BuildLabel("FloatingText", 3.2f, Color.white), $"{Prefabs}/Fx/FloatingText.prefab");
            SavePrefab(BuildLabel("Bubble", 2.4f, Color.white), $"{Prefabs}/Fx/Bubble.prefab");
            SavePrefab(BuildPuff(), $"{Prefabs}/Fx/Puff.prefab");
        }

        /// <summary>Buildable id → Kenney model. Kept in one place so configs and prefabs match.</summary>
        private static readonly (string id, string model)[] ShelfModels =
        {
            ("shelf_fruits", "mini-market/display-fruit"),
            ("shelf_bread", "mini-market/display-bread"),
            ("shelf_groceries", "mini-market/shelf-boxes"),
            ("shelf_snacks", "mini-market/shelf-bags"),
            ("shelf_drinks", "mini-market/freezer"),
            ("shelf_frozen", "mini-market/freezers-standing"),
        };

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static GameObject AddModel(Transform parent, GameObject model)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = "Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static Bounds LocalBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            bounds.center -= go.transform.position;
            return bounds;
        }

        private static void AddClickAndObstacle(GameObject root, Bounds bounds)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = new Vector3(bounds.size.x * 0.9f, bounds.size.y, bounds.size.z * 0.9f);
            obstacle.carving = true;
        }

        private static Transform Point(Transform parent, string name, Vector3 position, float yaw)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
            point.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return point;
        }

        private static GameObject Quad(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            quad.transform.localScale = scale;
            quad.GetComponent<Renderer>().sharedMaterial = material;
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad;
        }

        private static GameObject BuildShelf(string id, GameObject model, Material barBack, Material barFill)
        {
            var root = new GameObject(id);
            var modelInstance = AddModel(root.transform, model);
            var bounds = LocalBounds(modelInstance);
            AddClickAndObstacle(root, bounds);

            // Customers stand in front (+Z) and face the shelf.
            var stand = Point(root.transform, "StandPoint", new Vector3(0f, 0f, bounds.max.z + 0.35f), 180f);

            var bar = new GameObject("StockBar").transform;
            bar.SetParent(root.transform, false);
            bar.localPosition = new Vector3(0f, bounds.max.y + 0.25f, 0f);
            Quad(bar, "Back", barBack, Vector3.zero, new Vector3(0.62f, 0.1f, 1f));
            var fill = Quad(bar, "Fill", barFill, new Vector3(0f, 0f, -0.001f), new Vector3(1f, 1f, 1f));
            var fillHolder = new GameObject("FillHolder").transform;
            fillHolder.SetParent(bar, false);
            fillHolder.localScale = new Vector3(0.58f, 0.07f, 1f);
            fill.transform.SetParent(fillHolder, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localScale = Vector3.one;

            var stockBar = bar.gameObject.AddComponent<StockBar>();
            stockBar.EditorSetup(fill.transform, fill.GetComponent<Renderer>());

            var shelf = root.AddComponent<Shelf>();
            shelf.EditorSetup(stand, stockBar);
            shelf.EditorSetBadge(LevelBadge(root.transform, bounds.max.y + 0.55f));
            return root;
        }

        private static GameObject BuildCheckout(Material coin)
        {
            var root = new GameObject("checkout");
            var modelInstance = AddModel(root.transform, Model("mini-market/cash-register"));
            var bounds = LocalBounds(modelInstance);
            AddClickAndObstacle(root, bounds);

            var counter = Point(root.transform, "CounterPoint", new Vector3(0f, 0f, bounds.max.z + 0.35f), 180f);
            var cashier = Point(root.transform, "CashierPoint", new Vector3(0f, 0f, bounds.min.z - 0.3f), 0f);

            var marker = BuildText(root.transform, "NeedsServiceMarker", "!", 6f, new Color(1f, 0.85f, 0.2f));
            marker.transform.localPosition = new Vector3(0f, bounds.max.y + 0.45f, 0f);
            marker.gameObject.AddComponent<Billboard>().EditorSetup(0.06f);

            var checkout = root.AddComponent<Checkout>();
            checkout.EditorSetup(counter, cashier, marker.gameObject);
            checkout.EditorSetBadge(LevelBadge(root.transform, bounds.max.y + 0.2f));
            return root;
        }

        private static TextMeshPro LevelBadge(Transform parent, float height)
        {
            var badge = BuildText(parent, "LevelBadge", "Lv 2", 2f, new Color(0.55f, 1f, 0.6f));
            badge.transform.localPosition = new Vector3(0f, height, 0f);
            badge.gameObject.AddComponent<Billboard>();
            badge.gameObject.SetActive(false);
            return badge;
        }

        private static GameObject BuildCharacterBase(string name, GameObject model, AnimatorController controller)
        {
            var root = new GameObject(name);
            var modelInstance = AddModel(root.transform, model);
            var animator = modelInstance.GetComponent<Animator>();
            if (animator == null) animator = modelInstance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            root.AddComponent<CharacterAnimator>();
            return root;
        }

        private static void AddAgent(GameObject root, float speed)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.angularSpeed = 720f;
            agent.acceleration = 16f;
            agent.radius = 0.18f;
            agent.height = 0.7f;
            agent.stoppingDistance = 0.05f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            root.AddComponent<CharacterMotor>();
        }

        private static GameObject BuildCustomer(GameObject model, AnimatorController controller)
        {
            var root = BuildCharacterBase("Customer", model, controller);
            AddAgent(root, 1.3f);

            var basket = (GameObject)PrefabUtility.InstantiatePrefab(Model("mini-market/shopping-basket"), root.transform);
            basket.name = "Basket";
            basket.transform.localPosition = new Vector3(0.2f, 0.12f, 0.08f);
            basket.transform.localScale = Vector3.one * 0.7f;

            root.AddComponent<CustomerAI>().EditorSetup(basket);
            return root;
        }

        private static GameObject BuildStocker(AnimatorController controller)
        {
            var root = BuildCharacterBase("Stocker", Model("mini-market/character-employee"), controller);
            AddAgent(root, 1.6f);

            var box = (GameObject)PrefabUtility.InstantiatePrefab(Model("furniture-kit/cardboardBoxClosed"), root.transform);
            box.name = "CarriedBox";
            box.transform.localPosition = new Vector3(0f, 0.3f, 0.18f);
            box.transform.localScale = Vector3.one * 0.9f;

            root.AddComponent<StockerAI>().EditorSetup(box);
            return root;
        }

        private static GameObject BuildCashier(AnimatorController controller) =>
            BuildCharacterBase("Cashier", Model("mini-market/character-employee"), controller);

        public static TextMeshPro BuildText(Transform parent, string name, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            tmp.font = UiFont();
            tmp.fontSharedMaterial = OutlinedFontMaterial();
            return tmp;
        }

        public static Material OutlinedDefaultFontMaterial()
        {
            var path = $"{Materials}/Font_Outlined.mat";
            var material = Load<Material>(path);
            if (material != null) return material;

            material = new Material(TMP_Settings.defaultFontAsset.material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.25f);
            material.SetColor("_OutlineColor", new Color32(30, 30, 40, 255));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Shared font material with a dark outline (setting outline per text would create leaking instances).</summary>
        public static Material OutlinedFontMaterial()
        {
            var path = $"{Materials}/Font_Outlined_Kenney.mat";
            var material = Load<Material>(path);
            if (material != null) return material;

            material = new Material(UiFont().material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.25f);
            material.SetColor("_OutlineColor", new Color32(30, 30, 40, 255));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject BuildLabel(string name, float size, Color color)
        {
            var holder = new GameObject("Holder");
            var label = BuildText(holder.transform, name, "Text", size, color);
            // Money texts ("+$10") read better in the plain font: the display font's "$" looks like an "S".
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSharedMaterial = OutlinedDefaultFontMaterial();
            label.transform.SetParent(null);
            Object.DestroyImmediate(holder);
            return label.gameObject;
        }

        private static GameObject BuildPuff()
        {
            var go = new GameObject("Puff");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 2.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f), Color.white);
            main.gravityModifier = 0.4f;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial();
            return go;
        }

        // ---------- Configs ----------

        private static T GetOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = Load<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ProductConfig Product(string id, string name, Color color, long price, int xp) =>
            GetOrCreate<ProductConfig>($"{Configs}/Products/{id}.asset", p =>
            {
                p.id = id;
                p.displayName = name;
                p.color = color;
                p.basePrice = price;
                p.xpPerSale = xp;
            });

        private static BuildableConfig Shelf(string id, string name, ProductConfig product, int unlock, long cost, int capacity)
        {
            var config = GetOrCreate<BuildableConfig>($"{Configs}/Buildables/{id}.asset", b =>
            {
                b.id = id;
                b.displayName = name;
                b.kind = BuildableKind.Shelf;
                b.product = product;
                b.unlockLevel = unlock;
                b.buildCost = cost;
                b.baseCapacity = capacity;
                b.capacityPerLevel = Mathf.Max(1, capacity / 3);
            });
            config.prefab = Load<GameObject>($"{Prefabs}/Buildables/{id}.prefab");
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void BuildConfigs()
        {
            var fruits = Product("fruits", "Fruits", new Color(1f, 0.55f, 0.2f), 6, 1);
            var bread = Product("bread", "Bread", new Color(0.85f, 0.65f, 0.35f), 9, 1);
            var groceries = Product("groceries", "Groceries", new Color(0.4f, 0.7f, 1f), 14, 2);
            var snacks = Product("snacks", "Snacks", new Color(1f, 0.8f, 0.2f), 18, 2);
            var drinks = Product("drinks", "Drinks", new Color(0.3f, 0.85f, 0.85f), 25, 3);
            var frozen = Product("frozen", "Frozen food", new Color(0.7f, 0.85f, 1f), 35, 4);

            var buildables = new List<BuildableConfig>
            {
                Shelf("shelf_fruits", "Fruit stand", fruits, 1, 40, 5),
                Shelf("shelf_bread", "Bread stand", bread, 2, 120, 6),
                Shelf("shelf_groceries", "Grocery shelf", groceries, 3, 300, 8),
                Shelf("shelf_snacks", "Snack shelf", snacks, 4, 650, 8),
                Shelf("shelf_drinks", "Drinks fridge", drinks, 5, 1200, 10),
                Shelf("shelf_frozen", "Freezer", frozen, 6, 2200, 10),
            };

            var checkout = GetOrCreate<BuildableConfig>($"{Configs}/Buildables/checkout.asset", b =>
            {
                b.id = "checkout";
                b.displayName = "Checkout";
                b.kind = BuildableKind.Checkout;
                b.unlockLevel = 1;
                b.buildCost = 60;
                b.upgradeCostGrowth = 1.7f;
                b.cashierHireCost = 150;
            });
            checkout.prefab = Load<GameObject>($"{Prefabs}/Buildables/checkout.prefab");
            EditorUtility.SetDirty(checkout);
            buildables.Add(checkout);

            var parking = GetOrCreate<ExpansionConfig>($"{Configs}/Expansions/parking.asset", e =>
            {
                e.id = "parking";
                e.displayName = "Parking lot";
                e.description = "More shoppers can come by car.";
                e.cost = 400;
                e.requiredLevel = 2;
                e.extraCustomersPerMinute = 6f;
            });
            var hall2 = GetOrCreate<ExpansionConfig>($"{Configs}/Expansions/hall2.asset", e =>
            {
                e.id = "hall2";
                e.displayName = "Second hall";
                e.description = "Room for six more shelves and a checkout.";
                e.cost = 1500;
                e.requiredLevel = 4;
                e.extraCustomersPerMinute = 5f;
            });

            var game = GetOrCreate<GameConfig>($"{Configs}/GameConfig.asset", _ => { });
            game.products = new List<ProductConfig> { fruits, bread, groceries, snacks, drinks, frozen };
            game.buildables = buildables;
            game.expansions = new List<ExpansionConfig> { parking, hall2 };
            EditorUtility.SetDirty(game);
        }
    }
}
