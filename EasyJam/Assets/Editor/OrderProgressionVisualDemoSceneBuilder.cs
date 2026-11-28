#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates or repairs a separate scene for visually exploring order progression.
/// The existing SampleScene and build settings are left untouched.
/// </summary>
public static class OrderProgressionVisualDemoSceneBuilder
{
    private const string CampaignAssetPath = "Assets/Data/Orders/ChristmasCampaign.asset";
    private const string DemoScenePath = "Assets/Scenes/OrderProgressionDemo.unity";
    private const string RootObjectName = "Order Progression Visual Demo";

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

        Scene scene;
        SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
        if (existingScene != null)
        {
            scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        GameObject root = GameObject.Find(RootObjectName);
        if (root == null)
            root = new GameObject(RootObjectName);

        // Keep Awake from running until all serialized references have been assigned.
        root.SetActive(false);

        OrderProgressionManager manager = root.GetComponent<OrderProgressionManager>();
        if (manager == null)
            manager = root.AddComponent<OrderProgressionManager>();

        SetObjectReference(manager, "campaign", campaign);
        SetInt(manager, "startingCoins", 0);

        OrderProgressionVisualDemo visualDemo = root.GetComponent<OrderProgressionVisualDemo>();
        if (visualDemo == null)
            visualDemo = root.AddComponent<OrderProgressionVisualDemo>();

        SetObjectReference(visualDemo, "progression", manager);
        SetObjectReference(visualDemo, "campaign", campaign);
        visualDemo.enabled = true;

        root.SetActive(true);

        if (!EditorSceneManager.SaveScene(scene, DemoScenePath))
        {
            EditorUtility.DisplayDialog("Visual Order Demo",
                "Could not save the demo scene to: " + DemoScenePath, "OK");
            return;
        }

        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        EditorUtility.DisplayDialog("Visual Order Demo Ready",
            "The separate demo scene has been created or repaired.\n\nPress Play and use the packing-result buttons to progress through all 9 orders.\n\nThe original SampleScene was not changed.", "OK");
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError("Visual demo setup: serialized field not found: " + propertyName, target);
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.UpdateIfRequiredOrScript();
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError("Visual demo setup: serialized field not found: " + propertyName, target);
            return;
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.UpdateIfRequiredOrScript();
    }
}
#endif
