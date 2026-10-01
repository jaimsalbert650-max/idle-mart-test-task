using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Batch-mode helpers for headless iteration:
    /// renders scenes (including the UI) to PNG and logs model sizes.
    /// Usage: Unity -batchmode -quit -executeMethod IdleMart.EditorTools.DevCapture.CaptureAll
    /// </summary>
    public static class DevCapture
    {
        private const string Output = "Logs/Captures";

        public static void CaptureAll()
        {
            Directory.CreateDirectory(Output);
            Capture(SceneBuilder.ScenePath, "game.png");
            Capture(ContentBuilder.Root + "/Scenes/MainMenu.unity", "menu.png");
            Capture(SceneBuilder.ScenePath, "street.png");
        }

        public static void BuildAndCapture()
        {
            UiBuilder.BuildEverything();
            CaptureAll();
        }

        private static void Capture(string scenePath, string fileName)
        {
            EditorSceneManager.OpenScene(scenePath);
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            // Overlay canvases are not rendered by cameras: switch them to camera space for the shot.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.1f;
            }

            if (scenePath == SceneBuilder.ScenePath) StageDemo();
            if (fileName == "street.png") cam.transform.position += new Vector3(0f, 6f, -14f);

            // Panels start hidden at runtime; hide them for the shot too.
            foreach (var name in new[] { "SettingsPanel", "PauseMenu", "OfflinePopup", "ContextPanel", "StaffPanel", "Toast", "ConfirmNewGame" })
            foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r => r.name == name))
                t.gameObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // Second pass so layout/text meshes are settled.

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            File.WriteAllBytes(Path.Combine(Output, fileName), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;

            // Do not keep the temporary canvas changes.
            EditorSceneManager.OpenScene(scenePath);
        }

        public const string MenuBackgroundPath = ContentBuilder.UiArt + "/menu_background.png";

        /// <summary>Renders the staged store (no UI) into a texture used as the main menu background.</summary>
        public static void RenderMenuBackground()
        {
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            StageDemo();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);

            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            File.WriteAllBytes(MenuBackgroundPath, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;

            EditorSceneManager.OpenScene(SceneBuilder.ScenePath); // Discard staging.
            AssetDatabase.ImportAsset(MenuBackgroundPath);
            ContentBuilder.UiSprite("menu_background");
        }

        /// <summary>Visual-only: places a few objects and customers so the shot looks like a running store.</summary>
        private static void StageDemo()
        {
            var store = Object.FindFirstObjectByType<World.Store>();
            string[] shelves = { "shelf_fruits", "shelf_bread", "shelf_groceries", "shelf_snacks", "shelf_fruits", "shelf_drinks" };
            var i = 0;
            foreach (var slot in store.Slots)
            {
                var id = slot.Kind == Configs.BuildableKind.Checkout ? "checkout" : i < shelves.Length ? shelves[i++] : null;
                if (id == null || slot.SlotId.StartsWith("h2")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ContentBuilder.Prefabs}/Buildables/{id}.prefab");
                Object.Instantiate(prefab, slot.transform.position, slot.transform.rotation);
                slot.gameObject.SetActive(false);
            }

            var customers = Directory.GetFiles($"{ContentBuilder.Prefabs}/Characters", "Customer_*.prefab");
            var random = new System.Random(7);
            foreach (var position in new[] { new Vector3(3.5f, 0, 4.6f), new Vector3(5.1f, 0, 2.9f), new Vector3(6.5f, 0, 6.6f), new Vector3(1.5f, 0, 2.1f), new Vector3(1.5f, 0, 2.7f), new Vector3(5f, 0, -1.5f), new Vector3(8.8f, 0, 2.1f) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(customers[random.Next(customers.Length)].Replace('\\', '/'));
                Object.Instantiate(prefab, position, Quaternion.Euler(0, random.Next(360), 0));
            }
        }

        public static void LogModelSizes()
        {
            var sb = new StringBuilder();
            foreach (var pack in new[] { "city-commercial", "city-roads", "car-kit" })
            foreach (var file in Directory.GetFiles($"{ContentBuilder.Art}/{pack}", "*.fbx"))
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/')));
                var renderers = go.GetComponentsInChildren<Renderer>();
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                sb.AppendLine($"{pack}/{Path.GetFileNameWithoutExtension(file)} size={b.size:F2} center={b.center:F2}");
                Object.DestroyImmediate(go);
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/model-sizes.txt", sb.ToString());
        }
    }
}
