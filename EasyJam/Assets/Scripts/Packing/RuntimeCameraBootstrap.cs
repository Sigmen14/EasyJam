using UnityEngine;

namespace TheLastGift.Packing
{
    /// <summary>
    /// Safety net for prototype scenes: guarantees that a camera exists after a scene loads.
    /// This does not depend on a scene GameObject or PackingBoardController being present.
    /// </summary>
    public static class RuntimeCameraBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAtLeastOneCamera()
        {
            if (Camera.allCamerasCount > 0)
                return;

            var cameraObject = new GameObject("Emergency Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = true;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.24f, 0.3f, 1f);
            camera.cullingMask = ~0;
            camera.depth = 100f;
            camera.targetDisplay = 0;

            Debug.LogWarning(
                "[Packing] No active cameras were found after scene load. An Emergency Main Camera was created.");
        }
    }
}
