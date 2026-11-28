using UnityEngine;

namespace TheLastGift.Packing
{
    /// <summary>
    /// Safety net for prototype scenes: guarantees that a camera and packing board
    /// exist after a scene loads. This does not depend on scene objects being present.
    /// </summary>
    public static class RuntimeCameraBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePrototypeIsVisible()
        {
            EnsureCamera();
            EnsurePackingBoard();
        }

        private static void EnsureCamera()
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

        private static void EnsurePackingBoard()
        {
            var existingBoard = Object.FindFirstObjectByType<PackingBoardController>();
            if (existingBoard != null)
                return;

            var boardObject = new GameObject("Packing Board (Auto Created)");
            boardObject.AddComponent<PackingBoardController>();

            Debug.LogWarning(
                "[Packing] No PackingBoardController was found in the loaded scene. A demo packing board was created.");
        }
    }
}
