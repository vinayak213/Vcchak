#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RunAndGun.Editor
{
    public static class FixSprites
    {
        [MenuItem("RunAndGun/Fix All Sprites In Scene")]
        public static void Fix()
        {
            int fixed_count = 0;

            // Find all SpriteRenderers in the scene
            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsSortMode.None);

            foreach (SpriteRenderer sr in renderers)
            {
                string goName = sr.gameObject.name;
                string spriteName = null;

                // Match game object name to sprite name
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
                // Load all sub-assets and find the Sprite one
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
                    Debug.Log($"[FixSprites] {goName} -> {spr.name} ({spr.texture.name})");
                }
            }

            // Mark scene dirty so it can be saved
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Fix Sprites",
                $"Fixed {fixed_count} sprite references.\n\nPress Ctrl+S to save the scene.",
                "OK");
        }
    }
}
#endif
