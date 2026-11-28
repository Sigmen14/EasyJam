using System;

/// <summary>Event kinds can be extended as additional Christmas shop activities are added.</summary>
public enum ChristmasShopEventType
{
    MixedGifts
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
/// Queues one short event after each two successful orders. Scheduling and consuming
/// are separate so the caller can wait until result/day-summary screens have closed.
/// </summary>
public sealed class ChristmasShopEventSchedule
{
    public const int OrdersBetweenEvents = 2;

    private int nextOrderMilestone = OrdersBetweenEvents;
    private bool hasPendingEvent;
    private ScheduledChristmasShopEvent pendingEvent;

    public bool HasPendingEvent => hasPendingEvent;

    public bool TryQueue(int successfulOrders, bool hasAnotherOrder)
    {
        if (hasPendingEvent || !hasAnotherOrder || successfulOrders < nextOrderMilestone)
            return false;

        pendingEvent = new ScheduledChristmasShopEvent(
            ChristmasShopEventType.MixedGifts, nextOrderMilestone);
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
    }
}
