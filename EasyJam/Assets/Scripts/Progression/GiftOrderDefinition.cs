using System.Collections.Generic;
using UnityEngine;

/// <summary>Data for one customer order. Gift names/IDs are kept independent of the packing implementation.</summary>
[CreateAssetMenu(fileName = "GiftOrder", menuName = "The Last Gift/Order")]
public class GiftOrderDefinition : ScriptableObject
{
    [Header("Customer")]
    public string CustomerName;
    [TextArea(2, 5)] public string CustomerWish;
    public Sprite CustomerPortrait;

    [Header("Order")]
    [Tooltip("GiftDefinition identifiers/names. The packing system can resolve these when its catalog is available.")]
    public List<string> GiftNames = new List<string>();
    public GiftBoxSize BoxSize = GiftBoxSize.Small;
    [Min(0)] public int RewardCoins = 30;
    [Min(1)] public int Day = 1;

    [Header("Bonus")]
    [Tooltip("Tip awarded when a valid packing result places every item.")]
    [Min(0)] public int PerfectPackingTip = 5;
}

public enum GiftBoxSize
{
    Small,
    Medium,
    Large
}
