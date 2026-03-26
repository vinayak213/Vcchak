// ---------------------------------------------------------------------------
// GameSetup.cs — One-click editor tool that builds everything needed to
// hit Play: placeholder sprites, prefabs, ScriptableObjects, two scenes
// (MainMenu + Level_Jungle), and wires Build Settings.
//
// Usage:  Unity menu bar → RunAndGun → Setup Game (Full)
// ---------------------------------------------------------------------------

#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunAndGun.Editor
{
    public static class GameSetup
    {
        // ------------------------------------------------------------------ //
        //  Paths
        // ------------------------------------------------------------------ //
        private const string SpritesPath   = "Assets/Sprites";
        private const string PrefabsPath   = "Assets/Prefabs";
        private const string SOPath        = "Assets/ScriptableObjects";
        private const string ScenesPath    = "Assets/Scenes";
        private const string AudioPath     = "Assets/Audio";

        // ------------------------------------------------------------------ //
        //  Menu entry
        // ------------------------------------------------------------------ //
        [MenuItem("RunAndGun/Setup Game (Full)")]
        public static void SetupAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "Run & Gun — Full Setup",
                    "This will create placeholder sprites, prefabs, ScriptableObjects, " +
                    "and two playable scenes (MainMenu + Level_Jungle).\n\n" +
                    "Existing assets with the same names will be overwritten.\n\nContinue?",
                    "Go!", "Cancel"))
                return;

            EnsureFolders();

            // 1. Sprites (Texture2D → Sprite)
            var sprites = CreatePlaceholderSprites();

            // 2. ScriptableObjects
            var weaponData = CreateWeaponData(sprites);
            var enemyData  = CreateEnemyData();

            // 3. Prefabs
            var prefabs = CreatePrefabs(sprites, weaponData, enemyData);

            // 4. Scenes
            string menuScene  = BuildMainMenuScene(sprites, prefabs);
            string levelScene = BuildLevelScene(sprites, prefabs, weaponData, enemyData);

            // 5. Build Settings
            SetBuildScenes(menuScene, levelScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Done!",
                "Setup complete.\n\n" +
                "1. Open the MainMenu scene (Assets/Scenes/MainMenu.unity)\n" +
                "2. Press Play!\n\n" +
                "Controls: WASD move, Space jump, Left-click shoot, Q switch weapon, ESC pause.",
                "OK");
        }

        // ------------------------------------------------------------------ //
        //  Folders
        // ------------------------------------------------------------------ //
        private static void EnsureFolders()
        {
            string[] dirs = { SpritesPath, PrefabsPath, SOPath, ScenesPath, AudioPath,
                              PrefabsPath + "/Enemies", PrefabsPath + "/Weapons",
                              PrefabsPath + "/Effects", PrefabsPath + "/Pickups" };

            foreach (string d in dirs)
            {
                if (!AssetDatabase.IsValidFolder(d))
                {
                    string parent = Path.GetDirectoryName(d).Replace('\\', '/');
                    string folder = Path.GetFileName(d);
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }

        // ================================================================== //
        //  1.  SPRITES
        // ================================================================== //
        private static Dictionary<string, Sprite> CreatePlaceholderSprites()
        {
            var sprites = new Dictionary<string, Sprite>();

            // Player — blue rectangle
            sprites["Player"] = MakeSprite("Player", 32, 48,
                new Color(0.2f, 0.5f, 1f));

            // Bullet — small yellow
            sprites["Bullet"] = MakeSprite("Bullet", 12, 6,
                new Color(1f, 0.9f, 0.2f));

            // EnemyBullet — red small
            sprites["EnemyBullet"] = MakeSprite("EnemyBullet", 10, 10,
                new Color(1f, 0.2f, 0.2f));

            // Ground tile
            sprites["Ground"] = MakeSprite("Ground", 64, 64,
                new Color(0.35f, 0.25f, 0.15f));

            // Platform tile
            sprites["Platform"] = MakeSprite("Platform", 128, 16,
                new Color(0.4f, 0.55f, 0.3f));

            // Enemy — red square
            sprites["GroundEnemy"] = MakeSprite("GroundEnemy", 32, 32,
                new Color(0.9f, 0.15f, 0.15f));

            // Flying enemy — magenta
            sprites["FlyingEnemy"] = MakeSprite("FlyingEnemy", 28, 28,
                new Color(0.85f, 0.1f, 0.7f));

            // Coin
            sprites["Coin"] = MakeSprite("Coin", 16, 16,
                new Color(1f, 0.85f, 0f));

            // Health pickup — green cross
            sprites["HealthPickup"] = MakeSprite("HealthPickup", 20, 20,
                new Color(0.1f, 0.9f, 0.2f));

            // Background
            sprites["Background"] = MakeSprite("Background", 512, 512,
                new Color(0.12f, 0.18f, 0.28f));

            // Explosion
            sprites["Explosion"] = MakeSprite("Explosion", 48, 48,
                new Color(1f, 0.6f, 0.1f));

            // Force refresh so all textures are fully imported
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Now load all sprites after everything is imported
            foreach (string key in new List<string>(sprites.Keys))
            {
                string texPath = $"{SpritesPath}/{key}.png";
                Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                if (spr != null) sprites[key] = spr;
            }

            return sprites;
        }

        private static Sprite MakeSprite(string name, int w, int h, Color color)
        {
            string texPath = $"{SpritesPath}/{name}.png";

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;

            for (int x = 0; x < w; x++)
            {
                pixels[x] = color * 0.5f;
                pixels[x + (h - 1) * w] = color * 0.5f;
            }
            for (int y = 0; y < h; y++)
            {
                pixels[y * w] = color * 0.5f;
                pixels[y * w + w - 1] = color * 0.5f;
            }

            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            // Return a placeholder - will be re-loaded after all sprites are created
            return AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        }

        // ================================================================== //
        //  2.  SCRIPTABLE OBJECTS
        // ================================================================== //
        private static Dictionary<string, WeaponData> CreateWeaponData(
            Dictionary<string, Sprite> sprites)
        {
            var data = new Dictionary<string, WeaponData>();

            // We'll set bullet prefab references after creating prefabs — use
            // a second pass.  For now create the SO with stats only.

            data["RapidFire"] = CreateWeaponSO("RapidFire", WeaponType.RapidFire,
                damage: 10, fireRate: 8, bulletSpeed: 22, ammo: -1,
                spread: 0, bulletsPerShot: 1, sprites);

            data["SpreadShot"] = CreateWeaponSO("SpreadShot", WeaponType.SpreadShot,
                damage: 8, fireRate: 3, bulletSpeed: 18, ammo: 50,
                spread: 30, bulletsPerShot: 3, sprites);

            data["Laser"] = CreateWeaponSO("Laser", WeaponType.Laser,
                damage: 25, fireRate: 1, bulletSpeed: 0, ammo: -1,
                spread: 0, bulletsPerShot: 1, sprites);

            data["Explosive"] = CreateWeaponSO("Explosive", WeaponType.Explosive,
                damage: 40, fireRate: 1.5f, bulletSpeed: 14, ammo: 20,
                spread: 0, bulletsPerShot: 1, sprites);

            return data;
        }

        private static WeaponData CreateWeaponSO(string name, WeaponType type,
            float damage, float fireRate, float bulletSpeed, int ammo,
            float spread, int bulletsPerShot, Dictionary<string, Sprite> sprites)
        {
            string path = $"{SOPath}/{name}Data.asset";
            var so = ScriptableObject.CreateInstance<WeaponData>();

            // Use SerializedObject to set private serialized fields
            AssetDatabase.CreateAsset(so, path);
            SerializedObject ser = new SerializedObject(so);
            ser.FindProperty("weaponName").stringValue = name;
            ser.FindProperty("weaponType").enumValueIndex = (int)type;
            ser.FindProperty("damage").floatValue = damage;
            ser.FindProperty("fireRate").floatValue = fireRate;
            ser.FindProperty("bulletSpeed").floatValue = bulletSpeed;
            ser.FindProperty("ammoCapacity").intValue = ammo;
            ser.FindProperty("spreadAngle").floatValue = spread;
            ser.FindProperty("bulletsPerShot").intValue = bulletsPerShot;
            ser.ApplyModifiedPropertiesWithoutUndo();

            return so;
        }

        private static Dictionary<string, EnemyData> CreateEnemyData()
        {
            var data = new Dictionary<string, EnemyData>();

            data["GroundSoldier"] = CreateEnemySO("GroundSoldier",
                health: 3, speed: 3, contactDmg: 1, detect: 10, attack: 6,
                retreat: 2, cooldown: 1.2f, projDmg: 1, projSpeed: 8, score: 100);

            data["FlyingDrone"] = CreateEnemySO("FlyingDrone",
                health: 2, speed: 4, contactDmg: 1, detect: 12, attack: 8,
                retreat: 3, cooldown: 1.5f, projDmg: 1, projSpeed: 6, score: 150);

            return data;
        }

        private static EnemyData CreateEnemySO(string name,
            int health, float speed, int contactDmg, float detect, float attack,
            float retreat, float cooldown, int projDmg, float projSpeed, int score)
        {
            string path = $"{SOPath}/{name}Data.asset";
            var so = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(so, path);

            SerializedObject ser = new SerializedObject(so);
            ser.FindProperty("maxHealth").intValue = health;
            ser.FindProperty("moveSpeed").floatValue = speed;
            ser.FindProperty("contactDamage").intValue = contactDmg;
            ser.FindProperty("detectionRange").floatValue = detect;
            ser.FindProperty("attackRange").floatValue = attack;
            ser.FindProperty("retreatRange").floatValue = retreat;
            ser.FindProperty("attackCooldown").floatValue = cooldown;
            ser.FindProperty("projectileDamage").intValue = projDmg;
            ser.FindProperty("projectileSpeed").floatValue = projSpeed;
            ser.FindProperty("scoreValue").intValue = score;
            ser.ApplyModifiedPropertiesWithoutUndo();

            return so;
        }

        // ================================================================== //
        //  3.  PREFABS
        // ================================================================== //
        private static Dictionary<string, GameObject> CreatePrefabs(
            Dictionary<string, Sprite> sprites,
            Dictionary<string, WeaponData> weaponData,
            Dictionary<string, EnemyData> enemyData)
        {
            var prefabs = new Dictionary<string, GameObject>();

            // --- Bullet ---
            prefabs["Bullet"] = CreateBulletPrefab(sprites["Bullet"], "Bullet",
                "PlayerBullet");

            // --- Enemy Bullet ---
            prefabs["EnemyBullet"] = CreateBulletPrefab(sprites["EnemyBullet"],
                "EnemyBullet", "EnemyBullet");

            // Wire bullet prefab into weapon data
            SetBulletOnWeaponData(weaponData["RapidFire"], prefabs["Bullet"]);
            SetBulletOnWeaponData(weaponData["SpreadShot"], prefabs["Bullet"]);
            SetBulletOnWeaponData(weaponData["Explosive"], prefabs["Bullet"]);

            // --- Coin ---
            prefabs["Coin"] = CreateCoinPrefab(sprites["Coin"]);

            // --- Health Pickup ---
            prefabs["HealthPickup"] = CreateHealthPickupPrefab(sprites["HealthPickup"]);

            // --- Ground Enemy ---
            prefabs["GroundEnemy"] = CreateGroundEnemyPrefab(
                sprites["GroundEnemy"], enemyData["GroundSoldier"],
                prefabs["EnemyBullet"]);

            // --- Flying Enemy ---
            prefabs["FlyingEnemy"] = CreateFlyingEnemyPrefab(
                sprites["FlyingEnemy"], enemyData["FlyingDrone"]);

            return prefabs;
        }

        private static GameObject CreateBulletPrefab(Sprite sprite, string name,
            string tag)
        {
            GameObject go = new GameObject(name);

            // Ensure tag exists (may fail if tags aren't in TagManager — that's OK)
            try { go.tag = tag; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            if (name == "EnemyBullet")
            {
                go.AddComponent<EnemyBullet>();
            }
            else
            {
                go.AddComponent<Bullet>();
            }

            string path = $"{PrefabsPath}/Weapons/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void SetBulletOnWeaponData(WeaponData wd, GameObject bulletPrefab)
        {
            SerializedObject ser = new SerializedObject(wd);
            ser.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
            ser.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateCoinPrefab(Sprite sprite)
        {
            GameObject go = new GameObject("Coin");
            try { go.tag = "Pickup"; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            go.AddComponent<Coin>();

            string path = $"{PrefabsPath}/Pickups/Coin.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateHealthPickupPrefab(Sprite sprite)
        {
            GameObject go = new GameObject("HealthPickup");
            try { go.tag = "Pickup"; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.6f;

            go.AddComponent<HealthPickup>();

            string path = $"{PrefabsPath}/Pickups/HealthPickup.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateGroundEnemyPrefab(Sprite sprite,
            EnemyData data, GameObject bulletPrefab)
        {
            GameObject go = new GameObject("GroundEnemy");
            try { go.tag = "Enemy"; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 2;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 1.8f);

            var enemy = go.AddComponent<GroundEnemy>();

            // Set enemy data via serialized property
            SerializedObject ser = new SerializedObject(enemy);
            ser.FindProperty("data").objectReferenceValue = data;
            ser.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
            ser.ApplyModifiedPropertiesWithoutUndo();

            // Fire point child
            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(go.transform);
            fp.transform.localPosition = new Vector3(1f, 0.2f, 0f);

            SerializedObject ser2 = new SerializedObject(enemy);
            ser2.FindProperty("firePoint").objectReferenceValue = fp.transform;
            ser2.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabsPath}/Enemies/GroundEnemy.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateFlyingEnemyPrefab(Sprite sprite,
            EnemyData data)
        {
            GameObject go = new GameObject("FlyingEnemy");
            try { go.tag = "Enemy"; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 2;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);

            var enemy = go.AddComponent<FlyingEnemy>();
            SerializedObject ser = new SerializedObject(enemy);
            ser.FindProperty("data").objectReferenceValue = data;
            ser.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabsPath}/Enemies/FlyingEnemy.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ================================================================== //
        //  4.  SCENES
        // ================================================================== //

        // -------------------- MAIN MENU -------------------- //
        private static string BuildMainMenuScene(
            Dictionary<string, Sprite> sprites,
            Dictionary<string, GameObject> prefabs)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Camera setup
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5;
                cam.backgroundColor = new Color(0.08f, 0.1f, 0.18f);
                cam.transform.position = new Vector3(0, 0, -10);
            }

            // -- Managers --
            CreateManagerObjects(prefabs);

            // -- Canvas --
            GameObject canvas = CreateCanvas("MainMenuCanvas");

            // Title text
            GameObject titleGO = CreateUIText(canvas.transform, "TitleText",
                "RUN & GUN", 48, Color.white,
                new Vector2(0, 120), new Vector2(600, 80));

            // Play button
            GameObject playBtn = CreateUIButton(canvas.transform, "PlayButton",
                "PLAY", new Vector2(0, 20), new Vector2(200, 50));

            // Quit button
            GameObject quitBtn = CreateUIButton(canvas.transform, "QuitButton",
                "QUIT", new Vector2(0, -50), new Vector2(200, 50));

            // Wire MainMenuUI
            var menuUI = canvas.AddComponent<MainMenuUI>();
            SerializedObject ser = new SerializedObject(menuUI);
            ser.FindProperty("playButton").objectReferenceValue =
                playBtn.GetComponent<Button>();
            ser.FindProperty("quitButton").objectReferenceValue =
                quitBtn.GetComponent<Button>();
            ser.FindProperty("titleTransform").objectReferenceValue =
                titleGO.GetComponent<RectTransform>();
            ser.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{ScenesPath}/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // -------------------- LEVEL SCENE -------------------- //
        private static string BuildLevelScene(
            Dictionary<string, Sprite> sprites,
            Dictionary<string, GameObject> prefabs,
            Dictionary<string, WeaponData> weaponData,
            Dictionary<string, EnemyData> enemyData)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // --- Camera ---
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5;
                cam.backgroundColor = new Color(0.12f, 0.18f, 0.28f);
                cam.transform.position = new Vector3(0, 2, -10);

                cam.gameObject.AddComponent<CameraController>();
                cam.gameObject.AddComponent<ScreenShake>();
            }

            // --- Managers ---
            CreateManagerObjects(prefabs);

            // --- Player ---
            GameObject player = CreatePlayer(sprites, weaponData);

            // Wire camera target
            if (cam != null)
            {
                SerializedObject camSer = new SerializedObject(
                    cam.GetComponent<CameraController>());
                camSer.FindProperty("target").objectReferenceValue = player.transform;
                camSer.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- Ground ---
            CreateGround(sprites["Ground"]);

            // --- Platforms ---
            CreatePlatform(sprites["Platform"], new Vector3(-3, -1, 0));
            CreatePlatform(sprites["Platform"], new Vector3(5, 1, 0));
            CreatePlatform(sprites["Platform"], new Vector3(12, 2.5f, 0));
            CreatePlatform(sprites["Platform"], new Vector3(20, 0.5f, 0));

            // --- Coins ---
            PlaceCoin(prefabs, new Vector3(3, 0, 0));
            PlaceCoin(prefabs, new Vector3(6, 2.5f, 0));
            PlaceCoin(prefabs, new Vector3(12, 4, 0));
            PlaceCoin(prefabs, new Vector3(15, 0, 0));
            PlaceCoin(prefabs, new Vector3(18, 0, 0));

            // --- Enemies ---
            PlaceEnemy(prefabs["GroundEnemy"], new Vector3(10, -1.5f, 0));
            PlaceEnemy(prefabs["GroundEnemy"], new Vector3(22, -1.5f, 0));
            PlaceEnemy(prefabs["FlyingEnemy"], new Vector3(16, 4, 0));

            // --- HUD Canvas ---
            GameObject hudCanvas = CreateCanvas("HUDCanvas");
            var gameplayUI = hudCanvas.AddComponent<GameplayUI>();

            // Score text
            GameObject scoreText = CreateUIText(hudCanvas.transform, "ScoreText",
                "Score: 0", 24, Color.white,
                new Vector2(-300, 220), new Vector2(200, 40));

            // Lives text
            GameObject livesText = CreateUIText(hudCanvas.transform, "LivesText",
                "Lives: 3", 24, Color.white,
                new Vector2(300, 220), new Vector2(200, 40));

            // Coins text
            GameObject coinsText = CreateUIText(hudCanvas.transform, "CoinText",
                "Coins: 0", 20, new Color(1, 0.85f, 0),
                new Vector2(-300, 190), new Vector2(200, 30));

            SerializedObject hudSer = new SerializedObject(gameplayUI);
            hudSer.FindProperty("scoreText").objectReferenceValue =
                scoreText.GetComponent<Text>();
            hudSer.FindProperty("livesText").objectReferenceValue =
                livesText.GetComponent<Text>();
            hudSer.FindProperty("coinText").objectReferenceValue =
                coinsText.GetComponent<Text>();
            hudSer.ApplyModifiedPropertiesWithoutUndo();

            // --- Pause Menu ---
            GameObject pauseCanvas = CreateCanvas("PauseCanvas");
            var pauseUI = pauseCanvas.AddComponent<PauseMenuUI>();

            GameObject pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(pauseCanvas.transform, false);
            var pauseRect = pausePanel.AddComponent<RectTransform>();
            pauseRect.anchorMin = Vector2.zero;
            pauseRect.anchorMax = Vector2.one;
            pauseRect.sizeDelta = Vector2.zero;

            // Semi-transparent background
            var pauseBG = pausePanel.AddComponent<Image>();
            pauseBG.color = new Color(0, 0, 0, 0.6f);

            // Pause title
            CreateUIText(pausePanel.transform, "PauseTitle", "PAUSED", 36,
                Color.white, new Vector2(0, 80), new Vector2(300, 60));

            // Resume button
            GameObject resumeBtn = CreateUIButton(pausePanel.transform, "ResumeBtn",
                "RESUME", new Vector2(0, 10), new Vector2(200, 45));

            // Restart button
            GameObject restartBtn = CreateUIButton(pausePanel.transform, "RestartBtn",
                "RESTART", new Vector2(0, -45), new Vector2(200, 45));

            // Main menu button
            GameObject menuBtn = CreateUIButton(pausePanel.transform, "MenuBtn",
                "MAIN MENU", new Vector2(0, -100), new Vector2(200, 45));

            SerializedObject pauseSer = new SerializedObject(pauseUI);
            pauseSer.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            pauseSer.FindProperty("resumeButton").objectReferenceValue =
                resumeBtn.GetComponent<Button>();
            pauseSer.FindProperty("restartButton").objectReferenceValue =
                restartBtn.GetComponent<Button>();
            pauseSer.FindProperty("mainMenuButton").objectReferenceValue =
                menuBtn.GetComponent<Button>();
            pauseSer.ApplyModifiedPropertiesWithoutUndo();

            pausePanel.SetActive(false);

            // --- Game Over UI ---
            GameObject goCanvas = CreateCanvas("GameOverCanvas");
            var gameOverUI = goCanvas.AddComponent<GameOverUI>();

            GameObject goPanel = new GameObject("GameOverPanel");
            goPanel.transform.SetParent(goCanvas.transform, false);
            var goRect = goPanel.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.sizeDelta = Vector2.zero;

            var goBG = goPanel.AddComponent<Image>();
            goBG.color = new Color(0.3f, 0, 0, 0.7f);

            CreateUIText(goPanel.transform, "GOTitle", "GAME OVER", 42,
                Color.red, new Vector2(0, 80), new Vector2(400, 60));

            GameObject goScoreLabel = CreateUIText(goPanel.transform, "GOScore",
                "Score: 0", 28, Color.white,
                new Vector2(0, 20), new Vector2(300, 40));

            GameObject retryBtn = CreateUIButton(goPanel.transform, "RetryBtn",
                "RETRY", new Vector2(0, -40), new Vector2(200, 45));

            GameObject goMenuBtn = CreateUIButton(goPanel.transform, "GOMenuBtn",
                "MAIN MENU", new Vector2(0, -95), new Vector2(200, 45));

            SerializedObject goSer = new SerializedObject(gameOverUI);
            goSer.FindProperty("gameOverPanel").objectReferenceValue = goPanel;
            goSer.FindProperty("scoreLabel").objectReferenceValue =
                goScoreLabel.GetComponent<Text>();
            goSer.FindProperty("retryButton").objectReferenceValue =
                retryBtn.GetComponent<Button>();
            goSer.FindProperty("mainMenuButton").objectReferenceValue =
                goMenuBtn.GetComponent<Button>();
            goSer.ApplyModifiedPropertiesWithoutUndo();

            goPanel.SetActive(false);

            // --- Background ---
            GameObject bg = new GameObject("Background");
            var bgSR = bg.AddComponent<SpriteRenderer>();
            bgSR.sprite = sprites["Background"];
            bgSR.sortingOrder = -100;
            bg.transform.position = new Vector3(10, 0, 5);
            bg.transform.localScale = new Vector3(3, 1, 1);

            string path = $"{ScenesPath}/Level_Jungle.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ================================================================== //
        //  Helpers — Scene building blocks
        // ================================================================== //

        private static void CreateManagerObjects(
            Dictionary<string, GameObject> prefabs)
        {
            // GameManager
            GameObject gm = new GameObject("GameManager");
            var gmComp = gm.AddComponent<GameManager>();
            SerializedObject gmSer = new SerializedObject(gmComp);
            gmSer.FindProperty("levelScenes").arraySize = 1;
            gmSer.FindProperty("levelScenes").GetArrayElementAtIndex(0).stringValue = "Level_Jungle";
            gmSer.ApplyModifiedPropertiesWithoutUndo();

            // AudioManager
            GameObject am = new GameObject("AudioManager");
            am.AddComponent<AudioManager>();
            var srcA = am.AddComponent<AudioSource>();
            srcA.loop = true;
            var srcB = am.AddComponent<AudioSource>();
            srcB.loop = true;

            SerializedObject amSer = new SerializedObject(am.GetComponent<AudioManager>());
            amSer.FindProperty("musicSourceA").objectReferenceValue = srcA;
            // The second AudioSource
            AudioSource[] sources = am.GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                amSer.FindProperty("musicSourceB").objectReferenceValue = sources[1];
            }
            amSer.ApplyModifiedPropertiesWithoutUndo();

            // ScoreManager
            GameObject sm = new GameObject("ScoreManager");
            sm.AddComponent<ScoreManager>();

            // InputManager
            GameObject im = new GameObject("InputManager");
            im.AddComponent<InputManager>();

            // ObjectPool
            GameObject op = new GameObject("ObjectPool");
            op.AddComponent<ObjectPool>();
        }

        private static GameObject CreatePlayer(
            Dictionary<string, Sprite> sprites,
            Dictionary<string, WeaponData> weaponData)
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.layer = LayerMask.NameToLayer("Default");

            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = sprites["Player"];
            sr.sortingOrder = 10;

            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            player.AddComponent<CapsuleCollider2D>();

            var pc = player.AddComponent<PlayerController>();
            // Set groundLayers to Default layer (where our ground tiles live)
            SerializedObject pcSer = new SerializedObject(pc);
            pcSer.FindProperty("groundLayers").intValue = 1 << LayerMask.NameToLayer("Default");
            pcSer.ApplyModifiedPropertiesWithoutUndo();

            player.AddComponent<PlayerHealth>();

            // Fire point child
            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(player.transform);
            firePoint.transform.localPosition = new Vector3(1.2f, 0.3f, 0);

            // Weapon child — RapidFire (default)
            GameObject wpnGO = new GameObject("RapidFireWeapon");
            wpnGO.transform.SetParent(player.transform);
            wpnGO.transform.localPosition = Vector3.zero;
            var wpn = wpnGO.AddComponent<RapidFireWeapon>();
            wpnGO.AddComponent<AudioSource>();

            SerializedObject wpnSer = new SerializedObject(wpn);
            wpnSer.FindProperty("weaponData").objectReferenceValue =
                weaponData["RapidFire"];
            wpnSer.FindProperty("firePoint").objectReferenceValue =
                firePoint.transform;
            wpnSer.ApplyModifiedPropertiesWithoutUndo();

            // PlayerCombat
            var combat = player.AddComponent<PlayerCombat>();
            SerializedObject combatSer = new SerializedObject(combat);
            var slotsArr = combatSer.FindProperty("weaponSlots");
            slotsArr.arraySize = 1;
            slotsArr.GetArrayElementAtIndex(0).objectReferenceValue = wpn;
            combatSer.FindProperty("firePointOverride").objectReferenceValue =
                firePoint.transform;
            combatSer.ApplyModifiedPropertiesWithoutUndo();

            // Animator controller (optional — works without Animator)
            player.AddComponent<PlayerAnimatorController>();

            player.transform.position = new Vector3(-5, 0, 0);

            return player;
        }

        private static void CreateGround(Sprite groundSprite)
        {
            // Create a long ground strip from tiles
            for (int i = -3; i < 10; i++)
            {
                GameObject tile = new GameObject($"Ground_{i}");
                tile.tag = "Ground";
                tile.layer = LayerMask.NameToLayer("Default");

                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = groundSprite;
                sr.sortingOrder = -1;

                var col = tile.AddComponent<BoxCollider2D>();
                tile.transform.position = new Vector3(i * 4, -4, 0);
                tile.transform.localScale = Vector3.one;
            }
        }

        private static void CreatePlatform(Sprite platSprite, Vector3 pos)
        {
            GameObject plat = new GameObject("Platform");
            plat.tag = "Ground";

            var sr = plat.AddComponent<SpriteRenderer>();
            sr.sprite = platSprite;
            sr.sortingOrder = -1;

            plat.AddComponent<BoxCollider2D>();
            plat.transform.position = pos;
        }

        private static void PlaceCoin(Dictionary<string, GameObject> prefabs,
            Vector3 pos)
        {
            if (!prefabs.ContainsKey("Coin")) return;
            GameObject coin = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Coin"]);
            coin.transform.position = pos;
        }

        private static void PlaceEnemy(GameObject prefab, Vector3 pos)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.transform.position = pos;
        }

        // ================================================================== //
        //  Helpers — UI
        // ================================================================== //

        private static GameObject CreateCanvas(string name)
        {
            GameObject canvas = new GameObject(name);
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = name.Contains("Pause") ? 20 :
                             name.Contains("GameOver") ? 25 : 10;

            canvas.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution =
                new Vector2(1920, 1080);

            canvas.AddComponent<GraphicRaycaster>();

            // EventSystem (only one per scene)
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        private static GameObject CreateUIText(Transform parent, string name,
            string content, int fontSize, Color color, Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return go;
        }

        private static GameObject CreateUIButton(Transform parent, string name,
            string label, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            var btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            btn.colors = colors;

            // Label child
            GameObject textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);

            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 22;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null)
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return go;
        }

        // ================================================================== //
        //  5.  BUILD SETTINGS
        // ================================================================== //
        private static void SetBuildScenes(string menuScene, string levelScene)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuScene, true),
                new EditorBuildSettingsScene(levelScene, true),
            };
        }
    }
}

#endif
