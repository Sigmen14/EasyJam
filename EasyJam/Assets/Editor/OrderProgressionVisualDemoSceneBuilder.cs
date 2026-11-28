#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates a separate scene for visually exploring order progression.
/// The existing SampleScene and build settings are left untouched.
/// </summary>
public static class OrderProgressionVisualDemoSceneBuilder
{
    private const string CampaignAssetPath = "Assets/Data/Orders/ChristmasCampaign.asset";
    private const string DemoScenePath = "Assets/Scenes/OrderProgressionDemo.unity";

    [MenuItem("Tools/The Last Gift/Create / Open Visual Progression Demo")]
    private static void CreateOrOpenDemoScene()
    {
        OrderCampaignDefinition campaign =
            AssetDatabase.LoadAssetAtPath<OrderCampaignDefinition>(CampaignAssetPath);

        if (campaign == null)
        {
            EditorUtility.DisplayDialog("Visual Order Demo",
                "Campaign asset was not found at: " + CampaignAssetPath, "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("Order Progression Visual Demo");

        OrderProgressionManager manager = root.AddComponent<OrderProgressionManager>();
        SerializedObject managerData = new SerializedObject(manager);
        managerData.FindProperty("campaign").objectReferenceValue = campaign;
        managerData.FindProperty("startingCoins").intValue = 0;
        managerData.ApplyModifiedPropertiesWithoutUndo();

        OrderProgressionVisualDemo visualDemo = root.AddComponent<OrderProgressionVisualDemo>();
        SerializedObject demoData = new SerializedObject(visualDemo);
        demoData.FindProperty("progression").objectReferenceValue = manager;
        demoData.FindProperty("campaign").objectReferenceValue = campaign;
        demoData.ApplyModifiedPropertiesWithoutUndo();

        if (!EditorSceneManager.SaveScene(scene, DemoScenePath))
        {
            EditorUtility.DisplayDialog("Visual Order Demo",
                "Could not save the demo scene to: " + DemoScenePath, "OK");
            return;
        }

        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        EditorUtility.DisplayDialog("Visual Order Demo Created",
            "The separate demo scene is ready.\n\nPress Play and use the two packing-result buttons to progress through all 9 orders.\n\nThe original SampleScene was not changed.", "OK");
    }
}
#endif
