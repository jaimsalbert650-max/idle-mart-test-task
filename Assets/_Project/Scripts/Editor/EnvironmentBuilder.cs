using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Dresses the Game scene: road with traffic, parked cars, trees, bushes, flowers, the store sign,
    /// post-processing and nicer ambient light. Purely visual; uses a fixed random seed so rebuilds are identical.
    /// </summary>
    public static class EnvironmentBuilder
    {
        private const float CarScale = 0.5f;

        private static readonly string[] Trees = { "tree_default", "tree_oak", "tree_detailed", "tree_fat", "tree_blocks" };
        private static readonly string[] Flowers = { "flower_redA", "flower_yellowA", "flower_purpleA" };
        private static readonly string[] Cars = { "sedan", "suv", "hatchback-sports", "van", "taxi" };

        private static System.Random _random;

        public static void Dress(Transform world, Transform parking)
        {
            _random = new System.Random(42);
            var env = new GameObject("Environment").transform;
            env.SetParent(world, false);

            BuildRoad(env);
            BuildTrees(env);
            BuildGreenery(env);
            BuildParkedCars(parking);
            BuildSign(env);
        }

        private static float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private static GameObject Place(string model, Transform parent, Vector3 position, float yaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(ContentBuilder.Model(model), parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static GameObject Slab(Transform parent, string name, Material material, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void BuildRoad(Transform env)
        {
            var road = new GameObject("Road").transform;
            road.SetParent(env, false);
            var asphalt = ContentBuilder.ColorMaterial("Asphalt", new Color(0.32f, 0.33f, 0.37f), unlit: false);
            var stripe = ContentBuilder.ColorMaterial("RoadStripe", new Color(0.95f, 0.92f, 0.75f), unlit: false);
            var curb = ContentBuilder.ColorMaterial("Curb", new Color(0.78f, 0.78f, 0.8f), unlit: false);

            Slab(road, "Asphalt", asphalt, new Vector3(8f, -0.005f, -8f), new Vector3(40f, 0.02f, 2.6f));
            Slab(road, "CurbNear", curb, new Vector3(8f, 0.01f, -6.65f), new Vector3(40f, 0.06f, 0.12f));
            Slab(road, "CurbFar", curb, new Vector3(8f, 0.01f, -9.35f), new Vector3(40f, 0.06f, 0.12f));
            for (var x = -11f; x < 28f; x += 1.6f)
                Slab(road, "Stripe", stripe, new Vector3(x, 0.01f, -8f), new Vector3(0.8f, 0.02f, 0.08f));

            // A couple of cars passing by (static decoration).
            Place("car-kit/taxi", road, new Vector3(-2f, 0f, -8.6f), 90f, CarScale);
            Place("car-kit/delivery", road, new Vector3(18.5f, 0f, -7.4f), -90f, CarScale);
            Place("car-kit/cone", road, new Vector3(15f, 0f, -6.4f), 0f, 0.5f);
        }

        private static void BuildTrees(Transform env)
        {
            var trees = new GameObject("Trees").transform;
            trees.SetParent(env, false);

            void Tree(Vector3 position) =>
                Place("nature-kit/" + Trees[_random.Next(Trees.Length)], trees, position, Range(0f, 360f), Range(1.5f, 2.1f));

            for (var x = -1.5f; x <= 17.5f; x += Range(1.3f, 1.9f)) Tree(new Vector3(x, 0f, Range(9.2f, 10.2f)));
            for (var z = -0.5f; z <= 8.5f; z += Range(1.3f, 1.9f))
            {
                Tree(new Vector3(Range(-1.9f, -1.2f), 0f, z));
                Tree(new Vector3(Range(17.2f, 17.9f), 0f, z));
            }

            // Corners near the road.
            Tree(new Vector3(-1.5f, 0f, -4.5f));
            Tree(new Vector3(0.5f, 0f, -5.5f));
            Tree(new Vector3(16.5f, 0f, -4f));
        }

        private static void BuildGreenery(Transform env)
        {
            var green = new GameObject("Greenery").transform;
            green.SetParent(env, false);

            // Bushes along the front wall, leaving the door and sidewalk free.
            foreach (var x in new[] { 0.4f, 1.3f, 7.6f, 8.5f, 9.4f, 10.6f, 11.5f, 12.4f, 13.3f, 14.2f, 15.1f })
            {
                Place("nature-kit/plant_bushDetailed", green, new Vector3(x, 0f, -0.55f), Range(0f, 360f), Range(1.1f, 1.4f));
                if (_random.NextDouble() < 0.6)
                    Place("nature-kit/" + Flowers[_random.Next(Flowers.Length)], green, new Vector3(x + 0.4f, 0f, -0.9f), Range(0f, 360f), 1.8f);
            }

            // Scattered grass and flowers on the lawn.
            for (var i = 0; i < 40; i++)
            {
                var position = new Vector3(Range(-2f, 18f), 0f, Range(8.6f, 9.4f));
                if (i % 2 == 0) position = new Vector3(Range(-2f, 0f), 0f, Range(-5f, 9f));
                var model = _random.NextDouble() < 0.5 ? "grass_large" : Flowers[_random.Next(Flowers.Length)];
                Place("nature-kit/" + model, green, position, Range(0f, 360f), Range(1.5f, 2.2f));
            }
        }

        private static void BuildParkedCars(Transform parking)
        {
            var x = 7.9f;
            foreach (var car in Cars)
            {
                Place("car-kit/" + car, parking, new Vector3(x, 0f, -4.6f), Range(-6f, 6f), CarScale);
                x += 1.35f;
            }

            var lines = ContentBuilder.ColorMaterial("ParkingLine", new Color(0.95f, 0.95f, 0.95f), unlit: false);
            for (var lx = 7.25f; lx <= 14f; lx += 1.35f)
                Slab(parking, "ParkingLine", lines, new Vector3(lx, 0.035f, -4.6f), new Vector3(0.06f, 0.01f, 1.6f));
        }

        private static void BuildSign(Transform env)
        {
            var sign = new GameObject("StoreSign").transform;
            sign.SetParent(env, false);
            // Stands on top of the front wall, tilted towards the camera so it reads from the tycoon view.
            sign.position = new Vector3(5f, 1.05f, -0.1f);
            sign.rotation = Quaternion.Euler(35f, 0f, 0f);
            var board = ContentBuilder.ColorMaterial("SignBoard", new Color(0.18f, 0.55f, 0.36f), unlit: false);
            var slab = Slab(sign, "Board", board, Vector3.zero, new Vector3(3.5f, 0.55f, 0.08f));
            slab.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            slab.transform.localRotation = Quaternion.identity;

            var text = ContentBuilder.BuildText(sign, "Title", "IDLE MART", 4.5f, new Color(1f, 0.88f, 0.3f));
            text.transform.localPosition = new Vector3(0f, 0.3f, -0.05f);
            text.transform.localRotation = Quaternion.identity;
            text.rectTransform.sizeDelta = new Vector2(2.8f, 0.55f);
        }

        /// <summary>Global post-processing and softer, sky-tinted ambient light.</summary>
        public static void SetupLook(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var path = $"{ContentBuilder.Root}/Art/Materials/GameLook.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(18f);
            color.contrast.Override(8f);
            color.postExposure.Override(0.15f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.64f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.42f, 0.38f);
        }
    }
}
