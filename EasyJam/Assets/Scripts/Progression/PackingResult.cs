using System;

/// <summary>
/// Result of one packing attempt. The packing controller creates this value and
/// passes it to OrderProgressionManager when the player finishes an order.
/// </summary>
[Serializable]
public sealed class PackingResult
{
    public bool IsValid;
    public bool AllItemsPlaced;
    public int PlacedCount;
    public int TotalCount;

    public PackingResult()
    {
    }

    public PackingResult(bool isValid, bool allItemsPlaced, int placedCount, int totalCount)
    {
        IsValid = isValid;
        AllItemsPlaced = allItemsPlaced;
        PlacedCount = placedCount;
        TotalCount = totalCount;
    }
}
