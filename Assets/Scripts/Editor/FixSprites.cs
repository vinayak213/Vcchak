#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RunAndGun.Editor
{
    public static class FixSprites
    {
        [MenuItem("RunAndGun/Fix All Sprites In Scene")]
        public static void Fix()
        {
            int fixed_count = 0;

            // ---- Fix Camera ----
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.18f, 0.28f);
                cam.transform.position = new Vector3(0, 0, -10);
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(cam.gameObject);
            }

            // ---- Delete Directional Light ----
            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                Undo.DestroyObjectImmediate(dirLight);
            }

            // ---- Pre-load all sprites ----
            string[] spriteNames = {
                "Player", "Ground", "Platform", "Coin", "GroundEnemy",
                "FlyingEnemy", "HealthPickup", "Background", "Bullet",
                "EnemyBullet", "Explosion"
            };

            System.Collections.Generic.Dictionary<string, Sprite> spriteMap =
                new System.Collections.Generic.Dictionary<string, Sprite>();

            foreach (string sn in spriteNames)
            {
                string path = $"Assets/Sprites/{sn}.png";

                // Method 1: Try LoadAssetAtPath<Sprite>
                Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                // Method 2: If that failed or returned wrong name, try sub-assets
                if (spr == null || spr.texture.name != sn)
                {
                    Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (Object obj in all)
                    {
                        if (obj is Sprite s && s.texture != null && s.texture.name == sn)
                        {
                            spr = s;
                            break;
                        }
                    }
                }

                // Method 3: Load texture and get sprite from it
                if (spr == null || spr.texture.name != sn)
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                    {
                        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (imp != null && imp.textureType != TextureImporterType.Sprite)
                        {
                            imp.textureType = TextureImporterType.Sprite;
                            imp.spritePixelsPerUnit = 16;
                            imp.filterMode = FilterMode.Point;
                            imp.SaveAndReimport();
                        }
                        spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }

                if (spr != null)
                {
                    spriteMap[sn] = spr;
                    Debug.Log($"[FixSprites] Loaded sprite '{sn}': texture={spr.texture.name}, rect={spr.rect}");
                }
                else
                {
                    Debug.LogWarning($"[FixSprites] FAILED to load sprite: {path}");
                }
            }

            // ---- Fix all SpriteRenderers ----
            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsSortMode.None);

            foreach (SpriteRenderer sr in renderers)
            {
                string goName = sr.gameObject.name;
                string spriteName = null;

                if (goName == "Player") spriteName = "Player";
                else if (goName.StartsWith("Ground_") || goName == "Ground") spriteName = "Ground";
                else if (goName == "Platform") spriteName = "Platform";
                else if (goName == "Coin") spriteName = "Coin";
                else if (goName == "GroundEnemy") spriteName = "GroundEnemy";
                else if (goName == "FlyingEnemy") spriteName = "FlyingEnemy";
                else if (goName == "HealthPickup") spriteName = "HealthPickup";
                else if (goName == "Background") spriteName = "Background";

                if (spriteName == null) continue;

                if (spriteMap.ContainsKey(spriteName))
                {
                    Sprite target = spriteMap[spriteName];
                    Undo.RecordObject(sr, "Fix Sprite");
                    sr.sprite = target;
                    EditorUtility.SetDirty(sr);
                    fixed_count++;
                    Debug.Log($"[FixSprites] Assigned {goName} -> {target.texture.name}");
                }
            }

            // ---- Fix Player ----
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");

            if (player != null)
            {
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Undo.RecordObject(rb, "Fix Player Rigidbody");
                    rb.gravityScale = 3f;
                    rb.freezeRotation = true;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    EditorUtility.SetDirty(rb);
                }

                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    SerializedObject so = new SerializedObject(pc);
                    var groundProp = so.FindProperty("groundLayers");
                    if (groundProp != null)
                    {
                        groundProp.intValue = 1 << 0;
                        so.ApplyModifiedProperties();
                    }
                }

                player.transform.position = new Vector3(-5, 0, 0);
                EditorUtility.SetDirty(player);
            }

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[FixSprites] === DONE === Fixed {fixed_count} sprites");

            EditorUtility.DisplayDialog("Fix Sprites & Scene",
                $"Fixed {fixed_count} sprite references.\n" +
                "Also fixed: Camera, Player, Directional Light.\n\n" +
                "Check Console for details.\nPress Ctrl+S to save, then Play!",
                "OK");
        }
    }
}
#endif
