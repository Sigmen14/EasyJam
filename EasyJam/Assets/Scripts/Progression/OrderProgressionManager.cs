using System;
using UnityEngine;

/// <summary>
/// Drives order/day progression and rewards. UI and the packing controller subscribe to the events.
/// GiftDefinition is owned by the packing feature; PackingResult is the shared result contract.
/// </summary>
public class OrderProgressionManager : MonoBehaviour
{
    [SerializeField] private OrderCampaignDefinition campaign;
    [SerializeField] private int startingCoins;

    /// <summary>The campaign currently configured for this manager.</summary>
    public OrderCampaignDefinition Campaign => campaign;

    /// <summary>
    /// Supplies campaign data before StartGame. Used by the visual demo as a recovery path
    /// when a scene was saved before its serialized references were assigned.
    /// </summary>
    public void ConfigureCampaign(OrderCampaignDefinition campaignDefinition)
    {
        if (campaignStarted)
        {
            Debug.LogWarning("OrderProgressionManager: Cannot replace the campaign after the game has started.", this);
            return;
        }

        campaign = campaignDefinition;
    }

    public GiftOrderDefinition CurrentOrder { get; private set; }
    public int CurrentCoins { get; private set; }
    public int CurrentDay { get; private set; }
    public int CurrentOrderNumber { get; private set; }

    public int DayOrdersCompleted { get; private set; }
    public int DayCoinsEarned { get; private set; }
    public int TotalOrdersCompleted { get; private set; }
    public int TotalCoinsEarned { get; private set; }
    public int TotalTipsEarned { get; private set; }

    public OrderDayDefinition CurrentDayData { get; private set; }

    public event Action<GiftOrderDefinition> OrderStarted;
    public event Action<OrderCompletionData> OrderCompleted;
    public event Action<DaySummaryData> DayCompleted;
    public event Action<GameSummaryData> GameCompleted;
    public event Action<int> CoinsChanged;

    private int dayIndex;
    private int orderIndex;
    private bool awaitingResultContinue;
    private bool campaignStarted;
    private bool secretWishBonusAwarded;

    public bool IsCompleted { get; private set; }

    public void StartGame()
    {
        if (campaignStarted)
            return;

        if (campaign == null || campaign.Days == null || campaign.Days.Count == 0)
        {
            Debug.LogError("OrderProgressionManager: Campaign is not assigned or has no days.", this);
            return;
        }

        campaignStarted = true;
        IsCompleted = false;
        CurrentCoins = startingCoins;
        CurrentDay = 0;
        CurrentOrderNumber = 0;
        DayOrdersCompleted = 0;
        DayCoinsEarned = 0;
        TotalOrdersCompleted = 0;
        TotalCoinsEarned = 0;
        TotalTipsEarned = 0;
        awaitingResultContinue = false;
        dayIndex = 0;
        orderIndex = 0;
        StartCurrentDay();
    }

    /// <summary>Starts a fresh campaign after the previous run has ended.</summary>
    public void RestartGame()
    {
        if (campaign == null || campaign.Days == null || campaign.Days.Count == 0)
        {
            Debug.LogError("OrderProgressionManager: Cannot restart because the campaign is missing.", this);
            return;
        }

        campaignStarted = false;
        CurrentOrder = null;
        CurrentDayData = null;
        IsCompleted = false;
        StartGame();
    }

    /// <summary>Called by the packing feature after a packing attempt has ended.</summary>
    public void CompleteCurrentOrder(PackingResult result)
    {
        if (!campaignStarted || CurrentOrder == null || awaitingResultContinue)
            return;

        if (result == null)
        {
            Debug.LogError("OrderProgressionManager: Packing returned a null result.", this);
            result = new PackingResult(false, false, 0, 0);
        }

        bool success = result.IsValid && result.AllItemsPlaced
            && result.PlacedCount >= 0 && result.TotalCount >= 0
            && result.PlacedCount == result.TotalCount;

        int baseReward = success ? Mathf.Max(0, CurrentOrder.RewardCoins) : 0;
        int tip = success ? Mathf.Max(0, CurrentOrder.PerfectPackingTip) : 0;
        int earned = baseReward + tip;

        if (earned > 0)
            AddCoins(earned);

        if (success)
        {
            DayOrdersCompleted++;
            DayCoinsEarned += earned;
            TotalOrdersCompleted++;
            TotalCoinsEarned += earned;
            TotalTipsEarned += tip;
        }

        awaitingResultContinue = true;
        OrderCompleted?.Invoke(new OrderCompletionData(CurrentOrder, result, success, baseReward, tip, earned));
    }

    /// <summary>
    /// Awards the optional secret-wish bonus without completing or changing the active order.
    /// The UI calls this only after the player selects the configured correct gift.
    /// </summary>
    public int AwardSecretWishBonus(bool correctAnswer, int amount)
    {
        if (!correctAnswer || !campaignStarted || CurrentOrder == null
            || awaitingResultContinue || secretWishBonusAwarded)
            return 0;

        secretWishBonusAwarded = true;
        int bonus = Mathf.Max(0, amount);
        if (bonus == 0)
            return 0;

        AddCoins(bonus);
        DayCoinsEarned += bonus;
        TotalCoinsEarned += bonus;
        return bonus;
    }

    /// <summary>UI calls after showing the order-result panel.</summary>
    public void ContinueAfterResult()
    {
        if (!awaitingResultContinue)
            return;

        awaitingResultContinue = false;
        orderIndex++;

        if (CurrentDayData != null && orderIndex < CurrentDayData.Orders.Count)
        {
            StartCurrentOrder();
            return;
        }

        FinishCurrentDay();
    }

    private void StartCurrentDay()
    {
        CurrentDayData = campaign.Days[dayIndex];
        CurrentDay = CurrentDayData.DayNumber;
        DayOrdersCompleted = 0;
        DayCoinsEarned = 0;
        orderIndex = 0;

        if (CurrentDayData.Orders == null || CurrentDayData.Orders.Count == 0)
        {
            Debug.LogWarning($"OrderProgressionManager: Day {CurrentDay} has no orders; ending the day.", this);
            FinishCurrentDay();
            return;
        }

        StartCurrentOrder();
    }

    private void StartCurrentOrder()
    {
        secretWishBonusAwarded = false;
        CurrentOrder = CurrentDayData.Orders[orderIndex];
        CurrentOrderNumber = orderIndex + 1;
        if (CurrentOrder == null)
        {
            Debug.LogError($"OrderProgressionManager: Missing order asset at day {CurrentDay}, index {orderIndex}.", this);
            return;
        }

        OrderStarted?.Invoke(CurrentOrder);
    }

    private void FinishCurrentDay()
    {
        DayCompleted?.Invoke(new DaySummaryData(CurrentDay, CurrentDayData.Orders.Count, DayOrdersCompleted, DayCoinsEarned, CurrentCoins));

        dayIndex++;
        if (dayIndex >= campaign.Days.Count)
        {
            CurrentOrder = null;
            CurrentDayData = null;
            IsCompleted = true;
            GameCompleted?.Invoke(new GameSummaryData(CurrentCoins, TotalOrdersCompleted, TotalCoinsEarned, TotalTipsEarned, campaign.Days.Count));
            return;
        }

        StartCurrentDay();
    }

    private void AddCoins(int amount)
    {
        CurrentCoins += amount;
        CoinsChanged?.Invoke(CurrentCoins);
    }
}

public sealed class OrderCompletionData
{
    public GiftOrderDefinition Order { get; }
    public PackingResult PackingResult { get; }
    public bool Success { get; }
    public int BaseReward { get; }
    public int Tip { get; }
    public int TotalEarned { get; }

    public OrderCompletionData(GiftOrderDefinition order, PackingResult result, bool success, int baseReward, int tip, int totalEarned)
    {
        Order = order;
        PackingResult = result;
        Success = success;
        BaseReward = baseReward;
        Tip = tip;
        TotalEarned = totalEarned;
    }
}

public sealed class DaySummaryData
{
    public int Day { get; }
    public int OrdersTotal { get; }
    public int OrdersCompleted { get; }
    public int CoinsEarned { get; }
    public int CurrentBalance { get; }

    public DaySummaryData(int day, int ordersTotal, int ordersCompleted, int coinsEarned, int currentBalance)
    {
        Day = day;
        OrdersTotal = ordersTotal;
        OrdersCompleted = ordersCompleted;
        CoinsEarned = coinsEarned;
        CurrentBalance = currentBalance;
    }
}

public sealed class GameSummaryData
{
    public int FinalCoins { get; }
    public int OrdersCompleted { get; }
    public int TotalCoinsEarned { get; }
    public int TipsEarned { get; }
    public int DaysPlayed { get; }

    public GameSummaryData(int finalCoins, int ordersCompleted, int totalCoinsEarned, int tipsEarned, int daysPlayed)
    {
        FinalCoins = finalCoins;
        OrdersCompleted = ordersCompleted;
        TotalCoinsEarned = totalCoinsEarned;
        TipsEarned = tipsEarned;
        DaysPlayed = daysPlayed;
    }
}
