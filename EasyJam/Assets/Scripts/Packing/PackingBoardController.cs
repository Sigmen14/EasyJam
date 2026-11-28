using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastGift.UI;

namespace TheLastGift.Packing
{
    /// <summary>Creates a packing grid, gift tray, and handles drag/drop and validation.</summary>
    public class PackingBoardController : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField, Min(1)] private int defaultWidth = 4;
        [SerializeField, Min(1)] private int defaultHeight = 4;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField] private Vector2 boardCenter = Vector2.zero;
        [SerializeField] private float trayGap = 0.65f;
        [SerializeField] private Camera targetCamera;

        [Header("Prototype")]
        [SerializeField] private bool runDemoOnStart = true;
        [SerializeField] private Color emptyCellColor = new Color(0.96f, 0.89f, 0.75f, 1f);
        [SerializeField] private Color boardBackgroundColor = new Color(0.045f, 0.12f, 0.2f, 1f);

        private readonly List<GiftPieceView> pieces = new List<GiftPieceView>();
        private readonly List<GameObject> boardVisuals = new List<GameObject>();
        private readonly List<GameObject> christmasVisuals = new List<GameObject>();
        private readonly List<Transform> snowflakeTransforms = new List<Transform>();
        private readonly List<float> snowflakeSpeeds = new List<float>();
        private readonly List<float> snowflakePhases = new List<float>();
        private Sprite cellSprite;
        private Sprite snowflakeSprite;
        private Sprite pineTreeSprite;
        private bool christmasBackgroundBuilt;
        private float snowTop;
        private float snowBottom;
        private float snowHalfWidth;
        private GiftPieceView draggedPiece;
        private Vector2 dragOffset;
        private Vector2Int previewAnchor;
        private bool previewValid;
        private int width, height;
        private bool hasBeenConfigured;
        private bool puzzleCompleted;
        private bool showValidationMessage;
        private string feedbackMessage = "Перетащите фрагменты в коробку.";
        private Vector2Int dragStartGridPosition;
        private int dragStartRotation;
        private bool dragStartedPlaced;
        private GUIStyle hudTitleStyle;
        private GUIStyle hudStyle;
        private GUIStyle controlStyle;
        private GUIStyle completionKickerStyle;
        private GUIStyle completionTitleStyle;
        private GUIStyle completionSubtitleStyle;
        private GUIStyle completionBodyStyle;
        private GUIStyle completionButtonStyle;
        private GUIStyle returnToShopButtonStyle;
        private Texture2D hudPanelTexture;
        private Texture2D hudRibbonTexture;
        private Texture2D completionPanelTexture;
        private Texture2D completionRibbonTexture;
        private Texture2D completionButtonTexture;
        private Texture2D completionButtonHoverTexture;

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<GiftPieceView> Gifts => pieces;

        private void OnDestroy()
        {
            // These textures are generated for the IMGUI panels and are owned by this controller.
            if (hudPanelTexture != null) Destroy(hudPanelTexture);
            if (hudRibbonTexture != null) Destroy(hudRibbonTexture);
            if (completionPanelTexture != null) Destroy(completionPanelTexture);
            if (completionRibbonTexture != null) Destroy(completionRibbonTexture);
            if (completionButtonTexture != null) Destroy(completionButtonTexture);
            if (completionButtonHoverTexture != null) Destroy(completionButtonHoverTexture);
        }

        private void Start()
        {
            EnsureCamera();
            BuildChristmasBackground();
            if (!hasBeenConfigured)
            {
                if (runDemoOnStart) SetupBoard(defaultWidth, defaultHeight, CreateDemoGifts());
                else SetupBoard(defaultWidth, defaultHeight, new List<GiftDefinition>());
            }
        }


        private void EnsureCamera()
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null && mainCamera.isActiveAndEnabled)
                    targetCamera = mainCamera;
            }

            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = new Color(0.025f, 0.07f, 0.16f, 1f);
                return;
            }

            // Safety net for scenes opened without an enabled Main Camera.
            var cameraObject = new GameObject("Packing Runtime Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            targetCamera = cameraObject.AddComponent<Camera>();
            targetCamera.enabled = true;
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = 5f;
            targetCamera.nearClipPlane = 0.1f;
            targetCamera.farClipPlane = 1000f;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.025f, 0.07f, 0.16f, 1f);
            targetCamera.cullingMask = ~0;
            targetCamera.depth = 0f;

            Debug.LogWarning("[Packing] No active camera was found. Created Packing Runtime Camera.", this);
        }

        private void Update()
        {
            UpdateSnowfall();

            // Validation must work even on platforms without a mouse device.
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
                ValidateAndReport();

            // Once solved, keep the completed board visible until the player chooses
            // to start a new round from the completion panel.
            if (puzzleCompleted) return;

            var mouse = Mouse.current;
            if (mouse == null || targetCamera == null) return;

            var world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;

            if (mouse.leftButton.wasPressedThisFrame) BeginDrag(world);
            if (draggedPiece != null)
            {
                var dragPosition = (Vector2)world + dragOffset;
                draggedPiece.transform.position = new Vector3(dragPosition.x, dragPosition.y, 0f);
                UpdatePreview(dragPosition);

                if (mouse.rightButton.wasPressedThisFrame ||
                    Keyboard.current != null &&
                    (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                    RotateDraggedPiece();

                if (mouse.leftButton.wasReleasedThisFrame) EndDrag();
            }
        }

        private void OnGUI()
        {
            if (pieces.Count == 0 || width <= 0 || height <= 0) return;

            EnsureGuiStyles();
            DrawTopHud();

            if (puzzleCompleted)
                DrawCompletionOverlay();

            DrawReturnToShopButton();
        }

        private void EnsureGuiStyles()
        {
            if (hudStyle != null) return;

            var scale = Mathf.Clamp(Screen.height / 900f, 0.78f, 1.25f);

            hudTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(19f * scale),
                fontStyle = FontStyle.Bold
            };
            hudTitleStyle.normal.textColor = new Color(1f, 0.94f, 0.80f, 1f);

            hudStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(27f * scale),
                fontStyle = FontStyle.Bold
            };
            hudStyle.normal.textColor = new Color(1f, 0.79f, 0.37f, 1f);

            controlStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(15f * scale),
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };
            controlStyle.normal.textColor = new Color(0.97f, 0.92f, 0.82f, 1f);

            completionKickerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold
            };
            completionKickerStyle.normal.textColor = new Color(0.72f, 0.91f, 0.73f, 1f);

            completionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(39f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            completionTitleStyle.normal.textColor = new Color(1f, 0.84f, 0.43f, 1f);

            completionSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(24f * scale),
                fontStyle = FontStyle.Bold
            };
            completionSubtitleStyle.normal.textColor = new Color(1f, 0.94f, 0.80f, 1f);

            completionBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(18f * scale),
                wordWrap = true
            };
            completionBodyStyle.normal.textColor = new Color(0.94f, 0.88f, 0.77f, 1f);

            completionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(18, 18, 12, 12),
                padding = new RectOffset(16, 16, 8, 8)
            };
            completionButtonStyle.normal.textColor = new Color(0.055f, 0.16f, 0.10f, 1f);
            completionButtonStyle.hover.textColor = new Color(0.035f, 0.12f, 0.08f, 1f);
            completionButtonStyle.active.textColor = Color.black;
            completionButtonStyle.normal.background = completionButtonTexture =
                CreateRoundedUiTexture(512, 96, 22, 4,
                    new Color(1f, 0.88f, 0.57f, 1f),
                    new Color(0.91f, 0.62f, 0.23f, 1f),
                    new Color(1f, 0.96f, 0.78f, 1f));
            completionButtonStyle.hover.background = completionButtonHoverTexture =
                CreateRoundedUiTexture(512, 96, 22, 4,
                    new Color(1f, 0.96f, 0.72f, 1f),
                    new Color(1f, 0.76f, 0.32f, 1f),
                    new Color(1f, 1f, 0.9f, 1f));
            completionButtonStyle.active.background = completionButtonTexture;

            hudPanelTexture = CreateRoundedUiTexture(512, 160, 26, 4,
                new Color(0.075f, 0.24f, 0.15f, 0.97f),
                new Color(0.018f, 0.075f, 0.052f, 0.98f),
                new Color(0.88f, 0.68f, 0.34f, 1f));
            hudRibbonTexture = CreateRoundedUiTexture(512, 84, 20, 4,
                new Color(0.67f, 0.16f, 0.15f, 1f),
                new Color(0.43f, 0.055f, 0.065f, 1f),
                new Color(0.93f, 0.73f, 0.39f, 1f));

            completionPanelTexture = CreateRoundedUiTexture(640, 360, 34, 6,
                new Color(0.085f, 0.28f, 0.18f, 0.99f),
                new Color(0.018f, 0.075f, 0.052f, 0.99f),
                new Color(0.93f, 0.72f, 0.35f, 1f));
            completionRibbonTexture = CreateRoundedUiTexture(512, 104, 22, 4,
                new Color(0.67f, 0.16f, 0.15f, 1f),
                new Color(0.43f, 0.055f, 0.065f, 1f),
                new Color(0.93f, 0.73f, 0.39f, 1f));
        }

        private Texture2D CreateRoundedUiTexture(int width, int height, int radius,
            int borderThickness, Color topColor, Color bottomColor, Color borderColor)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "PackingUiPanel";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;

            var pixels = new Color32[width * height];
            var innerWidth = width - borderThickness * 2;
            var innerHeight = height - borderThickness * 2;
            var innerRadius = Mathf.Max(1, radius - borderThickness);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!IsInsideRoundedRect(x, y, width, height, radius)) continue;

                var isInner = IsInsideRoundedRect(
                    x - borderThickness, y - borderThickness,
                    innerWidth, innerHeight, innerRadius);
                var vertical = y / (float)(height - 1);
                var fill = Color.Lerp(bottomColor, topColor, vertical);
                pixels[y * width + x] = isInner ? fill : borderColor;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;

            var nearestX = Mathf.Clamp(x, radius, width - radius - 1);
            var nearestY = Mathf.Clamp(y, radius, height - radius - 1);
            var dx = x - nearestX;
            var dy = y - nearestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private void DrawTopHud()
        {
            var scale = Mathf.Clamp(Screen.height / 900f, 0.78f, 1.25f);
            var panelWidth = Mathf.Min(960f, Screen.width - 24f);
            var panelHeight = 154f * scale;
            var panel = new Rect((Screen.width - panelWidth) * 0.5f, 10f, panelWidth, panelHeight);

            GUI.DrawTexture(panel, hudPanelTexture);
            var titleRibbon = new Rect(
                panel.x + panel.width * 0.16f,
                panel.y + 5f * scale,
                panel.width * 0.68f,
                34f * scale);
            GUI.DrawTexture(titleRibbon, hudRibbonTexture);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 7f, panel.width - 32f, 27f * scale),
                "РОЖДЕСТВЕНСКАЯ МАСТЕРСКАЯ ПОДАРКОВ", hudTitleStyle);

            var cells = CountPlacedCells();
            var placed = CountPlacedPieces();
            GUI.Label(new Rect(panel.x + 12f, panel.y + 35f * scale, panel.width - 24f, 38f * scale),
                $"ЯЧЕЙКИ  {cells}/{width * height}     •     ФИГУРЫ  {placed}/{pieces.Count}", hudStyle);

            GUI.Label(new Rect(panel.x + 18f, panel.y + 77f * scale, panel.width - 36f, 25f * scale),
                "ПОВОРОТ НА 90°:  R  /  ПРОБЕЛ  /  ПРАВАЯ КНОПКА МЫШИ", controlStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 103f * scale, panel.width - 36f, 25f * scale),
                "ВЫНУТЬ ФИГУРУ: ПЕРЕТАЩИ ЗА КРАЙ КОРОБКИ     •     V — ПРОВЕРКА", controlStyle);
        }

        private void DrawReturnToShopButton()
        {
            var scale = Mathf.Clamp(Screen.height / 900f, 0.78f, 1.25f);
            if (returnToShopButtonStyle == null)
            {
                returnToShopButtonStyle = new GUIStyle(completionButtonStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(6, 6, 2, 2),
                    border = new RectOffset(12, 12, 8, 8),
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };
            }

            returnToShopButtonStyle.fontSize = Mathf.RoundToInt(14f * scale);

            var buttonWidth = Mathf.Min(300f, Screen.width - 32f);
            var buttonRect = new Rect(
                Screen.width - buttonWidth - 16f,
                172f * scale,
                buttonWidth,
                44f * scale);

            if (GUI.Button(buttonRect, "ВЕРНУТЬСЯ В ЛАВКУ", returnToShopButtonStyle))
                LastGiftUIController.ReturnFromPackingMiniGame();
        }

        private void DrawCompletionOverlay()
        {
            GUI.color = new Color(0.006f, 0.025f, 0.016f, 0.76f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var scale = Mathf.Clamp(Screen.height / 900f, 0.78f, 1.2f);
            var panelWidth = Mathf.Min(660f, Screen.width - 36f);
            var panelHeight = 338f * scale;
            var panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            var shadow = new Rect(panel.x + 8f, panel.y + 10f, panel.width, panel.height);
            GUI.color = new Color(0f, 0f, 0f, 0.38f);
            GUI.DrawTexture(shadow, completionPanelTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(panel, completionPanelTexture);
            var completionRibbon = new Rect(
                panel.x + 46f * scale,
                panel.y + 16f * scale,
                panel.width - 92f * scale,
                58f * scale);
            GUI.DrawTexture(completionRibbon, completionRibbonTexture);

            if (snowflakeSprite != null)
            {
                GUI.color = new Color(0.7f, 0.9f, 1f, 0.88f);
                GUI.DrawTexture(new Rect(panel.x + 42f, panel.y + 36f, 34f, 34f), snowflakeSprite.texture);
                GUI.DrawTexture(new Rect(panel.x + panel.width - 76f, panel.y + 36f, 34f, 34f), snowflakeSprite.texture);
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(panel.x + 28f, panel.y + 28f * scale, panel.width - 56f, 32f * scale),
                "ПРАЗДНИЧНЫЙ УСПЕХ", completionKickerStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 76f * scale, panel.width - 48f, 58f * scale),
                "ПОЗДРАВЛЯЕМ!", completionTitleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 137f * scale, panel.width - 56f, 40f * scale),
                "КОРОБКА ЗАПОЛНЕНА", completionSubtitleStyle);
            GUI.Label(new Rect(panel.x + 42f, panel.y + 183f * scale, panel.width - 84f, 54f * scale),
                "Все подарки аккуратно уложены.\nНи одной пустой клеточки!", completionBodyStyle);

            var badge = new Rect(panel.x + panel.width * 0.24f, panel.y + 242f * scale,
                panel.width * 0.52f, 30f * scale);
            GUI.Label(badge, $"ИДЕАЛЬНАЯ УПАКОВКА  •  {width * height}/{width * height}", completionKickerStyle);

            var button = new Rect(panel.x + panel.width * 0.18f, panel.y + 282f * scale,
                panel.width * 0.64f, 44f * scale);
            if (GUI.Button(button, "СЫГРАТЬ ЕЩЁ РАЗ", completionButtonStyle))
                RestartPuzzle();
        }

        private void RestartPuzzle()
        {
            SetupBoard(width, height, CreateDemoGifts());
        }

        public void SetupBoard(int boardWidth, int boardHeight, List<GiftDefinition> gifts)
        {
            hasBeenConfigured = true;
            width = Mathf.Max(1, boardWidth);
            height = Mathf.Max(1, boardHeight);
            puzzleCompleted = false;
            showValidationMessage = false;
            feedbackMessage = "Перетащите все фрагменты в коробку.";
            ClearBoard();
            CreateCellSprite();
            BuildBoardVisuals();
            CreatePieces(gifts ?? new List<GiftDefinition>());
        }

        public bool TryValidate(out PackingResult result)
        {
            var placed = 0;
            var allPlaced = true;
            var valid = true;
            var occupied = new bool[width, height];

            foreach (var piece in pieces)
            {
                if (!piece.IsPlaced)
                {
                    allPlaced = false;
                    continue;
                }

                placed++;
                foreach (var cell in piece.GetRotatedCells())
                {
                    var x = piece.GridPosition.x + cell.x;
                    var y = piece.GridPosition.y + cell.y;
                    if (x < 0 || y < 0 || x >= width || y >= height || occupied[x, y])
                    {
                        valid = false;
                        continue;
                    }
                    occupied[x, y] = true;
                }
            }

            var filledCells = 0;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (occupied[x, y]) filledCells++;

            var emptyCells = width * height - filledCells;
            valid &= allPlaced && emptyCells == 0;

            var message = pieces.Count == 0 ? "Нет фрагментов для упаковки." :
                !allPlaced ? $"Размещено фрагментов: {placed}/{pieces.Count}." :
                !valid && filledCells < width * height && emptyCells > 0
                    ? $"Коробка заполнена не полностью: осталось пустых ячеек — {emptyCells}."
                    : !valid ? "Есть наложения или фрагмент выходит за границы коробки."
                    : "Все фрагменты помещены. Коробка заполнена!";

            result = new PackingResult(valid, allPlaced, placed, pieces.Count, message);
            return valid;
        }

        private void ValidateAndReport()
        {
            var wasCompleted = puzzleCompleted;
            var valid = TryValidate(out var result);
            puzzleCompleted = valid;
            feedbackMessage = result.Message;
            showValidationMessage = true;

            if (valid && !wasCompleted)
                Debug.Log($"[Packing] SUCCESS: {result.Message} ({result.PlacedCount}/{result.TotalCount})", this);
            else if (!valid)
                Debug.LogWarning($"[Packing] NOT READY: {result.Message} ({result.PlacedCount}/{result.TotalCount})", this);
        }

        private void RefreshProgress()
        {
            var wasCompleted = puzzleCompleted;
            var valid = TryValidate(out var result);
            puzzleCompleted = valid;

            // Keep the HUD quiet while the player is still arranging the pieces.
            showValidationMessage = valid || result.AllItemsPlaced;
            if (showValidationMessage) feedbackMessage = result.Message;

            if (valid && !wasCompleted)
                Debug.Log($"[Packing] SUCCESS: {result.Message}", this);
        }

        private int CountPlacedPieces()
        {
            var count = 0;
            foreach (var piece in pieces)
                if (piece != null && piece.IsPlaced) count++;
            return count;
        }

        private int CountPlacedCells()
        {
            var count = 0;
            foreach (var piece in pieces)
                if (piece != null && piece.IsPlaced)
                    count += piece.GetRotatedCells().Length;
            return count;
        }

        private void ClearBoard()
        {
            foreach (var visual in boardVisuals)
                if (visual != null) Destroy(visual);
            boardVisuals.Clear();

            foreach (var piece in pieces)
                if (piece != null) Destroy(piece.gameObject);
            pieces.Clear();
            draggedPiece = null;
        }

        private void CreateCellSprite()
        {
            if (cellSprite != null) return;
            const int pixels = 32;
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var fill = new Color32(250, 239, 215, 255);
            var border = new Color32(125, 82, 52, 255);
            var data = new Color32[pixels * pixels];
            for (var y = 0; y < pixels; y++)
            for (var x = 0; x < pixels; x++)
                data[y * pixels + x] = (x < 2 || y < 2 || x >= pixels - 2 || y >= pixels - 2) ? border : fill;
            texture.SetPixels32(data);
            texture.Apply();
            cellSprite = Sprite.Create(texture, new Rect(0, 0, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
        }

        private void BuildChristmasBackground()
        {
            if (christmasBackgroundBuilt || targetCamera == null || !targetCamera.orthographic) return;
            christmasBackgroundBuilt = true;

            var viewHeight = targetCamera.orthographicSize * 2f;
            var viewWidth = viewHeight * targetCamera.aspect;
            snowTop = targetCamera.transform.position.y + viewHeight * 0.5f + 0.2f;
            snowBottom = targetCamera.transform.position.y - viewHeight * 0.5f - 0.2f;
            snowHalfWidth = viewWidth * 0.5f;

            // Deep winter sky with a soft teal aurora instead of a flat brown fill.
            var skyTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            skyTexture.name = "WinterNightSkyTexture";
            skyTexture.filterMode = FilterMode.Bilinear;
            skyTexture.wrapMode = TextureWrapMode.Clamp;
            var skyPixels = new Color32[512 * 512];
            for (var y = 0; y < 512; y++)
            {
                var v = y / 511f;
                for (var x = 0; x < 512; x++)
                {
                    var u = x / 511f;
                    var baseColor = Color.Lerp(
                        new Color(0.018f, 0.055f, 0.14f, 1f),
                        new Color(0.035f, 0.20f, 0.25f, 1f), v);

                    var auroraLine = 0.58f + 0.075f * Mathf.Sin(u * 7f + 0.4f)
                                           + 0.035f * Mathf.Sin(u * 15f + 1.2f);
                    var aurora = Mathf.Exp(-Mathf.Pow((v - auroraLine) * 17f, 2f));
                    var glow = Mathf.Exp(-Mathf.Pow((v - 0.78f) * 5f, 2f))
                               * Mathf.Exp(-Mathf.Pow((u - 0.5f) * 2.4f, 2f));
                    baseColor += new Color(0.025f * aurora + 0.018f * glow,
                                           0.16f * aurora + 0.035f * glow,
                                           0.12f * aurora + 0.055f * glow, 0f);
                    skyPixels[y * 512 + x] = baseColor;
                }
            }
            skyTexture.SetPixels32(skyPixels);
            skyTexture.Apply(false, false);

            var skySprite = Sprite.Create(skyTexture, new Rect(0, 0, 512, 512),
                new Vector2(0.5f, 0.5f), 100f);
            CreateChristmasSprite("Winter Night Sky", skySprite,
                new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 9f),
                new Vector3(viewWidth / 5.12f, viewHeight / 5.12f, 1f), -100);

            snowflakeSprite = CreateSnowflakeSprite();
            pineTreeSprite = CreatePineTreeSprite();
            var snowBankSprite = CreateSnowBankSprite();

            CreateChristmasSprite("Snowy Ground", snowBankSprite,
                new Vector3(targetCamera.transform.position.x,
                    targetCamera.transform.position.y - viewHeight * 0.5f + 0.48f, 8.3f),
                new Vector3(viewWidth / 5.12f, 1f, 1f), -87);

            // Snowy fir trees frame the play area without covering the board or gifts.
            CreateChristmasSprite("Snowy Fir Left", pineTreeSprite,
                new Vector3(-viewWidth * 0.43f, -viewHeight * 0.30f, 8.5f),
                new Vector3(1.05f, 1.8f, 1f), -90);
            CreateChristmasSprite("Snowy Fir Right", pineTreeSprite,
                new Vector3(viewWidth * 0.43f, -viewHeight * 0.30f, 8.5f),
                new Vector3(1.05f, 1.8f, 1f), -90);
            CreateChristmasSprite("Small Fir Left", pineTreeSprite,
                new Vector3(-viewWidth * 0.34f, -viewHeight * 0.43f, 8.4f),
                new Vector3(0.62f, 1.12f, 1f), -89);
            CreateChristmasSprite("Small Fir Right", pineTreeSprite,
                new Vector3(viewWidth * 0.34f, -viewHeight * 0.43f, 8.4f),
                new Vector3(0.62f, 1.12f, 1f), -89);

            CreateHolidayGarland(viewWidth, viewHeight);
            CreateSnowfall(viewWidth, viewHeight, 34);
        }

        private Sprite CreateSnowflakeSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "SnowflakeTexture";
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var radius = Mathf.Sqrt(dx * dx + dy * dy);
                var onArm = Mathf.Abs(dx) < 1.35f || Mathf.Abs(dy) < 1.35f ||
                            Mathf.Abs(dx - dy) < 1.35f || Mathf.Abs(dx + dy) < 1.35f;
                if (radius < 13f && onArm)
                    pixels[y * size + x] = new Color32(226, 246, 255, 235);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreatePineTreeSprite()
        {
            const int width = 128;
            const int height = 192;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SnowCoveredPineTexture";
            var pixels = new Color32[width * height];

            FillPixelRect(pixels, width, height, 57, 0, 71, 33,
                new Color32(91, 56, 38, 255));
            FillTriangle(pixels, width, height, new Vector2(64, 112),
                new Vector2(6, 20), new Vector2(122, 20),
                new Color32(10, 76, 62, 255));
            FillTriangle(pixels, width, height, new Vector2(64, 151),
                new Vector2(18, 68), new Vector2(110, 68),
                new Color32(12, 91, 72, 255));
            FillTriangle(pixels, width, height, new Vector2(64, 184),
                new Vector2(32, 119), new Vector2(96, 119),
                new Color32(15, 108, 83, 255));

            // Soft snow caps on each tier.
            FillTriangle(pixels, width, height, new Vector2(64, 181),
                new Vector2(52, 157), new Vector2(76, 157),
                new Color32(221, 241, 249, 255));
            FillTriangle(pixels, width, height, new Vector2(64, 146),
                new Vector2(43, 111), new Vector2(85, 111),
                new Color32(205, 232, 243, 255));
            FillTriangle(pixels, width, height, new Vector2(64, 106),
                new Vector2(31, 57), new Vector2(97, 57),
                new Color32(213, 237, 247, 255));

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateSnowBankSprite()
        {
            const int width = 512;
            const int height = 96;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SnowyGroundTexture";
            var pixels = new Color32[width * height];

            for (var x = 0; x < width; x++)
            {
                var edge = 19f + 5f * Mathf.Sin(x * 0.018f) + 3f * Mathf.Sin(x * 0.043f + 1.1f);
                for (var y = 0; y < height; y++)
                {
                    if (y > edge) continue;
                    var highlight = y > edge - 4f;
                    pixels[y * width + x] = highlight
                        ? new Color32(239, 249, 255, 255)
                        : new Color32(184, 218, 237, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private void FillPixelRect(Color32[] pixels, int width, int height,
            int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            for (var y = Mathf.Max(0, yMin); y < Mathf.Min(height, yMax); y++)
            for (var x = Mathf.Max(0, xMin); x < Mathf.Min(width, xMax); x++)
                pixels[y * width + x] = color;
        }

        private void FillTriangle(Color32[] pixels, int width, int height,
            Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            var minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, width - 1);
            var maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, width - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, height - 1);
            var maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, height - 1);

            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
                if (PointInTriangle(new Vector2(x, y), a, b, c))
                    pixels[y * width + x] = color;
        }

        private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = (p.x - b.x) * (a.y - b.y) + (a.x - b.x) * (b.y - p.y);
            var d2 = (p.x - c.x) * (b.y - c.y) + (b.x - c.x) * (c.y - p.y);
            var d3 = (p.x - a.x) * (c.y - a.y) + (c.x - a.x) * (a.y - p.y);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private GameObject CreateChristmasSprite(string objectName, Sprite sprite,
            Vector3 position, Vector3 scale, int sortingOrder)
        {
            var obj = new GameObject(objectName);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            christmasVisuals.Add(obj);
            return obj;
        }

        private void CreateHolidayGarland(float viewWidth, float viewHeight)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            var wireObject = new GameObject("Christmas Garland Wire");
            var line = wireObject.AddComponent<LineRenderer>();
            line.material = new Material(shader);
            line.useWorldSpace = true;
            line.positionCount = 15;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.startColor = new Color(0.05f, 0.28f, 0.22f, 1f);
            line.endColor = line.startColor;
            line.sortingOrder = -86;
            line.numCapVertices = 3;

            var top = targetCamera.transform.position.y + viewHeight * 0.5f - 0.42f;
            var left = -viewWidth * 0.46f;
            var bulbSprite = CreateSoftCircleSprite();
            var bulbColors = new[]
            {
                new Color(1f, 0.28f, 0.27f, 1f),
                new Color(1f, 0.78f, 0.28f, 1f),
                new Color(0.28f, 0.9f, 0.7f, 1f),
                new Color(0.55f, 0.72f, 1f, 1f)
            };

            for (var i = 0; i < 15; i++)
            {
                var t = i / 14f;
                var x = left + viewWidth * 0.92f * t;
                var y = top - 0.17f * Mathf.Sin(t * Mathf.PI);
                var point = new Vector3(x, y, 8.2f);
                line.SetPosition(i, point);

                var bulb = CreateChristmasSprite("Garland Light", bulbSprite,
                    point + new Vector3(0f, -0.07f, -0.05f),
                    Vector3.one * 0.42f, -85);
                bulb.GetComponent<SpriteRenderer>().color = bulbColors[i % bulbColors.Length];
            }

            christmasVisuals.Add(wireObject);
        }

        private Sprite CreateSoftCircleSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "HolidayLightTexture";
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                var alpha = Mathf.Clamp01((14f - distance) / 3f);
                if (distance <= 14f)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private void CreateSnowfall(float viewWidth, float viewHeight, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var x = targetCamera.transform.position.x + Random.Range(-viewWidth * 0.48f, viewWidth * 0.48f);
                var y = targetCamera.transform.position.y + Random.Range(-viewHeight * 0.48f, viewHeight * 0.48f);
                var scale = Random.Range(0.035f, 0.09f);
                var flake = CreateChristmasSprite("Falling Snow", snowflakeSprite,
                    new Vector3(x, y, 8f), Vector3.one * scale, -80);
                flake.GetComponent<SpriteRenderer>().color =
                    new Color(0.82f, 0.94f, 1f, Random.Range(0.35f, 0.85f));

                snowflakeTransforms.Add(flake.transform);
                snowflakeSpeeds.Add(Random.Range(0.16f, 0.42f));
                snowflakePhases.Add(Random.Range(0f, Mathf.PI * 2f));
            }
        }

        private void UpdateSnowfall()
        {
            if (snowflakeTransforms.Count == 0) return;

            for (var i = 0; i < snowflakeTransforms.Count; i++)
            {
                var flake = snowflakeTransforms[i];
                if (flake == null) continue;

                var position = flake.position;
                position.y -= snowflakeSpeeds[i] * Time.deltaTime;
                position.x += Mathf.Sin(Time.time * 0.7f + snowflakePhases[i]) * Time.deltaTime * 0.12f;

                if (position.y < snowBottom)
                {
                    position.y = snowTop;
                    position.x = targetCamera.transform.position.x
                                 + Random.Range(-snowHalfWidth * 0.95f, snowHalfWidth * 0.95f);
                }

                flake.position = position;
            }
        }

        private void BuildBoardVisuals()
        {
            var bottomLeft = GetBoardBottomLeft();
            var background = CreateSpriteObject("BoardBackground", boardBackgroundColor,
                new Vector3(boardCenter.x, boardCenter.y, 0.2f));
            background.transform.localScale = new Vector3(width * cellSize + 0.15f, height * cellSize + 0.15f, 1f);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var cell = CreateSpriteObject($"Cell_{x}_{y}", emptyCellColor,
                    new Vector3(bottomLeft.x + (x + 0.5f) * cellSize, bottomLeft.y + (y + 0.5f) * cellSize, 0f));
                cell.transform.localScale = Vector3.one * cellSize * 0.96f;
            }
        }

        private GameObject CreateSpriteObject(string objectName, Color color, Vector3 position)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(transform, false);
            obj.transform.position = position;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = cellSprite;
            renderer.color = color;
            renderer.sortingOrder = -10;
            boardVisuals.Add(obj);
            return obj;
        }

        private void CreatePieces(List<GiftDefinition> gifts)
        {
            for (var i = 0; i < gifts.Count; i++)
            {
                if (gifts[i] == null) continue;
                var obj = new GameObject(gifts[i].GiftName);
                obj.transform.SetParent(transform, false);
                var view = obj.AddComponent<GiftPieceView>();
                var trayPosition = GetTrayPosition(i, gifts[i]);
                view.Initialize(this, gifts[i], trayPosition, cellSize, cellSprite, i + 10);
                pieces.Add(view);
            }
        }

        private Vector2 GetTrayPosition(int index, GiftDefinition definition)
        {
            var cells = definition.Cells;
            if (cells == null || cells.Length == 0) cells = new[] { Vector2Int.zero };

            var minX = int.MaxValue;
            var maxX = int.MinValue;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
            }

            var pieceWidth = (maxX - minX + 1) * cellSize;
            var left = GetBoardBottomLeft().x;
            var right = left + width * cellSize;
            var side = index % 2;
            var row = index / 2;
            var sideGap = Mathf.Max(0.18f, trayGap * 0.4f);
            var x = side == 0
                ? left - pieceWidth * 0.5f - sideGap
                : right + pieceWidth * 0.5f + sideGap;
            var y = boardCenter.y + (row == 0 ? 1.3f : -1.3f) * cellSize;
            return new Vector2(x, y);
        }

        private void BeginDrag(Vector2 world)
        {
            var candidate = FindPieceAtWorld(world);
            if (candidate == null) return;

            draggedPiece = candidate;
            dragOffset = (Vector2)candidate.transform.position - world;
            dragStartedPlaced = candidate.IsPlaced;
            dragStartGridPosition = candidate.GridPosition;
            dragStartRotation = candidate.RotationQuarterTurns;

            candidate.IsDragging = true;
            candidate.SetSortingOrder(100);
            if (candidate.IsPlaced) RemovePlacement(candidate);
            candidate.SetDefaultTint();
            UpdatePreview(world + dragOffset);
        }

        private void EndDrag()
        {
            if (draggedPiece == null) return;

            var piece = draggedPiece;
            piece.IsDragging = false;

            // Dragging a placed piece beyond the box is the explicit "take it out" action.
            // Keep its current rotation so the player can immediately try another orientation.
            if (dragStartedPlaced && IsOutsideBoard(piece.transform.position))
            {
                ReturnPieceToTray(piece, piece.RotationQuarterTurns);
            }
            else if (previewValid)
            {
                PlacePiece(piece, previewAnchor);
                piece.SetDefaultTint();
            }
            else if (dragStartedPlaced)
            {
                // An invalid drop inside the box restores the exact previous placement.
                piece.SetPlacement(dragStartGridPosition, dragStartRotation, true,
                    AnchorToWorldCenter(dragStartGridPosition, piece));
                piece.SetDefaultTint();
            }
            else
            {
                // Pieces picked up from the tray return there after an invalid drop.
                ReturnPieceToTray(piece, dragStartRotation);
                piece.SetDefaultTint();
            }

            piece.SetSortingOrder(pieces.IndexOf(piece) + 10);
            draggedPiece = null;
            RefreshBoardColors();
            RefreshProgress();
        }

        private bool IsOutsideBoard(Vector2 worldPosition)
        {
            var bottomLeft = GetBoardBottomLeft();
            var topRight = bottomLeft + new Vector2(width * cellSize, height * cellSize);
            return worldPosition.x < bottomLeft.x || worldPosition.x > topRight.x ||
                   worldPosition.y < bottomLeft.y || worldPosition.y > topRight.y;
        }

        private void ReturnPieceToTray(GiftPieceView piece, int rotation)
        {
            var trayCenter = new Vector3(piece.TrayPosition.x, piece.TrayPosition.y, -0.1f);
            piece.SetPlacement(new Vector2Int(-1, -1), rotation, false, trayCenter);
        }

        private void UpdatePreview(Vector2 worldCenter)
        {
            previewAnchor = WorldToAnchor(worldCenter, draggedPiece);
            previewValid = CanPlace(draggedPiece, previewAnchor);
            draggedPiece.SetPreview(previewValid);
            RefreshBoardColors();
        }

        private Vector2Int WorldToAnchor(Vector2 center, GiftPieceView piece)
        {
            var size = piece.GetFootprintSize();
            var bottomLeft = GetBoardBottomLeft();
            var x = Mathf.RoundToInt((center.x - bottomLeft.x) / cellSize - size.x * 0.5f);
            var y = Mathf.RoundToInt((center.y - bottomLeft.y) / cellSize - size.y * 0.5f);
            return new Vector2Int(x, y);
        }

        private bool CanPlace(GiftPieceView piece, Vector2Int anchor)
        {
            var occupied = new bool[width, height];
            foreach (var other in pieces)
            {
                if (other == piece || !other.IsPlaced) continue;
                foreach (var cell in other.GetRotatedCells())
                {
                    var x = other.GridPosition.x + cell.x;
                    var y = other.GridPosition.y + cell.y;
                    if (x >= 0 && y >= 0 && x < width && y < height) occupied[x, y] = true;
                }
            }

            foreach (var cell in piece.GetRotatedCells())
            {
                var x = anchor.x + cell.x;
                var y = anchor.y + cell.y;
                if (x < 0 || y < 0 || x >= width || y >= height || occupied[x, y]) return false;
            }
            return true;
        }

        private void PlacePiece(GiftPieceView piece, Vector2Int anchor)
        {
            piece.SetPlacement(anchor, piece.RotationQuarterTurns, true, AnchorToWorldCenter(anchor, piece));
        }

        private Vector3 AnchorToWorldCenter(Vector2Int anchor, GiftPieceView piece)
        {
            var size = piece.GetFootprintSize();
            var bottomLeft = GetBoardBottomLeft();
            return new Vector3(
                bottomLeft.x + (anchor.x + size.x * 0.5f) * cellSize,
                bottomLeft.y + (anchor.y + size.y * 0.5f) * cellSize,
                -0.1f);
        }

        private void RemovePlacement(GiftPieceView piece)
        {
            piece.SetPlacement(piece.GridPosition, piece.RotationQuarterTurns, false, piece.transform.position);
        }

        private GiftPieceView FindPieceAtWorld(Vector2 world)
        {
            // Search from front to back and hit-test actual rendered cells, not the
            // empty corners of a piece's rectangular footprint.
            for (var i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                if (piece != null && piece.ContainsWorldPoint(world))
                    return piece;
            }
            return null;
        }

        private void RotateDraggedPiece()
        {
            if (draggedPiece == null || !draggedPiece.Definition.CanRotate) return;
            RotatePiece(draggedPiece);
            UpdatePreview(draggedPiece.transform.position);
        }

        private void RotatePiece(GiftPieceView piece)
        {
            if (!piece.Definition.CanRotate) return;
            var next = (piece.RotationQuarterTurns + 1) % 4;
            piece.SetPlacement(piece.GridPosition, next, piece.IsPlaced, piece.transform.position);
        }

        private void RefreshBoardColors()
        {
            if (boardVisuals.Count < width * height + 1) return;

            for (var i = 1; i < boardVisuals.Count; i++)
            {
                var cell = boardVisuals[i];
                if (cell == null) continue;

                var renderer = cell.GetComponent<SpriteRenderer>();
                var x = (i - 1) % width;
                var y = (i - 1) / width;
                var occupied = false;
                foreach (var piece in pieces)
                {
                    if (!piece.IsPlaced || piece == draggedPiece) continue;
                    foreach (var local in piece.GetRotatedCells())
                        if (piece.GridPosition + local == new Vector2Int(x, y)) occupied = true;
                }
                renderer.color = occupied ? new Color(0.6f, 0.72f, 0.8f, 1f) : emptyCellColor;
            }

            if (draggedPiece != null)
            {
                foreach (var local in draggedPiece.GetRotatedCells())
                {
                    var cellPos = previewAnchor + local;
                    if (cellPos.x < 0 || cellPos.y < 0 || cellPos.x >= width || cellPos.y >= height) continue;

                    var index = 1 + cellPos.y * width + cellPos.x;
                    var renderer = boardVisuals[index].GetComponent<SpriteRenderer>();
                    renderer.color = previewValid
                        ? new Color(0.3f, 0.85f, 0.45f, 0.6f)
                        : new Color(0.95f, 0.25f, 0.25f, 0.6f);
                }
            }
        }

        private Vector2 GetBoardBottomLeft() =>
            new Vector2(boardCenter.x - width * cellSize * 0.5f, boardCenter.y - height * cellSize * 0.5f);

        private List<GiftDefinition> CreateDemoGifts()
        {
            // Four tetrominoes have exactly 16 cells total, so a valid solution fills the 4x4 box.
            // Two T pieces, one L piece, and one S piece require rotation and spatial planning.
            return new List<GiftDefinition>
            {
                GiftDefinition.CreateRuntime("Tangerine T", new[]
                {
                    new Vector2Int(0,0), new Vector2Int(1,0),
                    new Vector2Int(2,0), new Vector2Int(1,1)
                }, new Color(0.78f, 0.43f, 0.27f)),
                GiftDefinition.CreateRuntime("Golden T", new[]
                {
                    new Vector2Int(0,0), new Vector2Int(1,0),
                    new Vector2Int(2,0), new Vector2Int(1,1)
                }, new Color(0.95f, 0.78f, 0.28f)),
                GiftDefinition.CreateRuntime("Red L", new[]
                {
                    new Vector2Int(0,0), new Vector2Int(0,1),
                    new Vector2Int(0,2), new Vector2Int(1,0)
                }, new Color(0.88f, 0.25f, 0.31f)),
                GiftDefinition.CreateRuntime("Pink S", new[]
                {
                    new Vector2Int(1,0), new Vector2Int(2,0),
                    new Vector2Int(0,1), new Vector2Int(1,1)
                }, new Color(0.82f, 0.25f, 0.52f))
            };
        }
    }
}
