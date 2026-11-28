#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastGift.UI;

namespace TheLastGift.Editor
{
    public static class LastGiftUIPrefabFactory
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string UiPrefabFolder = "Assets/Prefabs/UI";
        private const string UiPrefabPath = "Assets/Prefabs/UI/LastGiftUI.prefab";

        [MenuItem("The Last Gift/UI/Create UI Root Prefab")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (!AssetDatabase.IsValidFolder(UiPrefabFolder))
                AssetDatabase.CreateFolder(PrefabFolder, "UI");

            GameObject prefabSource = new GameObject(
                "LastGiftUI",
                typeof(RectTransform),
                typeof(LastGiftUIController),
                typeof(OrderProgressionManager),
                typeof(OrderProgressionUIAdapter));

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabSource, UiPrefabPath);
            Object.DestroyImmediate(prefabSource);
            AssetDatabase.SaveAssets();

            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "The Last Gift UI",
                    "Не удалось создать префаб интерфейса. Проверь Console.",
                    "OK");
                return;
            }

            // The old command only saved the prefab asset. It did not put anything
            // into the open scene, so pressing Play could show an empty Game view.
            if (Object.FindObjectOfType<LastGiftUIController>() == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                Undo.RegisterCreatedObjectUndo(instance, "Add The Last Gift UI to Scene");
            }

            EnsureSceneCamera();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "The Last Gift UI",
                "Готово! Префаб создан и добавлен в открытую сцену вместе с камерой (если её не было).\n\nСохрани сцену через Ctrl+S и нажми Play.",
                "OK");
        }

        private static void EnsureSceneCamera()
        {
            Camera sceneCamera = Object.FindObjectOfType<Camera>();
            if (sceneCamera != null)
            {
                sceneCamera.enabled = true;
                return;
            }

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(38, 29, 25, 255);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.depth = -10f;
            camera.enabled = true;

            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera for The Last Gift");
        }
    }
}
#endif
