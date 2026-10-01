using System.Collections.Generic;
using System.Linq;
using IdleMart.AI;
using IdleMart.Configs;
using IdleMart.Core;
using IdleMart.World;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Builds the Game scene procedurally from Kenney tiles: halls, walls, storage, build slots,
    /// expansion zones, NavMesh, light, camera and the game systems.
    /// Layout (metres, +Z is "back" of the store):
    ///   Hall 1   x 0..10,  z 0..8  (entrance door in the front wall at x 4..6)
    ///   Hall 2   x 10..16, z 0..8  (expansion, separated by a removable wall at x = 10)
    ///   Parking  x 6..14,  z -6..-1 (expansion, outside)
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = ContentBuilder.Root + "/Scenes/Game.unity";

        private const int Hall1Width = 10;
        private const int Hall2Width = 6;
        private const int Depth = 8;

        private static Transform _world;

        [MenuItem("Idle Mart/Build Game Scene", priority = 2)]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(ContentBuilder.Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var config = ContentBuilder.Load<GameConfig>($"{ContentBuilder.Configs}/GameConfig.asset");
            _world = new GameObject("World").transform;

            BuildEnvironment();
            var slots = new List<BuildSlot>();
            var storage = BuildHall1(slots);
            var zones = new List<ExpansionZone>
            {
                BuildHall2(config.FindExpansion("hall2"), slots),
                BuildParking(config.FindExpansion("parking"))
            };

            EnvironmentBuilder.Dress(_world, zones[1].transform.Find("Parking"));

            var entrance = Point(_world, "Entrance", new Vector3(5f, 0f, -3.5f));
            var exit = Point(_world, "Exit", new Vector3(3f, 0f, -4.5f));

            var store = new GameObject("Store").AddComponent<Store>();
            store.EditorSetup(entrance, exit, storage, slots, zones,
                ContentBuilder.Load<GameObject>($"{ContentBuilder.Prefabs}/Characters/Cashier.prefab"));

            BakeNavMesh(zones);
            var (cam, controller) = BuildCameraAndLight();
            EnvironmentBuilder.SetupLook(cam);
            BuildSystems(config, store, cam, controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"Idle Mart: scene saved to {ScenePath}");
        }

        // ---------- Helpers ----------

        private static GameObject Place(string model, Transform parent, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(ContentBuilder.Model(model), parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static Transform Point(Transform parent, string name, Vector3 position, float yaw = 0f)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return point;
        }

        private static void Floor(Transform parent, int x0, int x1, int z0, int z1)
        {
            for (var x = x0; x < x1; x++)
            for (var z = z0; z < z1; z++)
                Place("mini-market/floor", parent, new Vector3(x + 0.5f, 0f, z + 0.5f));
        }

        /// <summary>A straight wall along X (facing ±Z) or Z (facing ±X) from a to b, skipping door cells.</summary>
        private static List<GameObject> WallLine(Transform parent, Vector3 from, int length, bool alongX, float yaw, ICollection<int> gaps = null, ICollection<int> windows = null)
        {
            var pieces = new List<GameObject>();
            for (var i = 0; i < length; i++)
            {
                if (gaps != null && gaps.Contains(i)) continue;
                var offset = alongX ? new Vector3(i + 0.5f, 0f, 0f) : new Vector3(0f, 0f, i + 0.5f);
                var model = windows != null && windows.Contains(i) ? "mini-market/wall-window" : "mini-market/wall";
                pieces.Add(Place(model, parent, from + offset, yaw));
            }

            return pieces;
        }

        private static BuildSlot Slot(Transform parent, string id, BuildableKind kind, Vector3 position, float yaw)
        {
            var go = new GameObject($"Slot_{id}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.05f, 0f);
            collider.size = new Vector3(0.9f, 0.1f, 0.9f);

            var marker = new GameObject("EmptyMarker");
            marker.transform.SetParent(go.transform, false);
            var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(tile.GetComponent<Collider>());
            tile.transform.SetParent(marker.transform, false);
            tile.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tile.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            var color = kind == BuildableKind.Checkout ? new Color(1f, 0.8f, 0.3f) : new Color(0.45f, 0.85f, 0.5f);
            tile.GetComponent<Renderer>().sharedMaterial = ContentBuilder.ColorMaterial(kind == BuildableKind.Checkout ? "SlotCheckout" : "SlotShelf", color);

            var plus = ContentBuilder.BuildText(marker.transform, "Plus", "+", 6f, Color.white);
            plus.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            plus.gameObject.AddComponent<Billboard>().EditorSetup(0.05f);

            IgnoreForNavMesh(marker);

            var slot = go.AddComponent<BuildSlot>();
            slot.EditorSetup(id, kind, marker);
            return slot;
        }

        private static GameObject SaleSign(Transform parent, Vector3 position, ExpansionConfig config)
        {
            var sign = new GameObject("SaleSign");
            sign.transform.SetParent(parent, false);
            sign.transform.position = position;
            Place("mini-market/column", sign.transform, position, 0f, 0.6f);

            var label = ContentBuilder.BuildText(sign.transform, "Label", $"FOR SALE\n<size=70%>{config.displayName}</size>", 4f, new Color(1f, 0.85f, 0.3f));
            label.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            label.rectTransform.sizeDelta = new Vector2(4f, 2f);
            label.gameObject.AddComponent<Billboard>().EditorSetup(0.05f);
            IgnoreForNavMesh(label.gameObject);

            var collider = sign.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.8f, 0f);
            collider.size = new Vector3(1.4f, 1.6f, 1.4f);
            return sign;
        }

        /// <summary>Labels and markers must not affect walkable area.</summary>
        private static void IgnoreForNavMesh(GameObject go)
        {
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
        }

        // ---------- Areas ----------

        private static void BuildEnvironment()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_world, false);
            ground.transform.position = new Vector3(8f, -0.02f, 1f);
            ground.transform.localScale = new Vector3(5f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = ContentBuilder.ColorMaterial("Ground", new Color(0.55f, 0.75f, 0.45f), unlit: false);

            // Sidewalk in front of the entrance.
            var walk = Group(_world, "Sidewalk");
            Floor(walk, 2, 7, -5, 0);
        }

        private static Transform BuildHall1(List<BuildSlot> slots)
        {
            var hall = Group(_world, "Hall1");
            Floor(Group(hall, "Floor"), 0, Hall1Width, 0, Depth);

            var walls = Group(hall, "Walls");
            WallLine(walls, new Vector3(0f, 0f, Depth), Hall1Width, true, 180f);
            WallLine(walls, new Vector3(0f, 0f, 0f), Hall1Width, true, 0f, gaps: new[] { 4, 5 }, windows: new[] { 1, 2, 7, 8 });
            WallLine(walls, new Vector3(0f, 0f, 0f), Depth, false, 90f);

            // Storage corner: boxes the stockers take goods from.
            var storageGroup = Group(hall, "Storage");
            Place("furniture-kit/rugDoormat", storageGroup, new Vector3(1.2f, 0.01f, 7f), 0f, 1.6f);
            Place("furniture-kit/cardboardBoxClosed", storageGroup, new Vector3(0.5f, 0f, 7.5f));
            Place("furniture-kit/cardboardBoxClosed", storageGroup, new Vector3(0.9f, 0f, 7.5f));
            Place("furniture-kit/cardboardBoxOpen", storageGroup, new Vector3(0.5f, 0f, 7.1f));
            Place("furniture-kit/cardboardBoxClosed", storageGroup, new Vector3(0.7f, 0.3f, 7.5f));
            var storagePoint = Point(storageGroup, "StoragePoint", new Vector3(1.4f, 0f, 6.7f));

            var slotGroup = Group(hall, "Slots");
            var index = 1;
            foreach (var z in new[] { 3.8f, 5.8f })
            foreach (var x in new[] { 3.5f, 5f, 6.5f, 8f })
                slots.Add(Slot(slotGroup, $"h1_shelf_{index++:00}", BuildableKind.Shelf, new Vector3(x, 0f, z), 0f));

            slots.Add(Slot(slotGroup, "h1_checkout_01", BuildableKind.Checkout, new Vector3(1.5f, 0f, 1.3f), 0f));
            slots.Add(Slot(slotGroup, "h1_checkout_02", BuildableKind.Checkout, new Vector3(8.8f, 0f, 1.3f), 0f));

            var decor = Group(hall, "Decor");
            Place("furniture-kit/pottedPlant", decor, new Vector3(3.4f, 0f, 0.5f));
            Place("furniture-kit/pottedPlant", decor, new Vector3(6.6f, 0f, 0.5f));
            Place("mini-market/shopping-cart", decor, new Vector3(0.5f, 0f, 3f), 90f);
            Place("mini-market/shopping-cart", decor, new Vector3(0.5f, 0f, 3.6f), 90f);
            return storagePoint;
        }

        private static ExpansionZone BuildHall2(ExpansionConfig config, List<BuildSlot> allSlots)
        {
            var zoneGo = new GameObject("Zone_Hall2");
            zoneGo.transform.SetParent(_world, false);
            var zone = zoneGo.AddComponent<ExpansionZone>();
            var x0 = Hall1Width;

            Floor(Group(zoneGo.transform, "Floor"), x0, x0 + Hall2Width, 0, Depth);
            var walls = Group(zoneGo.transform, "Walls");
            WallLine(walls, new Vector3(x0, 0f, Depth), Hall2Width, true, 180f);
            WallLine(walls, new Vector3(x0, 0f, 0f), Hall2Width, true, 0f, windows: new[] { 1, 2, 4 });
            WallLine(walls, new Vector3(x0 + Hall2Width, 0f, 0f), Depth, false, -90f);

            // The divider between halls is the blocker that disappears on purchase.
            var divider = Group(zoneGo.transform, "Divider");
            WallLine(divider, new Vector3(x0, 0f, 0f), Depth, false, -90f);
            var dividerObstacle = divider.gameObject.AddComponent<NavMeshObstacle>();
            dividerObstacle.shape = NavMeshObstacleShape.Box;
            dividerObstacle.center = new Vector3(x0, 0.5f, Depth * 0.5f);
            dividerObstacle.size = new Vector3(0.4f, 1f, Depth);
            dividerObstacle.carving = true;

            var slotGroup = Group(zoneGo.transform, "Slots");
            var slots = new List<BuildSlot>();
            var index = 1;
            foreach (var z in new[] { 4.3f, 6.3f })
            foreach (var x in new[] { 11.5f, 13f, 14.5f })
                slots.Add(Slot(slotGroup, $"h2_shelf_{index++:00}", BuildableKind.Shelf, new Vector3(x, 0f, z), 0f));
            slots.Add(Slot(slotGroup, "h2_checkout_01", BuildableKind.Checkout, new Vector3(13f, 0f, 1.3f), 0f));
            allSlots.AddRange(slots);

            var sign = SaleSign(zoneGo.transform, new Vector3(13f, 0f, 3f), config);
            zone.EditorSetup(config, new List<GameObject> { divider.gameObject }, new List<GameObject>(), slots, sign);
            return zone;
        }

        private static ExpansionZone BuildParking(ExpansionConfig config)
        {
            var zoneGo = new GameObject("Zone_Parking");
            zoneGo.transform.SetParent(_world, false);
            var zone = zoneGo.AddComponent<ExpansionZone>();

            var revealed = Group(zoneGo.transform, "Parking");
            Floor(revealed, 7, 14, -6, -1);
            for (var x = 7; x < 14; x++)
            {
                Place("mini-market/fence", revealed, new Vector3(x + 0.5f, 0f, -6f), 0f);
            }

            Place("furniture-kit/bench", revealed, new Vector3(8f, 0f, -1.6f), 180f);
            Place("furniture-kit/pottedPlant", revealed, new Vector3(7.4f, 0f, -1.5f));
            Place("furniture-kit/pottedPlant", revealed, new Vector3(13.6f, 0f, -1.5f));
            Place("mini-market/shopping-cart", revealed, new Vector3(12f, 0f, -2f), 30f);
            Place("mini-market/shopping-basket", revealed, new Vector3(10f, 0f, -3f));

            var sign = SaleSign(zoneGo.transform, new Vector3(10.5f, 0f, -3.5f), config);
            zone.EditorSetup(config, new List<GameObject>(), new List<GameObject> { revealed.gameObject }, new List<BuildSlot>(), sign);
            return zone;
        }

        // ---------- NavMesh ----------

        private static void BakeNavMesh(List<ExpansionZone> zones)
        {
            SetAgentRadius(0.18f);

            // Bake with blockers removed so their area is walkable once bought; at runtime they carve instead.
            var blockers = zones.SelectMany(z => z.GetComponentsInChildren<NavMeshObstacle>(true)).Select(o => o.gameObject).ToList();
            foreach (var b in blockers) b.SetActive(false);

            var surface = _world.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            var data = surface.navMeshData;
            var path = System.IO.Path.ChangeExtension(ScenePath, null) + "_NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);

            foreach (var b in blockers) b.SetActive(true);
        }

        private static void SetAgentRadius(float radius)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            settings.FindProperty("m_Settings.Array.data[0].agentRadius").floatValue = radius;
            settings.FindProperty("m_Settings.Array.data[0].agentHeight").floatValue = 0.7f;
            settings.FindProperty("m_Settings.Array.data[0].agentClimb").floatValue = 0.1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Camera, light, systems ----------

        private static (Camera, CameraController) BuildCameraAndLight()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.7f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.9f);
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();

            var controller = camGo.AddComponent<CameraController>();
            controller.EditorSetup(cam, new Vector2(1f, -4f), new Vector2(15f, 7f));

            // Same framing the controller applies at runtime, so the scene looks right in the editor too.
            var rotation = Quaternion.Euler(CameraController.DefaultPitch, CameraController.DefaultYaw, 0f);
            camGo.transform.SetPositionAndRotation(new Vector3(5.5f, 0f, 3f) - rotation * Vector3.forward * 13f, rotation);
            return (cam, controller);
        }

        private static void BuildSystems(GameConfig config, Store store, Camera cam, CameraController controller)
        {
            var systems = new GameObject("Systems");

            var fx = new GameObject("WorldFx").AddComponent<WorldFx>();
            fx.transform.SetParent(systems.transform);
            fx.EditorSetup(
                ContentBuilder.Load<GameObject>($"{ContentBuilder.Prefabs}/Fx/FloatingText.prefab").GetComponent<TextMeshPro>(),
                ContentBuilder.Load<GameObject>($"{ContentBuilder.Prefabs}/Fx/Bubble.prefab").GetComponent<TextMeshPro>(),
                ContentBuilder.Load<GameObject>($"{ContentBuilder.Prefabs}/Fx/Puff.prefab").GetComponent<ParticleSystem>());

            var spawner = new GameObject("Customers").AddComponent<CustomerSpawner>();
            spawner.transform.SetParent(systems.transform);
            spawner.EditorSetup(AssetDatabase.FindAssets("Customer_ t:Prefab", new[] { $"{ContentBuilder.Prefabs}/Characters" })
                .Select(g => ContentBuilder.Load<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<CustomerAI>())
                .ToList());

            var staff = new GameObject("Staff").AddComponent<StaffService>();
            staff.transform.SetParent(systems.transform);
            staff.EditorSetup(ContentBuilder.Load<GameObject>($"{ContentBuilder.Prefabs}/Characters/Stocker.prefab").GetComponent<StockerAI>());

            var input = new GameObject("ClickInput").AddComponent<ClickInput>();
            input.transform.SetParent(systems.transform);
            input.EditorSetup(cam, controller);

            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            bootstrap.transform.SetParent(systems.transform);
            bootstrap.EditorSetup(config, store, spawner, staff, input);
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path && s.path != "Assets/Scenes/SampleScene.unity").ToList();
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
