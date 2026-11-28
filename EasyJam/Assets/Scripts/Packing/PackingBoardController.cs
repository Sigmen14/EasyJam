using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private Color emptyCellColor = new Color(0.88f, 0.9f, 0.95f, 1f);
        [SerializeField] private Color boardBackgroundColor = new Color(0.18f, 0.24f, 0.3f, 1f);

        private readonly List<GiftPieceView> pieces = new List<GiftPieceView>();
        private readonly List<GameObject> boardVisuals = new List<GameObject>();
        private Sprite cellSprite;
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
        private GUIStyle hudStyle;
        private GUIStyle controlStyle;
        private GUIStyle completionTitleStyle;
        private GUIStyle completionBodyStyle;
        private GUIStyle completionButtonStyle;

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<GiftPieceView> Gifts => pieces;

        private void Start()
        {
            EnsureCamera();
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

            if (targetCamera != null && targetCamera.isActiveAndEnabled) return;

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
            targetCamera.backgroundColor = boardBackgroundColor;
            targetCamera.cullingMask = ~0;
            targetCamera.depth = 0f;

            Debug.LogWarning("[Packing] No active camera was found. Created Packing Runtime Camera.", this);
        }

        private void Update()
        {
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

            var cells = CountPlacedCells();
            var placed = CountPlacedPieces();
            GUI.Label(new Rect(4f, 2f, Screen.width - 8f, 22f),
                $"Ячейки: {cells}/{width * height}    Фрагменты: {placed}/{pieces.Count}", hudStyle);
            GUI.Label(new Rect(4f, 25f, Screen.width - 8f, 22f),
                "ПОВОРОТ НА 90°: R или ПРОБЕЛ (также правая кнопка мыши)", controlStyle);
            GUI.Label(new Rect(4f, 48f, Screen.width - 8f, 22f),
                "Вынуть фигуру: перетащите её за пределы коробки", controlStyle);
            GUI.Label(new Rect(4f, 71f, Screen.width - 8f, 22f),
                "V — проверить заполнение вручную", controlStyle);

            if (showValidationMessage && !puzzleCompleted)
                GUI.Label(new Rect(4f, 94f, Screen.width - 8f, 24f), feedbackMessage, hudStyle);

            if (puzzleCompleted)
                DrawCompletionOverlay();
        }

        private void EnsureGuiStyles()
        {
            if (hudStyle != null) return;

            hudStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 30, 12, 17),
                fontStyle = FontStyle.Bold
            };
            hudStyle.normal.textColor = Color.white;

            controlStyle = new GUIStyle(hudStyle)
            {
                fontSize = Mathf.Clamp(Screen.height / 42, 10, 14),
                fontStyle = FontStyle.Normal
            };
            controlStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);

            completionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 13, 24, 42),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            completionTitleStyle.normal.textColor = new Color(1f, 0.88f, 0.36f, 1f);

            completionBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 32, 13, 19),
                wordWrap = true
            };
            completionBodyStyle.normal.textColor = Color.white;

            completionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Clamp(Screen.height / 34, 14, 20),
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawCompletionOverlay()
        {
            var panelWidth = Mathf.Min(560f, Screen.width - 32f);
            var panelHeight = 210f;
            var panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.color = new Color(0.035f, 0.055f, 0.08f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 12f, panel.y + 16f, panel.width - 24f, 58f),
                "КОРОБКА ЗАПОЛНЕНА!", completionTitleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 78f, panel.width - 48f, 48f),
                "Отличная работа! Все фрагменты уложены без пустых клеток и наложений.",
                completionBodyStyle);

            var button = new Rect(panel.x + panel.width * 0.2f, panel.y + 145f,
                panel.width * 0.6f, 44f);
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
            var fill = new Color32(255, 255, 255, 255);
            var border = new Color32(90, 100, 110, 255);
            var data = new Color32[pixels * pixels];
            for (var y = 0; y < pixels; y++)
            for (var x = 0; x < pixels; x++)
                data[y * pixels + x] = (x < 2 || y < 2 || x >= pixels - 2 || y >= pixels - 2) ? border : fill;
            texture.SetPixels32(data);
            texture.Apply();
            cellSprite = Sprite.Create(texture, new Rect(0, 0, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
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
