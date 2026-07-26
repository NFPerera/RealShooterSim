using RealShooter.Ballistics;
using RealShooter.Ballistics.Visuals;
using RealShooter.Player;
using RealShooter.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealShooter.EditorTools
{
    /// Herramienta de editor para armar de un click una escena de prueba de balistica:
    /// terreno, blancos a distintas distancias, jugador con camara/disparo y los managers
    /// de fisica/clima/visuales ya conectados entre si. Pensada solo para iterar sobre el
    /// "feeling" del disparo, no para produccion.
    public static class TestSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/BallisticsTestScene.unity";
        private const string DataFolder = "Assets/ShooterScripts/Data/TestPresets";

        [MenuItem("RealShooter/Crear Escena de Prueba de Balistica")]
        public static void CreateTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            int groundLayer = GetOrCreateLayer("Ground");
            int targetLayer = GetOrCreateLayer("Targets");

            CreateLight();
            CreateGround(groundLayer);
            CreateTargets(targetLayer);

            PlayerShooterController controller = CreatePlayer(out Transform cameraTransform);
            PhysicsManager physicsManager = CreateManagers(groundLayer, targetLayer, controller, out WeatherManager weatherManager);
            CreateHud(physicsManager, weatherManager, controller.transform);

            EnsureDataFolder();
            BulletData bullet = CreateOrLoadBullet();
            WeaponData weapon = CreateOrLoadWeapon();
            AssignPlayerData(controller, bullet, weapon, cameraTransform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = controller.gameObject;
            Debug.Log("[TestSceneSetup] Escena creada en " + ScenePath + ". Dale Play: click izquierdo dispara, Escape libera el cursor.");
        }

        private static void CreateLight()
        {
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround(int groundLayer)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100f, 1f, 100f); // Plane de 10x10 -> 1000x1000 m
            ground.layer = groundLayer;

            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = CreateColoredMaterial(new Color(0.3f, 0.35f, 0.25f));
        }

        private static void CreateTargets(int targetLayer)
        {
            float[] distances = { 100f, 300f, 600f, 900f };
            Color[] colors = { Color.green, Color.yellow, new Color(1f, 0.5f, 0f), Color.red };

            for (int i = 0; i < distances.Length; i++)
            {
                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = $"Target_{distances[i]:0}m";
                target.transform.position = new Vector3(0f, 1f, distances[i]);
                target.transform.localScale = new Vector3(0.5f, 1.8f, 0.3f);
                target.layer = targetLayer;

                Renderer renderer = target.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = CreateColoredMaterial(colors[i]);
            }
        }

        private static PlayerShooterController CreatePlayer(out Transform cameraTransform)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, -5f); // pies a nivel del piso; el CharacterController (centro en y=1, alto=2) ya queda apoyado en y=0
            player.transform.rotation = Quaternion.identity;

            PlayerShooterController controller = player.AddComponent<PlayerShooterController>();

            GameObject cameraGo = new GameObject("PlayerCamera");
            cameraGo.transform.SetParent(player.transform);
            cameraGo.transform.localPosition = new Vector3(0f, 1.6f, 0f); // altura de ojos
            cameraGo.transform.localRotation = Quaternion.identity;
            cameraGo.tag = "MainCamera";

            Camera cam = cameraGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();

            cameraTransform = cameraGo.transform;
            return controller;
        }

        private static PhysicsManager CreateManagers(int groundLayer, int targetLayer, PlayerShooterController controller, out WeatherManager weatherManager)
        {
            GameObject weatherGo = new GameObject("WeatherManager");
            weatherManager = weatherGo.AddComponent<WeatherManager>();

            GameObject physicsGo = new GameObject("PhysicsManager");
            PhysicsManager physicsManager = physicsGo.AddComponent<PhysicsManager>();

            int whitelist = (1 << groundLayer) | (1 << targetLayer);

            SerializedObject physicsSO = new SerializedObject(physicsManager);
            physicsSO.FindProperty("weatherManager").objectReferenceValue = weatherManager;
            physicsSO.FindProperty("impactLayerMask").intValue = whitelist;
            physicsSO.ApplyModifiedProperties();

            GameObject visualsGo = new GameObject("ProjectileVisualManager");
            ProjectileVisualManager visualManager = visualsGo.AddComponent<ProjectileVisualManager>();

            SerializedObject visualsSO = new SerializedObject(visualManager);
            visualsSO.FindProperty("physicsManager").objectReferenceValue = physicsManager;
            visualsSO.ApplyModifiedProperties();

            SerializedObject controllerSO = new SerializedObject(controller);
            controllerSO.FindProperty("physicsManager").objectReferenceValue = physicsManager;
            controllerSO.ApplyModifiedProperties();

            return physicsManager;
        }

        private static void CreateHud(PhysicsManager physicsManager, WeatherManager weatherManager, Transform headingTransform)
        {
            GameObject canvasGo = new GameObject("HUD Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGo.AddComponent<GraphicRaycaster>();

            CreateCrosshair(canvasGo.transform);
            CreateDiagnosticHud(canvasGo.transform, physicsManager);
            CreateEnvironmentHud(canvasGo.transform, weatherManager, headingTransform);
        }

        private static void CreateCrosshair(Transform canvasParent)
        {
            GameObject crosshairGo = new GameObject("Crosshair");
            crosshairGo.transform.SetParent(canvasParent, false);

            Image image = crosshairGo.AddComponent<Image>();
            image.color = Color.white;

            RectTransform rect = crosshairGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 4f);
        }

        private static void CreateDiagnosticHud(Transform canvasParent, PhysicsManager physicsManager)
        {
            GameObject textGo = new GameObject("DiagnosticHudText");
            textGo.transform.SetParent(canvasParent, false);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.text = "Sin proyectil en vuelo";

            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(420f, 120f);

            GameObject controllerGo = new GameObject("BallisticsHudController");
            controllerGo.transform.SetParent(canvasParent, false);
            BallisticsHudController hudController = controllerGo.AddComponent<BallisticsHudController>();

            SerializedObject so = new SerializedObject(hudController);
            so.FindProperty("hudText").objectReferenceValue = text;
            so.FindProperty("physicsManager").objectReferenceValue = physicsManager;
            so.ApplyModifiedProperties();
        }

        private static void CreateEnvironmentHud(Transform canvasParent, WeatherManager weatherManager, Transform headingTransform)
        {
            GameObject textGo = new GameObject("EnvironmentHudText");
            textGo.transform.SetParent(canvasParent, false);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.TopRight;
            text.color = Color.white;
            text.text = string.Empty;

            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -20f);
            rect.sizeDelta = new Vector2(420f, 180f);

            GameObject controllerGo = new GameObject("EnvironmentHudController");
            controllerGo.transform.SetParent(canvasParent, false);
            EnvironmentHudController hudController = controllerGo.AddComponent<EnvironmentHudController>();

            SerializedObject so = new SerializedObject(hudController);
            so.FindProperty("hudText").objectReferenceValue = text;
            so.FindProperty("weatherManager").objectReferenceValue = weatherManager;
            so.FindProperty("headingTransform").objectReferenceValue = headingTransform;
            so.ApplyModifiedProperties();
        }

        private static void EnsureDataFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets/ShooterScripts/Data", "TestPresets");
            }
        }

        private static BulletData CreateOrLoadBullet()
        {
            string path = DataFolder + "/TestBullet.asset";
            BulletData existing = AssetDatabase.LoadAssetAtPath<BulletData>(path);
            if (existing != null) return existing;

            BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
            bullet.bulletName = "Bala de prueba (.308 168gr)";
            AssetDatabase.CreateAsset(bullet, path);
            return bullet;
        }

        private static WeaponData CreateOrLoadWeapon()
        {
            string path = DataFolder + "/TestWeapon.asset";
            WeaponData existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing != null) return existing;

            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponName = "Rifle de prueba";
            AssetDatabase.CreateAsset(weapon, path);
            return weapon;
        }

        private static void AssignPlayerData(PlayerShooterController controller, BulletData bullet, WeaponData weapon, Transform cameraTransform)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("bulletData").objectReferenceValue = bullet;
            so.FindProperty("weaponData").objectReferenceValue = weapon;
            so.FindProperty("cameraTransform").objectReferenceValue = cameraTransform;
            so.ApplyModifiedProperties();
        }

        private static Material CreateColoredMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        /// Busca una layer de usuario por nombre; si no existe, la crea en el primer slot libre (8-31).
        private static int GetOrCreateLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return i;
                }
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerSP.stringValue))
                {
                    layerSP.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning($"[TestSceneSetup] No hay slots de layer libres para '{layerName}', se uso Default.");
            return 0;
        }
    }
}
