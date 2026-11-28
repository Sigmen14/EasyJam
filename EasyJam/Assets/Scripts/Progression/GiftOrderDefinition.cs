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

    [Header("Тайное желание покупателя")]
    [Tooltip("Необязательно. Заполни Clue, 2–3 Gift Options и Correct Gift Name, чтобы показать диалог до сборки коробки.")]
    public SecretCustomerWishDefinition SecretWish = new SecretCustomerWishDefinition();
}

/// <summary>
/// Optional clue presented before packing. Gift names must match the existing shop shelf catalog.
/// </summary>
[Serializable]
public class SecretCustomerWishDefinition
{
    [TextArea(2, 4)]
    [Tooltip("Короткая фраза покупателя о человеке, которому предназначен подарок.")]
    public string Clue;

    [Tooltip("2–3 варианта из каталога: МИШКА, ПОДАРОК, ЛЕДЕНЕЦ, СНЕЖНЫЙ ШАР.")]
    public List<string> GiftOptions = new List<string>();

    [Tooltip("Правильный вариант из списка Gift Options.")]
    public string CorrectGiftName;

    [Min(0)]
    [Tooltip("Бонусные монеты за правильный ответ.")]
    public int BonusCoins = 10;
}

public enum GiftBoxSize
{
    Small,
    Medium,
    Large
}
