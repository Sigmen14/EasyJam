#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TheLastGift.Packing;

public static class PackingGiftAssetCreator
{
    [MenuItem("The Last Gift/Packing/Create Example Gift Assets")]
    public static void CreateExampleAssets()
    {
        const string folder = "Assets/Gifts";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Gifts");

        CreateGift(folder, "Bear", new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }, new Color(0.75f,0.48f,0.3f), true);
        CreateGift(folder, "Candle", new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2) }, new Color(0.95f,0.83f,0.38f), false);
        CreateGift(folder, "CandyCane", new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1) }, new Color(0.9f,0.3f,0.35f), true);
        CreateGift(folder, "SnowGlobe", new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }, new Color(0.4f,0.7f,0.9f), true);
        CreateGift(folder, "Cookie", new[] { new Vector2Int(0,0), new Vector2Int(1,0) }, new Color(0.7f,0.48f,0.25f), true);
        CreateGift(folder, "Ornament", new[] { Vector2Int.zero }, new Color(0.85f,0.3f,0.55f), true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Example GiftDefinition assets created in Assets/Gifts.");
    }

    private static void CreateGift(string folder, string fileName, Vector2Int[] shape, Color tint, bool rotatable)
    {
        var path = folder + "/" + fileName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<GiftDefinition>(path) != null) return;
        var gift = ScriptableObject.CreateInstance<GiftDefinition>();
        var serialized = new SerializedObject(gift);
        serialized.FindProperty("giftName").stringValue = fileName;
        serialized.FindProperty("cells").arraySize = shape.Length;
        for (var i = 0; i < shape.Length; i++)
        {
            var element = serialized.FindProperty("cells").GetArrayElementAtIndex(i);
            element.vector2IntValue = shape[i];
        }
        serialized.FindProperty("tint").colorValue = tint;
        serialized.FindProperty("canRotate").boolValue = rotatable;
        serialized.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(gift, path);
    }
}
#endif
