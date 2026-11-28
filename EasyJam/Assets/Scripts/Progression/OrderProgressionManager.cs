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

    /// <summary>
    /// Enables the small customer queue without replacing the existing campaign or reward system.
    /// The queue is deliberately capped at three visible customers.
    /// </summary>
    public void ConfigureOrderQueue(bool enabled, int maximumWaitingOrders = 3,
        float urgentDeadlineSeconds = 120f, int urgencyBonusCoins = 8)
    {
        if (campaignStarted)
        {
            Debug.LogWarning("OrderProgressionManager: Queue settings cannot change after the game has started.", this);
            return;
        }

        orderQueueEnabled = enabled;
        maxWaitingOrders = Mathf.Clamp(maximumWaitingOrders, 1, 3);
        urgentOrderDeadlineSeconds = Mathf.Max(1f, urgentDeadlineSeconds);
        urgentOrderBonusCoins = Mathf.Max(0, urgencyBonusCoins);
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

    public bool OrderQueueEnabled => orderQueueEnabled;
    public IReadOnlyList<OrderQueueEntry> WaitingOrders => waitingOrders;
    public OrderQueueEntry ActiveQueueEntry => activeQueueEntry;
    public bool HasPendingOrders => orderQueueEnabled && CurrentDayData != null
        && CurrentDayData.Orders != null
        && (waitingOrders.Count > 0 || nextQueueOrderIndex < CurrentDayData.Orders.Count);

    public event Action<IReadOnlyList<OrderQueueEntry>> OrderQueueChanged;
    public event Action<IReadOnlyList<OrderQueueEntry>> OrderQueueTimerChanged;
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

    private bool orderQueueEnabled;
    private int maxWaitingOrders = 3;
    private float urgentOrderDeadlineSeconds = 120f;
    private int urgentOrderBonusCoins = 8;
    private int nextQueueOrderIndex;
    private int activeQueueOrderIndex = -1;
    private readonly List<OrderQueueEntry> waitingOrders = new List<OrderQueueEntry>();
    private readonly HashSet<int> completedQueueOrderIndices = new HashSet<int>();
    private OrderQueueEntry activeQueueEntry;

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

    private void Update()
    {
        if (!orderQueueEnabled || Time.deltaTime <= 0f)
            return;

        bool waitingTimerChanged = false;
        for (int i = 0; i < waitingOrders.Count; i++)
        {
            OrderQueueEntry entry = waitingOrders[i];
            int previousSecond = Mathf.FloorToInt(entry.WaitedSeconds);
            entry.AdvanceWait(Time.deltaTime);
            if (Mathf.FloorToInt(entry.WaitedSeconds) != previousSecond)
                waitingTimerChanged = true;
        }

        // The selected customer's deadline keeps running during packing. Pausing the game
        // sets timeScale to zero, so Time.deltaTime freezes every queue timer automatically.
        if (activeQueueEntry != null && CurrentOrder != null)
        {
            int previousSecond = Mathf.FloorToInt(activeQueueEntry.WaitedSeconds);
            activeQueueEntry.AdvanceWait(Time.deltaTime);
            if (Mathf.FloorToInt(activeQueueEntry.WaitedSeconds) != previousSecond)
                waitingTimerChanged = true;
        }

        if (waitingTimerChanged)
            OrderQueueTimerChanged?.Invoke(waitingOrders);
    }

    private void GenerateSecretWishAssignments()
    {
        int seed = Guid.NewGuid().GetHashCode() ^ Environment.TickCount;
        int orderCount = 0;
        if (campaign != null && campaign.Days != null)
        {
            for (int i = 0; i < campaign.Days.Count; i++)
            {
                if (campaign.Days[i] != null && campaign.Days[i].Orders != null)
                    orderCount += campaign.Days[i].Orders.Count;
            }
        }

        // Preserve the established nine-order campaign and its four secret wishes.
        // The short story run deliberately skips optional wish popups to stay under 10 minutes.
        if (orderCount == 9)
            secretWishAssignments = CreateSecretWishSequence(seed, previousSecretWishAssignments);
        else
            secretWishAssignments = new bool[orderCount];

        previousSecretWishAssignments = GetSecretWishAssignments();
    }

    private int GetCurrentOrderFlatIndex()
    {
        if (campaign == null || campaign.Days == null || dayIndex < 0 || dayIndex >= campaign.Days.Count)
            return -1;

        int index = orderQueueEnabled && activeQueueOrderIndex >= 0
            ? activeQueueOrderIndex
            : orderIndex;
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
        CurrentOrder = null;
        waitingOrders.Clear();
        completedQueueOrderIndices.Clear();
        activeQueueEntry = null;
        activeQueueOrderIndex = -1;
        nextQueueOrderIndex = 0;
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

    /// <summary>
    /// Ends the current run without destroying the manager or its event subscriptions.
    /// The next StartGame call begins a clean campaign while preserving the previous
    /// secret-wish pattern used to avoid an identical consecutive run.
    /// </summary>
    public void ReturnToMenu()
    {
        campaignStarted = false;
        IsCompleted = false;
        CurrentOrder = null;
        CurrentDayData = null;
        CurrentCoins = startingCoins;
        CurrentDay = 0;
        CurrentOrderNumber = 0;
        DayOrdersCompleted = 0;
        DayCoinsEarned = 0;
        TotalOrdersCompleted = 0;
        TotalCoinsEarned = 0;
        TotalTipsEarned = 0;
        TotalReputationEarned = 0;
        dayIndex = 0;
        orderIndex = 0;
        awaitingResultContinue = false;
        awaitingDayContinue = false;
        secretWishShownForCurrentOrder = false;
        secretWishBonusAwarded = false;
        secretWishAssignments = new bool[0];
        waitingOrders.Clear();
        completedQueueOrderIndices.Clear();
        activeQueueEntry = null;
        activeQueueOrderIndex = -1;
        nextQueueOrderIndex = 0;
        OrderQueueChanged?.Invoke(waitingOrders);
        CoinsChanged?.Invoke(CurrentCoins);
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
        int urgencyBonus = success && orderQueueEnabled && activeQueueEntry != null
            && activeQueueEntry.UrgencyBonusAvailable
            ? activeQueueEntry.UrgencyBonusCoins : 0;
        int earned = baseReward + tip + wrappingBonus + urgencyBonus;

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
            if (orderQueueEnabled && activeQueueOrderIndex >= 0)
                completedQueueOrderIndices.Add(activeQueueOrderIndex);
        }

        // A failed packing attempt leaves the selected order active so it can be corrected
        // and retried. A successful result is locked before listeners receive the callback.
        if (!success && orderQueueEnabled)
        {
            OrderCompleted?.Invoke(new OrderCompletionData(
                CurrentOrder, result, false, baseReward, tip, wrappingBonus, reputationBonus, 0, 0));
            return;
        }

        // Set this before invoking listeners so repeated clicks cannot award anything twice.
        awaitingResultContinue = true;
        OrderCompleted?.Invoke(new OrderCompletionData(
            CurrentOrder, result, success, baseReward, tip, wrappingBonus, reputationBonus, earned, urgencyBonus));
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

        if (orderQueueEnabled)
        {
            CurrentOrder = null;
            activeQueueEntry = null;
            activeQueueOrderIndex = -1;
            FillWaitingOrderQueue();

            if (CurrentDayData != null && CurrentDayData.Orders != null
                && completedQueueOrderIndices.Count >= CurrentDayData.Orders.Count)
            {
                FinishCurrentDay();
                return;
            }

            OrderQueueChanged?.Invoke(waitingOrders);
            return;
        }

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

        if (orderQueueEnabled)
        {
            nextQueueOrderIndex = 0;
            activeQueueOrderIndex = -1;
            activeQueueEntry = null;
            waitingOrders.Clear();
            completedQueueOrderIndices.Clear();
            FillWaitingOrderQueue();
            OrderQueueChanged?.Invoke(waitingOrders);
            return;
        }

        StartCurrentOrder();
    }

    /// <summary>Selects a customer from the waiting list without changing campaign ownership.</summary>
    public bool SelectQueuedOrder(GiftOrderDefinition order)
    {
        if (!orderQueueEnabled || !campaignStarted || order == null || CurrentOrder != null
            || awaitingResultContinue || awaitingDayContinue || CurrentDayData == null)
            return false;

        for (int i = 0; i < waitingOrders.Count; i++)
        {
            OrderQueueEntry entry = waitingOrders[i];
            if (entry == null || entry.Order != order)
                continue;

            waitingOrders.RemoveAt(i);
            activeQueueEntry = entry;
            activeQueueOrderIndex = entry.CampaignOrderNumber - 1;
            orderIndex = activeQueueOrderIndex;
            CurrentOrder = entry.Order;
            CurrentOrderNumber = entry.CampaignOrderNumber;
            secretWishShownForCurrentOrder = false;
            secretWishBonusAwarded = false;

            bool showSecretWish = IsSecretWishAssignedForCurrentOrder;
            OrderQueueChanged?.Invoke(waitingOrders);
            OrderStarted?.Invoke(CurrentOrder, showSecretWish);
            return true;
        }

        return false;
    }

    private void FillWaitingOrderQueue()
    {
        if (!orderQueueEnabled || CurrentDayData == null || CurrentDayData.Orders == null)
            return;

        while (waitingOrders.Count < maxWaitingOrders
            && nextQueueOrderIndex < CurrentDayData.Orders.Count)
        {
            int candidateIndex = nextQueueOrderIndex;
            GiftOrderDefinition candidate = CurrentDayData.Orders[candidateIndex];
            if (candidate == null)
            {
                Debug.LogWarning($"OrderProgressionManager: Missing order asset at day {CurrentDay}, index {candidateIndex}.", this);
                completedQueueOrderIndices.Add(candidateIndex);
                nextQueueOrderIndex++;
                continue;
            }

            // The story scarf order cannot enter the queue until every earlier order
            // has been completed. It is never randomly selected or discarded.
            if (candidate.IsStoryFinaleOrder && !HasCompletedOrdersBefore(candidateIndex))
                break;

            bool isUrgent = !candidate.IsStoryFinaleOrder && (candidateIndex + 1) % 2 == 0;
            // Small staggered arrival offsets make customer waiting times readable at a glance,
            // without adding a separate spawn timer or stretching the short campaign.
            float initialWaitSeconds = (candidateIndex % maxWaitingOrders) * 12f;
            waitingOrders.Add(new OrderQueueEntry(candidate, candidateIndex + 1,
                isUrgent, urgentOrderDeadlineSeconds, urgentOrderBonusCoins, initialWaitSeconds));
            nextQueueOrderIndex++;
        }
    }

    private bool HasCompletedOrdersBefore(int orderIndexToCheck)
    {
        for (int i = 0; i < orderIndexToCheck; i++)
        {
            if (!completedQueueOrderIndices.Contains(i))
                return false;
        }

        return true;
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

    /// <summary>Awards a small event reward and includes it in campaign earnings.</summary>
    public void AwardBonusCoins(int amount)
    {
        int reward = Mathf.Max(0, amount);
        if (reward == 0) return;
        AddCoins(reward);
        DayCoinsEarned += reward;
        TotalCoinsEarned += reward;
    }

    /// <summary>Spends coins only when the current balance can cover the full price.</summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || CurrentCoins < amount) return false;
        CurrentCoins -= amount;
        CoinsChanged?.Invoke(CurrentCoins);
        return true;
    }

    private void AddCoins(int amount)
    {
        CurrentCoins += amount;
        CoinsChanged?.Invoke(CurrentCoins);
    }
}

public sealed class OrderQueueEntry
{
    private float waitedSeconds;

    public GiftOrderDefinition Order { get; }
    public int CampaignOrderNumber { get; }
    public bool IsUrgent { get; }
    public float WaitedSeconds => waitedSeconds;
    public int UrgencyBonusCoins { get; }
    public bool UrgencyBonusAvailable => IsUrgent && waitedSeconds < urgencyDeadlineSeconds;
    public float UrgencySecondsRemaining => Mathf.Max(0f, urgencyDeadlineSeconds - waitedSeconds);

    private readonly float urgencyDeadlineSeconds;

    public OrderQueueEntry(GiftOrderDefinition order, int campaignOrderNumber, bool isUrgent,
        float urgencyDeadlineSeconds, int urgencyBonusCoins, float initialWaitSeconds = 0f)
    {
        Order = order;
        CampaignOrderNumber = campaignOrderNumber;
        IsUrgent = isUrgent;
        this.urgencyDeadlineSeconds = Mathf.Max(1f, urgencyDeadlineSeconds);
        UrgencyBonusCoins = Mathf.Max(0, urgencyBonusCoins);
        waitedSeconds = Mathf.Max(0f, initialWaitSeconds);
    }

    internal void AdvanceWait(float seconds)
    {
        if (seconds > 0f)
            waitedSeconds += seconds;
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
    public int UrgencyBonus { get; }
    public int TotalEarned { get; }

    public OrderCompletionData(GiftOrderDefinition order, PackingResult result, bool success, int baseReward,
        int tip, int wrappingBonus, int reputationBonus, int totalEarned)
        : this(order, result, success, baseReward, tip, wrappingBonus, reputationBonus, totalEarned, 0)
    {
    }

    public OrderCompletionData(GiftOrderDefinition order, PackingResult result, bool success, int baseReward,
        int tip, int wrappingBonus, int reputationBonus, int totalEarned, int urgencyBonus)
    {
        Order = order;
        PackingResult = result;
        Success = success;
        BaseReward = baseReward;
        Tip = tip;
        WrappingBonus = wrappingBonus;
        ReputationBonus = reputationBonus;
        UrgencyBonus = urgencyBonus;
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
