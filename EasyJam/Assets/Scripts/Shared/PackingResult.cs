using System;

namespace TheLastGift.Packing
{
    /// <summary>Summary returned by packing validation.</summary>
    [Serializable]
    public struct PackingResult
    {
        public bool IsValid;
        public bool AllItemsPlaced;
        public int PlacedCount;
        public int TotalCount;
        public string Message;

        public PackingResult(bool isValid, bool allItemsPlaced, int placedCount, int totalCount, string message)
        {
            IsValid = isValid;
            AllItemsPlaced = allItemsPlaced;
            PlacedCount = placedCount;
            TotalCount = totalCount;
            Message = message;
        }
    }
}
