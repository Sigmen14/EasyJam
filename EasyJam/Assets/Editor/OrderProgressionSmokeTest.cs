#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs the order progression flow against the real campaign asset without changing a scene.
/// The first order is deliberately failed; all remaining orders are simulated as successful.
/// </summary>
public static class OrderProgressionSmokeTest
{
    private const string CampaignAssetPath = "Assets/Data/Orders/ChristmasCampaign.asset";

    [MenuItem("Tools/The Last Gift/Run Order Progression Smoke Test")]
    private static void Run()
    {
        OrderCampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<OrderCampaignDefinition>(CampaignAssetPath);
        if (campaign == null)
        {
            ShowFailure("Campaign asset was not found at: " + CampaignAssetPath);
            return;
        }

        string structureError = ValidateCampaignStructure(campaign, out int totalOrders, out int expectedCoins);
        if (!string.IsNullOrEmpty(structureError))
        {
            ShowFailure(structureError);
            return;
        }

        GameObject testObject = new GameObject("OrderProgressionSmokeTest_Temporary");
        testObject.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            OrderProgressionManager manager = testObject.AddComponent<OrderProgressionManager>();
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("campaign").objectReferenceValue = campaign;
            serializedManager.FindProperty("startingCoins").intValue = 0;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            List<GiftOrderDefinition> startedOrders = new List<GiftOrderDefinition>();
            List<OrderCompletionData> completedOrders = new List<OrderCompletionData>();
            List<DaySummaryData> completedDays = new List<DaySummaryData>();
            GameSummaryData gameSummary = null;

            manager.OrderStarted += order => startedOrders.Add(order);
            manager.OrderCompleted += result => completedOrders.Add(result);
            manager.DayCompleted += summary => completedDays.Add(summary);
            manager.GameCompleted += summary => gameSummary = summary;

            manager.StartGame();

            ChristmasShopEventSchedule eventSchedule = new ChristmasShopEventSchedule();
            Require(!eventSchedule.TryQueue(2, true),
                "A Christmas event was queued before three successful orders.");
            Require(eventSchedule.TryQueue(3, true),
                "A Christmas event was not queued at the third successful order.");
            Require(!eventSchedule.TryQueue(3, true),
                "The scheduler queued a second event while one was already pending.");
            Require(eventSchedule.TryConsume(out ScheduledChristmasShopEvent firstEvent)
                && firstEvent.Type == ChristmasShopEventType.MixedGifts
                && firstEvent.OrderMilestone == 3,
                "The first queued event has an incorrect type or order milestone.");
            Require(!eventSchedule.TryQueue(3, true),
                "The scheduler repeated the same three-order event.");
            Require(eventSchedule.TryQueue(6, true),
                "The next event was not queued at six successful orders.");
            Require(eventSchedule.TryConsume(out ScheduledChristmasShopEvent secondEvent)
                && secondEvent.OrderMilestone == 6,
                "The second event has an incorrect order milestone.");
            Require(!eventSchedule.TryQueue(9, false),
                "An event was queued after the final order when no next customer exists.");
            eventSchedule.Reset();
            Require(!eventSchedule.HasPendingEvent,
                "Reset did not clear the pending Christmas event.");

            int attemptIndex = 0;
            while (manager.CurrentOrder != null && attemptIndex < totalOrders)
            {
                GiftOrderDefinition currentOrder = manager.CurrentOrder;
                int giftCount = currentOrder.GiftNames == null ? 0 : currentOrder.GiftNames.Count;
                bool deliberatelyFail = attemptIndex == 0;

                PackingResult result = deliberatelyFail
                    ? new PackingResult(false, false, 0, giftCount)
                    : new PackingResult(true, true, giftCount, giftCount);

                manager.CompleteCurrentOrder(result);

                Require(completedOrders.Count == attemptIndex + 1,
                    "OrderCompleted did not fire exactly once for order " + (attemptIndex + 1) + ".");

                OrderCompletionData completion = completedOrders[completedOrders.Count - 1];
                Require(completion.Order == currentOrder,
                    "OrderCompleted returned a different order than the active order.");

                if (deliberatelyFail)
                {
                    Require(!completion.Success, "The deliberately failed first order was marked as successful.");
                    Require(completion.TotalEarned == 0, "The failed first order incorrectly awarded coins.");
                }
                else
                {
                    Require(completion.Success, "A simulated successful order was marked as failed: " + currentOrder.name);
                    Require(completion.TotalEarned == Mathf.Max(0, currentOrder.RewardCoins)
                        + Mathf.Max(0, currentOrder.PerfectPackingTip),
                        "The reward or perfect-packing tip is incorrect for " + currentOrder.name + ".");
                }

                manager.ContinueAfterResult();
                attemptIndex++;
            }

            Require(manager.CurrentOrder == null, "The campaign did not finish after all configured orders.");
            Require(attemptIndex == totalOrders, "The test did not process every configured order.");
            Require(startedOrders.Count == totalOrders, "OrderStarted fired " + startedOrders.Count
                + " times instead of " + totalOrders + ".");
            Require(completedOrders.Count == totalOrders, "Not all orders produced a completion result.");
            Require(completedDays.Count == campaign.Days.Count, "DayCompleted did not fire once per campaign day.");
            Require(gameSummary != null, "GameCompleted did not fire after the final day.");

            int expectedSuccessfulOrders = totalOrders - 1;
            Require(manager.TotalOrdersCompleted == expectedSuccessfulOrders,
                "Successful order count is incorrect. Expected " + expectedSuccessfulOrders
                + ", received " + manager.TotalOrdersCompleted + ".");
            Require(manager.TotalCoinsEarned == expectedCoins,
                "Total earned coins are incorrect. Expected " + expectedCoins
                + ", received " + manager.TotalCoinsEarned + ".");
            Require(manager.TotalTipsEarned == SumTipsForSuccessfulOrders(campaign),
                "Perfect-packing tip total is incorrect.");
            Require(manager.CurrentCoins == expectedCoins, "Final coin balance is incorrect.");
            Require(gameSummary.OrdersCompleted == expectedSuccessfulOrders, "Game summary has an incorrect order count.");
            Require(gameSummary.TotalCoinsEarned == expectedCoins, "Game summary has an incorrect coin total.");
            Require(gameSummary.FinalCoins == expectedCoins, "Game summary has an incorrect final balance.");
            Require(gameSummary.DaysPlayed == campaign.Days.Count, "Game summary has an incorrect number of days.");

            for (int i = 0; i < completedDays.Count; i++)
            {
                int expectedCompleted = campaign.Days[i].Orders.Count - (i == 0 ? 1 : 0);
                Require(completedDays[i].Day == campaign.Days[i].DayNumber,
                    "Day summary order is incorrect at index " + i + ".");
                Require(completedDays[i].OrdersTotal == campaign.Days[i].Orders.Count,
                    "Incorrect order total in day " + completedDays[i].Day + ".");
                Require(completedDays[i].OrdersCompleted == expectedCompleted,
                    "Incorrect successful order count in day " + completedDays[i].Day + ".");
            }

            manager.RestartGame();

            Require(manager.TryAwardEventReward("smoke-test-mixed-gifts", 30),
                "A valid event reward was not granted.");
            int balanceAfterEventReward = manager.CurrentCoins;
            Require(balanceAfterEventReward == 30,
                "The event reward was not added to the coin balance.");
            Require(manager.TotalCoinsEarned == 30,
                "The event reward was not included in campaign earnings.");
            Require(!manager.TryAwardEventReward("smoke-test-mixed-gifts", 30),
                "The same event reward was granted twice.");
            Require(manager.CurrentCoins == balanceAfterEventReward,
                "A duplicate event reward changed the coin balance.");

            manager.RestartGame();
            Require(manager.CurrentCoins == 0 && manager.TotalCoinsEarned == 0,
                "Restart did not clear event rewards and campaign earnings.");
            Require(!manager.IsCompleted, "Restart did not clear the completed state.");
            Require(manager.CurrentDay == campaign.Days[0].DayNumber,
                "Restart did not return to day one.");
            Require(manager.CurrentOrderNumber == 1,
                "Restart did not return to the first order.");
            Require(manager.CurrentOrder == campaign.Days[0].Orders[0],
                "Restart did not restore the first campaign order.");
            Require(manager.CurrentCoins == 0 && manager.TotalOrdersCompleted == 0,
                "Restart did not reset the campaign balance and statistics.");

            StringBuilder report = new StringBuilder();
            report.AppendLine("PASSED");
            report.AppendLine("Campaign: " + campaign.name);
            report.AppendLine("Days checked: " + campaign.Days.Count);
            report.AppendLine("Orders checked: " + totalOrders);
            report.AppendLine("Deliberately failed orders: 1 (no reward awarded)");
            report.AppendLine("Successful orders: " + expectedSuccessfulOrders);
            report.AppendLine("Final coin balance: " + expectedCoins);
            report.AppendLine("Day transitions and campaign completion: OK");
            report.AppendLine("Restart campaign: OK");
            report.AppendLine("Christmas event cadence (orders 3 and 6): OK");
            report.AppendLine("Event reward and duplicate protection: OK");

            Debug.Log("[Order Progression Smoke Test]\n" + report);
            EditorUtility.DisplayDialog("Order Progression Smoke Test", report.ToString(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowFailure(exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static string ValidateCampaignStructure(
        OrderCampaignDefinition campaign,
        out int totalOrders,
        out int expectedCoins)
    {
        totalOrders = 0;
        expectedCoins = 0;

        if (campaign.Days == null || campaign.Days.Count != 3)
            return "Expected a campaign with exactly 3 days.";

        bool firstOrder = true;
        for (int dayIndex = 0; dayIndex < campaign.Days.Count; dayIndex++)
        {
            OrderDayDefinition day = campaign.Days[dayIndex];
            if (day == null)
                return "Campaign day " + (dayIndex + 1) + " is missing.";

            if (day.DayNumber != dayIndex + 1)
                return "Expected day number " + (dayIndex + 1) + ", found " + day.DayNumber + ".";

            if (day.Orders == null || day.Orders.Count != 3)
                return "Expected exactly 3 orders on day " + day.DayNumber + ".";

            for (int orderIndex = 0; orderIndex < day.Orders.Count; orderIndex++)
            {
                GiftOrderDefinition order = day.Orders[orderIndex];
                if (order == null)
                    return "An order is missing on day " + day.DayNumber + ", position " + (orderIndex + 1) + ".";

                if (order.Day != day.DayNumber)
                    return "Order " + order.name + " has an incorrect day number.";
                if (string.IsNullOrWhiteSpace(order.CustomerName) || string.IsNullOrWhiteSpace(order.CustomerWish))
                    return "Order " + order.name + " is missing its Russian customer name or wish.";

                if (order.GiftNames == null || order.GiftNames.Count == 0)
                    return "Order " + order.name + " does not contain any gifts.";

                string[] availableGifts = { "МИШКА", "ПОДАРОК", "ЛЕДЕНЕЦ", "СНЕЖНЫЙ ШАР" };
                for (int giftIndex = 0; giftIndex < order.GiftNames.Count; giftIndex++)
                {
                    if (Array.IndexOf(availableGifts, order.GiftNames[giftIndex]) < 0)
                        return "Order " + order.name + " contains an unknown shelf gift: " + order.GiftNames[giftIndex] + ".";
                }

                int boxCapacity = order.BoxSize == GiftBoxSize.Small ? 4
                    : order.BoxSize == GiftBoxSize.Medium ? 6 : 9;
                if (order.GiftNames.Count > boxCapacity)
                    return "Order " + order.name + " does not fit its configured box size.";

                totalOrders++;
                if (!firstOrder)
                {
                    expectedCoins += Mathf.Max(0, order.RewardCoins);
                    expectedCoins += Mathf.Max(0, order.PerfectPackingTip);
                }

                firstOrder = false;
            }
        }

        return null;
    }

    private static int SumTipsForSuccessfulOrders(OrderCampaignDefinition campaign)
    {
        int total = 0;
        bool firstOrder = true;

        foreach (OrderDayDefinition day in campaign.Days)
        {
            foreach (GiftOrderDefinition order in day.Orders)
            {
                if (!firstOrder)
                    total += Mathf.Max(0, order.PerfectPackingTip);

                firstOrder = false;
            }
        }

        return total;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ShowFailure(string message)
    {
        Debug.LogError("[Order Progression Smoke Test] FAILED: " + message);
        EditorUtility.DisplayDialog("Order Progression Smoke Test", "FAILED\n\n" + message, "OK");
    }
}
#endif
