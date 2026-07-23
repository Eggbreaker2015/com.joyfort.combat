#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Combat.Unity.Editor
{
    public static partial class BattleAuthoringValidator
    {
        private static T[] LoadAssets<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var assets = new List<T>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets.ToArray();
        }
    }
}
#endif
