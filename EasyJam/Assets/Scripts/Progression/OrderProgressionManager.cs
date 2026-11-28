using System;
using System.Collections.Generic;
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
    public int TotalReputationEarned { get; private set; }

    public OrderDayDefinition CurrentDayData { get; private set; }

    public event Action<GiftOrderDefinition, bool> OrderStarted;
    public event Action<OrderCompletionData> OrderCompleted;
    public event Action<DaySummaryData> DayCompleted;
    public event Action<GameSummaryData> GameCompleted;
    public event Action<int> CoinsChanged;

    private int dayIndex;
    private int orderIndex;
    private bool awaitingResultContinue;
    private bool awaitingDayContinue;
    private bool campaignStarted;
    private bool secretWishShownForCurrentOrder;
    private bool secretWishBonusAwarded;
    private bool[] secretWishAssignments = new bool[9];
    private bool[] previousSecretWishAssignments;

    public bool IsCompleted { get; private set; }

    public bool IsSecretWishAssignedForCurrentOrder
    {
        get
        {
            if (!campaignStarted || CurrentOrder == null || CurrentOrder.SecretWish == null
                || string.IsNullOrWhiteSpace(CurrentOrder.SecretWish.Clue))
                return false;

            int assignmentIndex = GetCurrentOrderFlatIndex();
            return secretWishAssignments != null
                && assignmentIndex >= 0
                && assignmentIndex < secretWishAssignments.Length
                && secretWishAssignments[assignmentIndex];
        }
    }

    public bool HasSecretWishBeenShownForCurrentOrder => secretWishShownForCurrentOrder;

    /// <summary>Returns a copy so callers cannot mutate the active campaign sequence.</summary>
    public bool[] GetSecretWishAssignments()
    {
        bool[] copy = new bool[secretWishAssignments == null ? 0 : secretWishAssignments.Length];
        if (secretWishAssignments != null)
            Array.Copy(secretWishAssignments, copy, copy.Length);
        return copy;
    }

    /// <summary>
    /// Builds a reproducible nine-order sequence for tests. Valid masks contain exactly four
    /// wishes, at least one per day, and never contain three equal states in a row.
    /// </summary>
    public static bool[] CreateSecretWishSequence(int seed)
    {
        return CreateSecretWishSequence(seed, null);
    }

    public static bool IsValidSecretWishSequence(IList<bool> sequence)
    {
        if (sequence == null || sequence.Count != 9)
            return false;

        int wishCount = 0;
        int previousRun = 0;
        bool previousValue = false;
        int wishRun = 0;
        int noWishRun = 0;

        for (int i = 0; i < sequence.Count; i++)
        {
            bool hasWish = sequence[i];
            if (hasWish)
            {
                wishCount++;
                wishRun = previousRun > 0 && previousValue ? wishRun + 1 : 1;
                noWishRun = 0;
                previousRun = wishRun;
            }
            else
            {
                noWishRun = previousRun > 0 && !previousValue ? noWishRun + 1 : 1;
                wishRun = 0;
                previousRun = noWishRun;
            }

            previousValue = hasWish;
            if (wishRun > 2 || noWishRun > 2)
                return false;
        }

        if (wishCount != 4)
            return false;

        for (int day = 0; day < 3; day++)
        {
            if (!sequence[day * 3] && !sequence[day * 3 + 1] && !sequence[day * 3 + 2])
                return false;
        }

        return true;
    }

    private static bool[] CreateSecretWishSequence(int seed, IList<bool> excludedSequence)
    {
        List<int> validMasks = new List<int>();
        for (int mask = 0; mask < (1 << 9); mask++)
        {
            bool[] candidate = new bool[9];
            for (int bit = 0; bit < candidate.Length; bit++)
                candidate[bit] = (mask & (1 << bit)) != 0;

            if (!IsValidSecretWishSequence(candidate))
                continue;

            if (excludedSequence != null && SequencesMatch(candidate, excludedSequence))
                continue;

            validMasks.Add(mask);
        }

        if (validMasks.Count == 0)
            throw new InvalidOperationException("No valid secret-wish sequence is available.");

        int selectedMask = validMasks[new System.Random(seed).Next(validMasks.Count)];
        bool[] selected = new bool[9];
        for (int bit = 0; bit < selected.Length; bit++)
            selected[bit] = (selectedMask & (1 << bit)) != 0;

        return selected;
    }

    private static bool SequencesMatch(IList<bool> left, IList<bool> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private void GenerateSecretWishAssignments()
    {
        int seed = Guid.NewGuid().GetHashCode() ^ Environment.TickCount;
        secretWishAssignments = CreateSecretWishSequence(seed, previousSecretWishAssignments);
        previousSecretWishAssignments = GetSecretWishAssignments();
    }

    private int GetCurrentOrderFlatIndex()
    {
        if (campaign == null || campaign.Days == null || dayIndex < 0 || dayIndex >= campaign.Days.Count)
            return -1;

        int index = orderIndex;
        for (int i = 0; i < dayIndex; i++)
        {
            if (campaign.Days[i] == null || campaign.Days[i].Orders == null)
                return -1;
            index += campaign.Days[i].Orders.Count;
        }

        return index;
    }

    /// <summary>Called by the UI only after the assigned customer's wish panel is displayed.</summary>
    public void MarkCurrentSecretWishShown()
    {
        if (IsSecretWishAssignedForCurrentOrder)
            secretWishShownForCurrentOrder = true;
    }

    /// <summary>Only a wish that was assigned and actually displayed can award its bonus.</summary>
    public int AwardSecretWishBonus(bool correctAnswer, int amount)
    {
        if (!correctAnswer || !IsSecretWishAssignedForCurrentOrder
            || !secretWishShownForCurrentOrder || secretWishBonusAwarded
            || awaitingResultContinue)
            return 0;

        secretWishBonusAwarded = true;
        int bonus = Mathf.Max(0, amount);
        if (bonus > 0)
        {
            AddCoins(bonus);
            DayCoinsEarned += bonus;
            TotalCoinsEarned += bonus;
        }

        return bonus;
    }

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
        TotalReputationEarned = 0;
        awaitingResultContinue = false;
        awaitingDayContinue = false;
        GenerateSecretWishAssignments();
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
        CompleteCurrentOrder(result, false, false);
    }

    /// <summary>Completes an order and applies optional wrapping rewards after the gift contents are valid.</summary>
    public void CompleteCurrentOrder(PackingResult result, bool suitableWrapping, bool perfectWrapping)
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
        int wrappingBonus = success && suitableWrapping ? Mathf.Max(0, CurrentOrder.WrappingBonusCoins) : 0;
        int reputationBonus = success && suitableWrapping && perfectWrapping
            ? Mathf.Max(0, CurrentOrder.PerfectWrappingReputationBonus) : 0;
        int earned = baseReward + tip + wrappingBonus;

        if (earned > 0)
            AddCoins(earned);

        if (success)
        {
            DayOrdersCompleted++;
            DayCoinsEarned += earned;
            TotalOrdersCompleted++;
            TotalCoinsEarned += earned;
            TotalTipsEarned += tip;
            TotalReputationEarned += reputationBonus;
        }

        // Set this before invoking listeners so repeated clicks cannot award anything twice.
        awaitingResultContinue = true;
        OrderCompleted?.Invoke(new OrderCompletionData(
            CurrentOrder, result, success, baseReward, tip, wrappingBonus, reputationBonus, earned));
    }

    /// <summary>
    /// UI calls after an order result or day summary. A completed day is deliberately paused
    /// until the player presses the day-summary button.
    /// </summary>
    public void ContinueAfterResult()
    {
        if (awaitingDayContinue)
        {
            awaitingDayContinue = false;
            StartCurrentDay();
            return;
        }

        if (!awaitingResultContinue)
            return;

        awaitingResultContinue = false;
        orderIndex++;

        if (CurrentDayData != null && CurrentDayData.Orders != null
            && orderIndex < CurrentDayData.Orders.Count)
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
        secretWishShownForCurrentOrder = false;
        secretWishBonusAwarded = false;
        CurrentOrder = CurrentDayData.Orders[orderIndex];
        CurrentOrderNumber = orderIndex + 1;
        if (CurrentOrder == null)
        {
            Debug.LogError($"OrderProgressionManager: Missing order asset at day {CurrentDay}, index {orderIndex}.", this);
            return;
        }

        bool showSecretWish = IsSecretWishAssignedForCurrentOrder;
        OrderStarted?.Invoke(CurrentOrder, showSecretWish);
    }

    private void FinishCurrentDay()
    {
        DayCompleted?.Invoke(new DaySummaryData(CurrentDay, CurrentDayData.Orders.Count, DayOrdersCompleted, DayCoinsEarned, CurrentCoins));

        dayIndex++;
        CurrentOrder = null;
        CurrentDayData = null;

        if (dayIndex >= campaign.Days.Count)
        {
            awaitingDayContinue = false;
            IsCompleted = true;
            GameCompleted?.Invoke(new GameSummaryData(
                CurrentCoins, TotalOrdersCompleted, TotalCoinsEarned, TotalTipsEarned,
                TotalReputationEarned, campaign.Days.Count));
            return;
        }

        // Do not start the next day yet. The day summary is a real pause in progression,
        // and its button is responsible for advancing to the next day.
        awaitingDayContinue = true;
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
    public int WrappingBonus { get; }
    public int ReputationBonus { get; }
    public int TotalEarned { get; }

    public OrderCompletionData(GiftOrderDefinition order, PackingResult result, bool success, int baseReward,
        int tip, int wrappingBonus, int reputationBonus, int totalEarned)
    {
        Order = order;
        PackingResult = result;
        Success = success;
        BaseReward = baseReward;
        Tip = tip;
        WrappingBonus = wrappingBonus;
        ReputationBonus = reputationBonus;
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
    public int ReputationEarned { get; }
    public int DaysPlayed { get; }

    public GameSummaryData(int finalCoins, int ordersCompleted, int totalCoinsEarned, int tipsEarned,
        int reputationEarned, int daysPlayed)
    {
        FinalCoins = finalCoins;
        OrdersCompleted = ordersCompleted;
        TotalCoinsEarned = totalCoinsEarned;
        TipsEarned = tipsEarned;
        ReputationEarned = reputationEarned;
        DaysPlayed = daysPlayed;
    }
}
