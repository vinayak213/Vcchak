#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RunAndGun.Editor
{
    /// <summary>
    /// Custom editor menus for creating game assets quickly.
    /// </summary>
    public static class CreateAssetMenus
    {
        [MenuItem("Assets/Create/RunAndGun/Weapon Data")]
        public static void CreateWeaponData()
        {
            WeaponData asset = ScriptableObject.CreateInstance<WeaponData>();
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                path = "Assets/Resources";

            path = AssetDatabase.GenerateUniqueAssetPath(path + "/NewWeaponData.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        [MenuItem("Assets/Create/RunAndGun/Enemy Data")]
        public static void CreateEnemyData()
        {
            EnemyData asset = ScriptableObject.CreateInstance<EnemyData>();
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                path = "Assets/Resources";

            path = AssetDatabase.GenerateUniqueAssetPath(path + "/NewEnemyData.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}
#endif
