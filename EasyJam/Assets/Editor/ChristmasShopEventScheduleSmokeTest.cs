#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Editor smoke test for randomized event intervals, variety and campaign reset.</summary>
public static class ChristmasShopEventScheduleSmokeTest
{
    [MenuItem("The Last Gift/Run Random Event Schedule Smoke Test")]
    private static void Run()
    {
        try
        {
            ChristmasShopEventSchedule schedule = new ChristmasShopEventSchedule(20261128);
            List<ChristmasShopEventType> observed = new List<ChristmasShopEventType>();
            int[] milestones = { 2, 4, 6, 8, 10, 12 };

            Require(!schedule.TryQueue(1, true),
                "An event was queued after only one successful order.");

            for (int i = 0; i < milestones.Length; i++)
            {
                int milestone = milestones[i];
                Require(schedule.TryQueue(milestone, true),
                    "The event was not queued at milestone " + milestone + ".");
                Require(schedule.HasPendingEvent,
                    "The scheduler did not mark the queued event as pending.");

                ScheduledChristmasShopEvent scheduled;
                Require(schedule.TryConsume(out scheduled),
                    "The queued event could not be consumed.");
                Require(scheduled.OrderMilestone == milestone,
                    "The event was associated with the wrong order milestone.");
                Require(!observed.Contains(scheduled.Type),
                    "An event type repeated before the event pool was exhausted.");
                observed.Add(scheduled.Type);
                Require(!schedule.HasPendingEvent,
                    "The pending flag was not cleared after consuming the event.");

                Require(!schedule.TryQueue(milestone + 1, true),
                    "A second event was queued one order after the previous event.");
            }

            Require(observed.Count == 6,
                "The scheduler did not expose all six event types in the seeded run.");
            Require(!schedule.TryQueue(14, false),
                "An event was queued despite there being no next order.");

            schedule.Reset();
            Require(!schedule.HasPendingEvent,
                "Reset left a queued event behind.");
            Require(schedule.TryQueue(2, true),
                "Reset did not restore the first event milestone.");

            Debug.Log("[Christmas Shop Event Schedule Smoke Test] PASSED: " +
                "two-order interval, six unique event types, no event after the final order, reset.");
            EditorUtility.DisplayDialog(
                "Christmas Event Schedule Smoke Test",
                "PASSED\n\nTwo-order spacing: OK\nSix unique event types before repeat: OK\nNo event after the final order: OK\nCampaign reset: OK",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Christmas Event Schedule Smoke Test",
                "FAILED\n\n" + exception.Message,
                "OK");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
