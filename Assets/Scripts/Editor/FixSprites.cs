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
                Debug.Log("[FixSprites] Fixed camera: orthographic, solid color bg");
            }

            // ---- Delete Directional Light (not needed for 2D) ----
            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                Undo.DestroyObjectImmediate(dirLight);
                Debug.Log("[FixSprites] Removed Directional Light");
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
                else if (goName == "Bullet") spriteName = "Bullet";
                else if (goName == "EnemyBullet") spriteName = "EnemyBullet";
                else if (goName == "Explosion") spriteName = "Explosion";

                if (spriteName == null) continue;

                string path = $"Assets/Sprites/{spriteName}.png";
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                Sprite spr = null;
                foreach (Object a in assets)
                {
                    if (a is Sprite s)
                    {
                        spr = s;
                        break;
                    }
                }
                if (spr != null)
                {
                    Undo.RecordObject(sr, "Fix Sprite");
                    sr.sprite = spr;
                    EditorUtility.SetDirty(sr);
                    fixed_count++;
                    Debug.Log($"[FixSprites] {goName} -> {spr.name} (tex: {spr.texture.name})");
                }
            }

            // ---- Fix Player specifically ----
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");

            if (player != null)
            {
                // Fix Rigidbody2D
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Undo.RecordObject(rb, "Fix Player Rigidbody");
                    rb.gravityScale = 3f;
                    rb.freezeRotation = true;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    EditorUtility.SetDirty(rb);
                    Debug.Log("[FixSprites] Fixed Player Rigidbody2D: gravity=3");
                }

                // Fix PlayerController groundLayers
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    SerializedObject so = new SerializedObject(pc);
                    var groundProp = so.FindProperty("groundLayers");
                    if (groundProp != null)
                    {
                        // Set to Default layer (layer 0)
                        groundProp.intValue = 1 << 0; // Default layer
                        so.ApplyModifiedProperties();
                        Debug.Log("[FixSprites] Fixed PlayerController.groundLayers = Default");
                    }
                }

                // Reset position
                player.transform.position = new Vector3(-5, 0, 0);
                EditorUtility.SetDirty(player);
                Debug.Log("[FixSprites] Reset Player position to (-5, 0, 0)");
            }

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Fix Sprites & Scene",
                $"Fixed {fixed_count} sprite references.\n" +
                "Also fixed: Camera (2D), Player (groundLayers, gravity, position), removed Directional Light.\n\n" +
                "Press Ctrl+S to save, then Press Play!",
                "OK");
        }
    }
}
#endif
