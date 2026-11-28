
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TheLastGift.UI;
namespace TheLastGift.Editor
{
    public static class LastGiftUIPrefabFactory
    {
        [MenuItem("The Last Gift/UI/Create UI Root Prefab")]
        public static void Create()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Prefabs"))AssetDatabase.CreateFolder("Assets","Prefabs");
            if(!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))AssetDatabase.CreateFolder("Assets/Prefabs","UI");
            var root=new GameObject("LastGiftUI",typeof(RectTransform),typeof(LastGiftUIController));
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/UI/LastGiftUI.prefab");
            Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("The Last Gift UI","Prefab saved to Assets/Prefabs/UI/LastGiftUI.prefab","OK");
        }
    }
}
#endif