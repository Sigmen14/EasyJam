#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Inspector controls for the visual progression demo.
/// </summary>
[CustomEditor(typeof(OrderProgressionVisualDemo))]
public sealed class OrderProgressionVisualDemoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Измените масштаб текста, шрифт или разрешение выше, затем перестройте интерфейс. " +
            "Все элементы интерфейса находятся дочерними объектами в Hierarchy.",
            MessageType.Info);

        if (GUILayout.Button("Перестроить интерфейс"))
        {
            OrderProgressionVisualDemo demo = (OrderProgressionVisualDemo)target;
            demo.RebuildInterfaceForEditor();
            EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);
            Selection.activeGameObject = demo.gameObject;
        }
    }
}
#endif
