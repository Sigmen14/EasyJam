using UnityEngine;

public enum WorkshopUpgradeType
{
    FestiveGarland,
    ExtraShelf,
    PremiumWrapping
}

[DisallowMultipleComponent]
[RequireComponent(typeof(OrderProgressionManager))]
public sealed class LastGiftWorkshopUpgradeManager : MonoBehaviour
{
    public const int GarlandCost = 15;
    public const int ExtraShelfCost = 20;
    public const int PremiumWrappingCost = 25;

    private OrderProgressionManager progression;
    private bool garlandPurchased, extraShelfPurchased, premiumWrappingPurchased;
    public bool HasGarland => garlandPurchased;
    public bool HasExtraShelf => extraShelfPurchased;
    public bool HasPremiumWrapping => premiumWrappingPurchased;

    private void Awake() => progression = GetComponent<OrderProgressionManager>();

    public bool IsPurchased(WorkshopUpgradeType upgrade)
    {
        switch (upgrade)
        {
            case WorkshopUpgradeType.FestiveGarland: return garlandPurchased;
            case WorkshopUpgradeType.ExtraShelf: return extraShelfPurchased;
            case WorkshopUpgradeType.PremiumWrapping: return premiumWrappingPurchased;
            default: return false;
        }
    }

    public int GetCost(WorkshopUpgradeType upgrade)
    {
        switch (upgrade)
        {
            case WorkshopUpgradeType.FestiveGarland: return GarlandCost;
            case WorkshopUpgradeType.ExtraShelf: return ExtraShelfCost;
            case WorkshopUpgradeType.PremiumWrapping: return PremiumWrappingCost;
            default: return 0;
        }
    }

    public bool TryPurchase(WorkshopUpgradeType upgrade)
    {
        if (progression == null)
            progression = GetComponent<OrderProgressionManager>();
        if (progression == null || IsPurchased(upgrade)) return false;

        int cost = GetCost(upgrade);
        if (cost <= 0 || !progression.TrySpendCoins(cost)) return false;
        switch (upgrade)
        {
            case WorkshopUpgradeType.FestiveGarland: garlandPurchased = true; break;
            case WorkshopUpgradeType.ExtraShelf: extraShelfPurchased = true; break;
            case WorkshopUpgradeType.PremiumWrapping: premiumWrappingPurchased = true; break;
        }
        return true;
    }

    public void ResetForNewGame()
    {
        garlandPurchased = false;
        extraShelfPurchased = false;
        premiumWrappingPurchased = false;
    }
}
