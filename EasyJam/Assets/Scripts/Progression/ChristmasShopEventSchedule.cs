using System;
using System.Collections.Generic;

/// <summary>Randomized Christmas workshop events that can occur between completed orders.</summary>
public enum ChristmasShopEventType
{
    MixedGifts,
    RushOrder,
    HappyCustomer,
    RibbonShortage,
    ChristmasSurprise,
    FestiveMood
}

/// <summary>A queued shop event and the successful-order milestone that triggered it.</summary>
public struct ScheduledChristmasShopEvent
{
    public ChristmasShopEventType Type { get; }
    public int OrderMilestone { get; }

    public ScheduledChristmasShopEvent(ChristmasShopEventType type, int orderMilestone)
    {
        Type = type;
        OrderMilestone = orderMilestone;
    }
}

/// <summary>
/// Queues at most one event per two successful orders. Event kinds are selected randomly
/// without replacement during a campaign, and the caller consumes them only after the
/// current result/story dialogue has finished.
/// </summary>
public sealed class ChristmasShopEventSchedule
{
    public const int OrdersBetweenEvents = 2;

    private static readonly ChristmasShopEventType[] EventTypes =
    {
        ChristmasShopEventType.MixedGifts,
        ChristmasShopEventType.RushOrder,
        ChristmasShopEventType.HappyCustomer,
        ChristmasShopEventType.RibbonShortage,
        ChristmasShopEventType.ChristmasSurprise,
        ChristmasShopEventType.FestiveMood
    };

    private readonly System.Random random;
    private readonly HashSet<ChristmasShopEventType> usedEventTypes =
        new HashSet<ChristmasShopEventType>();

    private int nextOrderMilestone = OrdersBetweenEvents;
    private bool hasPendingEvent;
    private ScheduledChristmasShopEvent pendingEvent;
    private bool hasLastEvent;
    private ChristmasShopEventType lastEvent;

    public bool HasPendingEvent => hasPendingEvent;

    public ChristmasShopEventSchedule()
        : this(unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode()))
    {
    }

    /// <summary>Seeded constructor keeps the scheduler reproducible in editor smoke tests.</summary>
    public ChristmasShopEventSchedule(int seed)
    {
        random = new System.Random(seed);
    }

    public bool TryQueue(int successfulOrders, bool hasAnotherOrder)
    {
        if (hasPendingEvent || !hasAnotherOrder || successfulOrders < nextOrderMilestone)
            return false;

        List<ChristmasShopEventType> available = new List<ChristmasShopEventType>();
        for (int i = 0; i < EventTypes.Length; i++)
        {
            if (!usedEventTypes.Contains(EventTypes[i]))
                available.Add(EventTypes[i]);
        }

        // If a future campaign has more checkpoints than event kinds, begin a new
        // shuffle cycle while still avoiding an immediate repeat across the cycle edge.
        if (available.Count == 0)
        {
            usedEventTypes.Clear();
            available.AddRange(EventTypes);
            if (hasLastEvent && available.Count > 1)
                available.Remove(lastEvent);
        }

        ChristmasShopEventType selected = available[random.Next(available.Count)];
        usedEventTypes.Add(selected);
        lastEvent = selected;
        hasLastEvent = true;

        pendingEvent = new ScheduledChristmasShopEvent(selected, nextOrderMilestone);
        hasPendingEvent = true;
        nextOrderMilestone += OrdersBetweenEvents;
        return true;
    }

    public bool TryConsume(out ScheduledChristmasShopEvent scheduledEvent)
    {
        if (!hasPendingEvent)
        {
            scheduledEvent = default(ScheduledChristmasShopEvent);
            return false;
        }

        scheduledEvent = pendingEvent;
        pendingEvent = default(ScheduledChristmasShopEvent);
        hasPendingEvent = false;
        return true;
    }

    public void Reset()
    {
        nextOrderMilestone = OrdersBetweenEvents;
        pendingEvent = default(ScheduledChristmasShopEvent);
        hasPendingEvent = false;
        usedEventTypes.Clear();
        hasLastEvent = false;
        lastEvent = default(ChristmasShopEventType);
    }
}
