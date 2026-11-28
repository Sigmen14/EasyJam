using System;
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

    [Header("Wrapping preferences")]
    public GiftWrappingPaper PreferredWrappingPaper = GiftWrappingPaper.Red;
    public GiftWrappingRibbon PreferredWrappingRibbon = GiftWrappingRibbon.BigRed;
    public GiftWrappingCard PreferredWrappingCard = GiftWrappingCard.MerryChristmas;
    [TextArea(1, 2)] public string WrappingHint;
    [Min(0)] public int WrappingBonusCoins = 10;
    [Min(0)] public int PerfectWrappingReputationBonus = 1;
}

public enum GiftBoxSize
{
    Small,
    Medium,
    Large
}


public enum GiftWrappingPaper
{
    Red,
    Green,
    Blue,
    Gold,
    Blush
}

public enum GiftWrappingRibbon
{
    BigRed,
    BigGold,
    ClassicGreen,
    ThinBlue,
    SoftPink
}

public enum GiftWrappingCard
{
    MerryChristmas,
    WarmWishes,
    DreamsComeTrue,
    WithLove,
    HappyHolidays
}

[Serializable]
public struct GiftWrappingSelection
{
    public GiftWrappingPaper Paper;
    public GiftWrappingRibbon Ribbon;
    public GiftWrappingCard Card;

    public GiftWrappingSelection(GiftWrappingPaper paper, GiftWrappingRibbon ribbon, GiftWrappingCard card)
    {
        Paper = paper;
        Ribbon = ribbon;
        Card = card;
    }
}
