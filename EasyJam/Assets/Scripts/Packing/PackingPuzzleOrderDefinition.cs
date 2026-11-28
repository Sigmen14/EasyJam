using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastGift.Packing
{
    public enum PackingWeightCategory { Normal, Fragile, Heavy }
    public enum PackingConstraintType { FragileAwayFromHeavy, KeepTogether, MustBeInCenter }
    public enum PackingConstraintState { Pending, Satisfied, Violated }

    [Serializable]
    public sealed class PackingConstraintDefinition
    {
        [SerializeField] private PackingConstraintType type;
        [SerializeField] private GiftDefinition primaryGift;
        [SerializeField] private GiftDefinition secondaryGift;

        public PackingConstraintType Type => type;
        public GiftDefinition PrimaryGift => primaryGift;
        public GiftDefinition SecondaryGift => secondaryGift;
        public string Icon => type == PackingConstraintType.FragileAwayFromHeavy ? "❄" :
            type == PackingConstraintType.KeepTogether ? "↔" : "✦";
        public string Description
        {
            get
            {
                switch (type)
                {
                    case PackingConstraintType.FragileAwayFromHeavy:
                        return "Хрупкие предметы не должны соприкасаться с тяжёлыми.";
                    case PackingConstraintType.KeepTogether:
                        return "Держи рядом: «" + primaryGift.GiftName + "» и «" + secondaryGift.GiftName + "».";
                    case PackingConstraintType.MustBeInCenter:
                        return "Положи «" + primaryGift.GiftName + "» в центральные 4 клетки коробки.";
                    default: return "Выполни особое условие заказа.";
                }
            }
        }

        internal string GetFailureMessage(GiftDefinition first, GiftDefinition second = null)
        {
            switch (type)
            {
                case PackingConstraintType.FragileAwayFromHeavy:
                    return "Раздвинь предметы: «" + first.GiftName + "» и «" + second.GiftName +
                        "» не должны соприкасаться, даже по диагонали.";
                case PackingConstraintType.KeepTogether:
                    return "Сблизь «" + primaryGift.GiftName + "» и «" + secondaryGift.GiftName +
                        "» — они должны касаться друг друга.";
                case PackingConstraintType.MustBeInCenter:
                    return "Перемести «" + primaryGift.GiftName + "» в центральные 4 клетки коробки.";
                default: return "Исправь расположение подарков согласно условию заказа.";
            }
        }
    }

    [CreateAssetMenu(menuName = "The Last Gift/Packing/Puzzle Order", fileName = "PackingPuzzleOrder")]
    public sealed class PackingPuzzleOrderDefinition : ScriptableObject
    {
        [SerializeField] private int orderNumber = 1;
        [SerializeField] private string orderTitle = "Рождественский заказ";
        [SerializeField, TextArea(2, 4)] private string customerWish;
        [SerializeField] private List<GiftDefinition> gifts = new List<GiftDefinition>();
        [SerializeField] private List<PackingConstraintDefinition> constraints =
            new List<PackingConstraintDefinition>();

        public int OrderNumber => orderNumber;
        public string OrderTitle => orderTitle;
        public string CustomerWish => customerWish;
        public IReadOnlyList<GiftDefinition> Gifts => gifts;
        public IReadOnlyList<PackingConstraintDefinition> Constraints => constraints;

        public bool TryValidateConfiguration(int boardWidth, int boardHeight, out string error)
        {
            error = null;
            if (boardWidth != 4 || boardHeight != 4)
            { error = "Заказы кампании рассчитаны на коробку 4×4."; return false; }
            if (gifts == null || gifts.Count == 0)
            { error = "В заказе нет подарков."; return false; }
            if (constraints == null || constraints.Count > 2)
            { error = "В одном заказе должно быть не больше двух особых условий."; return false; }

            var uniqueGifts = new HashSet<GiftDefinition>();
            var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalCells = 0;
            for (int i = 0; i < gifts.Count; i++)
            {
                GiftDefinition gift = gifts[i];
                if (gift == null || !uniqueGifts.Add(gift))
                { error = "В заказе есть пустой или повторяющийся предмет."; return false; }
                if (string.IsNullOrWhiteSpace(gift.GiftName) || !uniqueNames.Add(gift.GiftName))
                { error = "У каждого предмета в заказе должно быть уникальное имя."; return false; }
                if (gift.Cells == null || gift.Cells.Length == 0)
                { error = "У предмета «" + gift.GiftName + "» не задана форма."; return false; }
                var shapeCells = new HashSet<Vector2Int>();
                for (int c = 0; c < gift.Cells.Length; c++)
                    if (!shapeCells.Add(gift.Cells[c]))
                    { error = "Форма предмета «" + gift.GiftName + "» содержит повторяющуюся клетку."; return false; }
                totalCells += gift.Cells.Length;
            }
            if (totalCells != 16)
            { error = "Площадь предметов должна точно заполнять коробку 4×4 (16 клеток)."; return false; }

            for (int i = 0; i < constraints.Count; i++)
            {
                PackingConstraintDefinition rule = constraints[i];
                if (rule == null)
                { error = "В заказе найдено пустое условие."; return false; }
                switch (rule.Type)
                {
                    case PackingConstraintType.FragileAwayFromHeavy:
                        bool fragile = false, heavy = false;
                        for (int g = 0; g < gifts.Count; g++)
                        {
                            fragile |= gifts[g].WeightCategory == PackingWeightCategory.Fragile;
                            heavy |= gifts[g].WeightCategory == PackingWeightCategory.Heavy;
                        }
                        if (!fragile || !heavy)
                        { error = "Для условия хрупкости нужны и хрупкий, и тяжёлый предмет."; return false; }
                        break;
                    case PackingConstraintType.KeepTogether:
                        if (rule.PrimaryGift == null || rule.SecondaryGift == null ||
                            rule.PrimaryGift == rule.SecondaryGift ||
                            !uniqueGifts.Contains(rule.PrimaryGift) || !uniqueGifts.Contains(rule.SecondaryGift))
                        { error = "Для комплекта выбери два разных предмета из этого заказа."; return false; }
                        break;
                    case PackingConstraintType.MustBeInCenter:
                        if (rule.PrimaryGift == null || !uniqueGifts.Contains(rule.PrimaryGift))
                        { error = "Для центрального условия выбери предмет из этого заказа."; return false; }
                        break;
                }
            }
            return true;
        }

        /// <summary>Exhaustive 4×4 feasibility check used by the Editor smoke test and order loader.</summary>
        public bool HasFeasibleSolution(int boardWidth, int boardHeight)
        {
            if (!TryValidateConfiguration(boardWidth, boardHeight, out _)) return false;
            return Search(0, new bool[boardWidth, boardHeight],
                new List<GridPlacement>(gifts.Count), boardWidth, boardHeight);
        }

        private bool Search(int index, bool[,] occupied, List<GridPlacement> placements, int w, int h)
        {
            if (index == gifts.Count) return SatisfiesRules(placements, w, h);
            GiftDefinition gift = gifts[index];
            var seen = new HashSet<string>();
            int turns = gift.CanRotate ? 4 : 1;
            for (int turn = 0; turn < turns; turn++)
            {
                Vector2Int[] shape = RotateAndNormalize(gift.Cells, turn);
                string signature = ShapeSignature(shape);
                if (!seen.Add(signature)) continue;
                int maxX = 0, maxY = 0;
                for (int c = 0; c < shape.Length; c++)
                { maxX = Mathf.Max(maxX, shape[c].x); maxY = Mathf.Max(maxY, shape[c].y); }

                for (int y = 0; y + maxY < h; y++)
                for (int x = 0; x + maxX < w; x++)
                {
                    bool fits = true;
                    for (int c = 0; c < shape.Length; c++)
                        if (occupied[x + shape[c].x, y + shape[c].y]) { fits = false; break; }
                    if (!fits) continue;

                    var worldCells = new Vector2Int[shape.Length];
                    for (int c = 0; c < shape.Length; c++)
                    {
                        worldCells[c] = new Vector2Int(x + shape[c].x, y + shape[c].y);
                        occupied[worldCells[c].x, worldCells[c].y] = true;
                    }
                    placements.Add(new GridPlacement(gift, worldCells));
                    if (Search(index + 1, occupied, placements, w, h)) return true;
                    placements.RemoveAt(placements.Count - 1);
                    for (int c = 0; c < worldCells.Length; c++)
                        occupied[worldCells[c].x, worldCells[c].y] = false;
                }
            }
            return false;
        }

        private bool SatisfiesRules(List<GridPlacement> placements, int w, int h)
        {
            for (int r = 0; r < constraints.Count; r++)
            {
                PackingConstraintDefinition rule = constraints[r];
                switch (rule.Type)
                {
                    case PackingConstraintType.FragileAwayFromHeavy:
                        for (int i = 0; i < placements.Count; i++)
                        for (int j = 0; j < placements.Count; j++)
                            if (placements[i].Gift.WeightCategory == PackingWeightCategory.Fragile &&
                                placements[j].Gift.WeightCategory == PackingWeightCategory.Heavy &&
                                AreAdjacent(placements[i].Cells, placements[j].Cells))
                                return false;
                        break;
                    case PackingConstraintType.KeepTogether:
                        if (!AreAdjacent(Find(placements, rule.PrimaryGift).Cells,
                            Find(placements, rule.SecondaryGift).Cells)) return false;
                        break;
                    case PackingConstraintType.MustBeInCenter:
                        GridPlacement center = Find(placements, rule.PrimaryGift);
                        int minX = w / 2 - 1, maxX = w / 2;
                        int minY = h / 2 - 1, maxY = h / 2;
                        for (int i = 0; i < center.Cells.Length; i++)
                            if (center.Cells[i].x < minX || center.Cells[i].x > maxX ||
                                center.Cells[i].y < minY || center.Cells[i].y > maxY) return false;
                        break;
                }
            }
            return true;
        }

        private static GridPlacement Find(List<GridPlacement> placements, GiftDefinition gift)
        {
            for (int i = 0; i < placements.Count; i++)
                if (placements[i].Gift == gift) return placements[i];
            return default;
        }

        private static Vector2Int[] RotateAndNormalize(Vector2Int[] source, int turns)
        {
            var result = new Vector2Int[source.Length];
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < source.Length; i++)
            {
                Vector2Int cell = source[i];
                for (int t = 0; t < turns; t++) cell = new Vector2Int(cell.y, -cell.x);
                result[i] = cell;
                minX = Mathf.Min(minX, cell.x); minY = Mathf.Min(minY, cell.y);
            }
            for (int i = 0; i < result.Length; i++) result[i] -= new Vector2Int(minX, minY);
            Array.Sort(result, (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return result;
        }

        private static string ShapeSignature(Vector2Int[] cells)
        {
            var values = new string[cells.Length];
            for (int i = 0; i < cells.Length; i++) values[i] = cells[i].x + "," + cells[i].y;
            return string.Join(";", values);
        }

        private static bool AreAdjacent(Vector2Int[] first, Vector2Int[] second)
        {
            for (int i = 0; i < first.Length; i++)
            for (int j = 0; j < second.Length; j++)
                if (Mathf.Abs(first[i].x - second[j].x) <= 1 &&
                    Mathf.Abs(first[i].y - second[j].y) <= 1) return true;
            return false;
        }

        private struct GridPlacement
        {
            public GiftDefinition Gift;
            public Vector2Int[] Cells;
            public GridPlacement(GiftDefinition gift, Vector2Int[] cells) { Gift = gift; Cells = cells; }
        }
    }

    public struct PackingConstraintStatus
    {
        public PackingConstraintDefinition Constraint { get; }
        public PackingConstraintState State { get; }
        public string Message { get; }
        public PackingConstraintStatus(PackingConstraintDefinition constraint, PackingConstraintState state, string message)
        { Constraint = constraint; State = state; Message = message; }
    }

    public static class PackingConstraintEvaluator
    {
        public static List<PackingConstraintStatus> Evaluate(IReadOnlyList<GiftPieceView> pieces,
            IReadOnlyList<PackingConstraintDefinition> constraints, int width, int height)
        {
            var results = new List<PackingConstraintStatus>();
            if (constraints == null) return results;
            for (int i = 0; i < constraints.Count; i++)
            {
                PackingConstraintDefinition rule = constraints[i];
                if (rule == null) continue;
                if (rule.Type == PackingConstraintType.FragileAwayFromHeavy)
                    results.Add(EvaluateWeights(rule, pieces));
                else if (rule.Type == PackingConstraintType.KeepTogether)
                    results.Add(EvaluatePair(rule, pieces));
                else if (rule.Type == PackingConstraintType.MustBeInCenter)
                    results.Add(EvaluateCenter(rule, pieces, width, height));
            }
            return results;
        }

        private static PackingConstraintStatus EvaluateWeights(PackingConstraintDefinition rule,
            IReadOnlyList<GiftPieceView> pieces)
        {
            bool pending = false;
            for (int i = 0; i < pieces.Count; i++)
            {
                GiftPieceView fragile = pieces[i];
                if (fragile.Definition.WeightCategory != PackingWeightCategory.Fragile) continue;
                for (int j = 0; j < pieces.Count; j++)
                {
                    GiftPieceView heavy = pieces[j];
                    if (heavy.Definition.WeightCategory != PackingWeightCategory.Heavy) continue;
                    if (!fragile.IsPlaced || !heavy.IsPlaced) { pending = true; continue; }
                    if (AreAdjacent(WorldCells(fragile), WorldCells(heavy)))
                        return new PackingConstraintStatus(rule, PackingConstraintState.Violated,
                            rule.GetFailureMessage(fragile.Definition, heavy.Definition));
                }
            }
            return new PackingConstraintStatus(rule,
                pending ? PackingConstraintState.Pending : PackingConstraintState.Satisfied, rule.Description);
        }

        private static PackingConstraintStatus EvaluatePair(PackingConstraintDefinition rule,
            IReadOnlyList<GiftPieceView> pieces)
        {
            GiftPieceView first = FindPiece(pieces, rule.PrimaryGift);
            GiftPieceView second = FindPiece(pieces, rule.SecondaryGift);
            if (first == null || second == null || !first.IsPlaced || !second.IsPlaced)
                return new PackingConstraintStatus(rule, PackingConstraintState.Pending, rule.Description);
            return AreAdjacent(WorldCells(first), WorldCells(second))
                ? new PackingConstraintStatus(rule, PackingConstraintState.Satisfied, rule.Description)
                : new PackingConstraintStatus(rule, PackingConstraintState.Violated, rule.GetFailureMessage(null));
        }

        private static PackingConstraintStatus EvaluateCenter(PackingConstraintDefinition rule,
            IReadOnlyList<GiftPieceView> pieces, int width, int height)
        {
            GiftPieceView piece = FindPiece(pieces, rule.PrimaryGift);
            if (piece == null || !piece.IsPlaced)
                return new PackingConstraintStatus(rule, PackingConstraintState.Pending, rule.Description);
            int minX = width / 2 - 1, maxX = width / 2;
            int minY = height / 2 - 1, maxY = height / 2;
            Vector2Int[] cells = WorldCells(piece);
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].x < minX || cells[i].x > maxX || cells[i].y < minY || cells[i].y > maxY)
                    return new PackingConstraintStatus(rule, PackingConstraintState.Violated,
                        rule.GetFailureMessage(piece.Definition));
            return new PackingConstraintStatus(rule, PackingConstraintState.Satisfied, rule.Description);
        }

        private static GiftPieceView FindPiece(IReadOnlyList<GiftPieceView> pieces, GiftDefinition gift)
        {
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i].Definition == gift) return pieces[i];
            return null;
        }

        private static Vector2Int[] WorldCells(GiftPieceView piece)
        {
            Vector2Int[] local = piece.GetRotatedCells();
            var world = new Vector2Int[local.Length];
            for (int i = 0; i < local.Length; i++) world[i] = piece.GridPosition + local[i];
            return world;
        }

        private static bool AreAdjacent(Vector2Int[] first, Vector2Int[] second)
        {
            for (int i = 0; i < first.Length; i++)
            for (int j = 0; j < second.Length; j++)
                if (Mathf.Abs(first[i].x - second[j].x) <= 1 &&
                    Mathf.Abs(first[i].y - second[j].y) <= 1) return true;
            return false;
        }
    }
}