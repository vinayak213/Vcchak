#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RunAndGun.Editor
{
    public static class FixSprites
    {
        [MenuItem("RunAndGun/1 - Reimport Sprites")]
        public static void ReimportSprites()
        {
            string[] names = {
                "Player", "Bullet", "EnemyBullet", "Ground", "Platform",
                "GroundEnemy", "FlyingEnemy", "Coin", "HealthPickup",
                "Background", "Explosion"
            };

            foreach (string n in names)
            {
                string path = $"Assets/Sprites/{n}.png";
                TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null)
                {
                    Debug.LogWarning($"[ReimportSprites] No importer found for {path}");
                    continue;
                }

                Debug.Log($"[ReimportSprites] {n}: current type = {imp.textureType}");
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 16;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                EditorUtility.SetDirty(imp);
                imp.SaveAndReimport();

                // Verify
                Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr != null)
                    Debug.Log($"[ReimportSprites] SUCCESS: {n} -> sprite loaded, tex={spr.texture.name}");
                else
                    Debug.LogError($"[ReimportSprites] FAIL: {n} -> sprite is still null after reimport!");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorUtility.DisplayDialog("Reimport Sprites",
                "Done! Check Console for results.\n\nNow run: RunAndGun > 2 - Fix Scene",
                "OK");
        }

        [MenuItem("RunAndGun/2 - Fix Scene")]
        public static void FixScene()
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
                Undo.DestroyObjectImmediate(dirLight);

            // ---- Load sprites ----
            string[] spriteNames = {
                "Player", "Ground", "Platform", "Coin", "GroundEnemy",
                "FlyingEnemy", "HealthPickup", "Background"
            };

            var spriteMap = new System.Collections.Generic.Dictionary<string, Sprite>();
            foreach (string sn in spriteNames)
            {
                string path = $"Assets/Sprites/{sn}.png";
                Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr != null)
                {
                    spriteMap[sn] = spr;
                    Debug.Log($"[FixScene] Loaded {sn}: tex={spr.texture.name}, w={spr.texture.width}");
                }
                else
                {
                    Debug.LogError($"[FixScene] Cannot load sprite: {path}");
                }
            }

            // ---- Fix all SpriteRenderers ----
            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            foreach (SpriteRenderer sr in renderers)
            {
                string goName = sr.gameObject.name;
                string spriteName = null;

                if (goName == "Player") spriteName = "Player";
                else if (goName.StartsWith("Ground_")) spriteName = "Ground";
                else if (goName == "Platform") spriteName = "Platform";
                else if (goName == "Coin") spriteName = "Coin";
                else if (goName == "GroundEnemy") spriteName = "GroundEnemy";
                else if (goName == "FlyingEnemy") spriteName = "FlyingEnemy";
                else if (goName == "HealthPickup") spriteName = "HealthPickup";
                else if (goName == "Background") spriteName = "Background";

                if (spriteName == null || !spriteMap.ContainsKey(spriteName)) continue;

                sr.sprite = spriteMap[spriteName];
                EditorUtility.SetDirty(sr);
                fixed_count++;
            }

            // ---- Fix ground colliders: not triggers + Layer Overrides ----
            BoxCollider2D[] boxes = Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
            int triggerFixes = 0;
            foreach (BoxCollider2D box in boxes)
            {
                string goName = box.gameObject.name;
                if (goName.StartsWith("Ground_") || goName == "Platform")
                {
                    if (box.isTrigger)
                    {
                        box.isTrigger = false;
                        triggerFixes++;
                    }
                    // Fix Unity 6 Layer Overrides to ensure collisions work
                    box.includeLayers = ~0;
                    box.excludeLayers = 0;
                    box.contactCaptureLayers = ~0;
                    box.callbackLayers = ~0;
                    box.forceReceiveLayers = ~0;
                    box.forceSendLayers = ~0;
                    EditorUtility.SetDirty(box);
                }
            }
            Debug.Log($"[FixScene] Configured {boxes.Length} BoxCollider2D Layer Overrides (triggerFixes={triggerFixes})");

            // ---- Fix Player ----
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.gravityScale = 3f;
                    rb.freezeRotation = true;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    // Fix Unity 6 Layer Overrides
                    rb.includeLayers = ~0;
                    rb.excludeLayers = 0;
                    EditorUtility.SetDirty(rb);
                }

                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    var so = new SerializedObject(pc);
                    so.FindProperty("groundLayers").intValue = 1 << 0;
                    so.ApplyModifiedProperties();
                }

                // Fix CapsuleCollider2D size to match sprite (32x48 at 16 PPU = 2x3 units)
                var col = player.GetComponent<CapsuleCollider2D>();
                if (col != null)
                {
                    col.size = new Vector2(1.8f, 2.8f);
                    col.offset = new Vector2(0f, 0f);
                    // Fix Unity 6 Layer Overrides
                    col.includeLayers = ~0;
                    col.excludeLayers = 0;
                    col.contactCaptureLayers = ~0;
                    col.callbackLayers = ~0;
                    col.forceReceiveLayers = ~0;
                    col.forceSendLayers = ~0;
                    EditorUtility.SetDirty(col);
                }

                player.transform.position = new Vector3(-5, 0, 0);
                EditorUtility.SetDirty(player);

                // Remove PlayerAnimatorController — causes 999+ errors without an AnimatorController
                var animCtrl = player.GetComponent<PlayerAnimatorController>();
                if (animCtrl != null)
                    Object.DestroyImmediate(animCtrl);

                // Remove Animator if no controller assigned
                var animator = player.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController == null)
                    Object.DestroyImmediate(animator);
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Fix Scene",
                $"Fixed {fixed_count} sprites.\nCheck Console.\n\nPress Ctrl+S, then Play!",
                "OK");
        }
    }
}
#endif
