using System.Collections.Generic;
using UnityEngine;

namespace TheLastGift.Packing
{
    /// <summary>Runtime visuals and grid footprint state for one draggable gift.</summary>
    public sealed class GiftPieceView : MonoBehaviour
    {
        public GiftDefinition Definition { get; private set; }
        public Vector2Int GridPosition { get; private set; }
        public int RotationQuarterTurns { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool IsDragging { get; set; }
        public Vector2 TrayPosition { get; private set; }

        private readonly List<SpriteRenderer> cellRenderers = new List<SpriteRenderer>();
        private SpriteRenderer iconRenderer;
        private PackingBoardController board;
        private int sortingOrder;

        internal void Initialize(PackingBoardController owner, GiftDefinition definition, Vector2 trayPosition, float cellSize, Sprite cellSprite, int renderOrder)
        {
            board = owner;
            Definition = definition;
            TrayPosition = trayPosition;
            sortingOrder = renderOrder;
            transform.position = trayPosition;
            BuildVisuals(cellSprite, cellSize);
        }

        private void BuildVisuals(Sprite cellSprite, float cellSize)
        {
            var cells = Definition.Cells;
            if (cells == null || cells.Length == 0) cells = new[] { Vector2Int.zero };
            var minX = int.MaxValue; var minY = int.MaxValue; var maxX = int.MinValue; var maxY = int.MinValue;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x); minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x); maxY = Mathf.Max(maxY, cell.y);
            }
            var width = maxX - minX + 1; var height = maxY - minY + 1;
            foreach (var cell in cells)
            {
                var child = new GameObject("GiftCell");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = new Vector3((cell.x - minX - (width - 1) * 0.5f) * cellSize, (cell.y - minY - (height - 1) * 0.5f) * cellSize, 0f);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = cellSprite;
                renderer.color = Definition.Tint;
                renderer.sortingOrder = sortingOrder;
                renderer.drawMode = SpriteDrawMode.Simple;
                child.transform.localScale = Vector3.one * (cellSize * 0.92f);
                cellRenderers.Add(renderer);
            }

            if (Definition.Sprite != null)
            {
                var iconObject = new GameObject("GiftIcon");
                iconObject.transform.SetParent(transform, false);
                iconObject.transform.localPosition = Vector3.zero;
                iconRenderer = iconObject.AddComponent<SpriteRenderer>();
                iconRenderer.sprite = Definition.Sprite;
                iconRenderer.color = Color.white;
                iconRenderer.sortingOrder = sortingOrder + 1;
                iconObject.transform.localScale = Vector3.one * cellSize * Mathf.Min(1.6f, Mathf.Max(width, height) * 0.42f);
            }
        }

        internal void SetPlacement(Vector2Int anchor, int quarterTurns, bool placed, Vector3 worldCenter)
        {
            GridPosition = anchor;
            RotationQuarterTurns = quarterTurns;
            IsPlaced = placed;
            transform.position = worldCenter;
            transform.rotation = Quaternion.Euler(0f, 0f, -90f * quarterTurns);
        }

        internal void SetTint(Color color)
        {
            foreach (var renderer in cellRenderers) renderer.color = color;
        }

        internal void SetSortingOrder(int order)
        {
            foreach (var renderer in cellRenderers) renderer.sortingOrder = order;
            if (iconRenderer != null) iconRenderer.sortingOrder = order + 1;
        }

        public Vector2Int[] GetRotatedCells()
        {
            var source = Definition.Cells;
            if (source == null || source.Length == 0) return new[] { Vector2Int.zero };
            var rotated = new Vector2Int[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var cell = source[i];
                for (var turn = 0; turn < RotationQuarterTurns; turn++)
                    cell = new Vector2Int(cell.y, -cell.x);
                rotated[i] = cell;
            }
            // Normalize to a bottom-left footprint after rotation.
            var minX = int.MaxValue; var minY = int.MaxValue;
            foreach (var cell in rotated) { minX = Mathf.Min(minX, cell.x); minY = Mathf.Min(minY, cell.y); }
            for (var i = 0; i < rotated.Length; i++) rotated[i] -= new Vector2Int(minX, minY);
            return rotated;
        }

        public Vector2Int GetFootprintSize()
        {
            var cells = GetRotatedCells();
            var maxX = 0; var maxY = 0;
            foreach (var cell in cells) { maxX = Mathf.Max(maxX, cell.x); maxY = Mathf.Max(maxY, cell.y); }
            return new Vector2Int(maxX + 1, maxY + 1);
        }

        internal void RestoreToTray()
        {
            IsPlaced = false;
            GridPosition = new Vector2Int(-1, -1);
            transform.rotation = Quaternion.identity;
            transform.position = TrayPosition;
        }

        internal void SetPreview(bool valid)
        {
            SetTint(valid ? new Color(0.35f, 0.9f, 0.48f, 1f) : new Color(1f, 0.32f, 0.3f, 1f));
        }

        internal void SetDefaultTint() => SetTint(Definition.Tint);
    }
}
