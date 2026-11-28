#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates the compact Christmas Eve campaign and progression rewards without opening or editing a scene.
/// Run from Tools/The Last Gift/Run Story Campaign Smoke Test.
/// </summary>
public static class LastGiftStoryCampaignSmokeTest
{
    private const string CampaignAssetPath = "Assets/Resources/StoryCampaign.asset";
    private const int SortingEventReward = 10;

    [MenuItem("Tools/The Last Gift/Run Story Campaign Smoke Test")]
    private static void Run()
    {
        OrderCampaignDefinition campaign =
            AssetDatabase.LoadAssetAtPath<OrderCampaignDefinition>(CampaignAssetPath);
        if (campaign == null)
        {
            ShowFailure("StoryCampaign asset was not found at: " + CampaignAssetPath);
            return;
        }

        string structureError = ValidateCampaign(campaign);
        if (!string.IsNullOrEmpty(structureError))
        {
            ShowFailure(structureError);
            return;
        }

        GameObject testObject = new GameObject("LastGiftStoryCampaignSmokeTest_Temporary");
        testObject.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            OrderProgressionManager manager = testObject.AddComponent<OrderProgressionManager>();
            manager.ConfigureCampaign(campaign);
            LastGiftWorkshopUpgradeManager upgrades =
                testObject.AddComponent<LastGiftWorkshopUpgradeManager>();

            List<GiftOrderDefinition> startedOrders = new List<GiftOrderDefinition>();
            List<OrderCompletionData> completedOrders = new List<OrderCompletionData>();
            GameSummaryData gameSummary = null;
            int daySummaries = 0;

            manager.OrderStarted += (order, showSecretWish) =>
            {
                startedOrders.Add(order);
                Require(!showSecretWish,
                    "The compact story campaign should not add optional secret-wish popups.");
            };
            manager.OrderCompleted += result => completedOrders.Add(result);
            manager.DayCompleted += summary => daySummaries++;
            manager.GameCompleted += summary => gameSummary = summary;

            manager.StartGame();
            bool[] wishAssignments = manager.GetSecretWishAssignments();
            Require(wishAssignments.Length == 4,
                "The story campaign should create four order assignment slots.");
            for (int i = 0; i < wishAssignments.Length; i++)
                Require(!wishAssignments[i], "The short campaign unexpectedly assigned a secret wish.");

            int expectedEarnings = SortingEventReward;
            int processedOrders = 0;
            while (manager.CurrentOrder != null && processedOrders < 4)
            {
                GiftOrderDefinition order = manager.CurrentOrder;
                int giftCount = order.GiftNames == null ? 0 : order.GiftNames.Count;
                Require(giftCount > 0, "Order " + order.name + " has no gifts.");

                expectedEarnings += Mathf.Max(0, order.RewardCoins);
                expectedEarnings += Mathf.Max(0, order.PerfectPackingTip);

                manager.CompleteCurrentOrder(
                    new PackingResult(true, true, giftCount, giftCount),
                    false,
                    false);

                Require(completedOrders.Count == processedOrders + 1,
                    "An order did not produce exactly one completion event.");
                Require(completedOrders[processedOrders].Success,
                    "A valid simulated order did not complete successfully.");

                manager.ContinueAfterResult();
                processedOrders++;

                if (processedOrders == 2)
                    manager.AwardBonusCoins(SortingEventReward);
            }

            Require(processedOrders == 4, "The story campaign did not process exactly four orders.");
            Require(startedOrders.Count == 4, "OrderStarted did not fire exactly four times.");
            Require(manager.IsCompleted, "The one-evening campaign did not reach its completed state.");
            Require(gameSummary != null, "GameCompleted was not raised for the story campaign.");
            Require(daySummaries == 1, "The short story campaign should contain exactly one day.");
            Require(manager.TotalOrdersCompleted == 4, "The total successful order count is incorrect.");
            Require(manager.TotalCoinsEarned == expectedEarnings,
                "Campaign earnings do not include the order rewards and sorting-event bonus.");
            Require(manager.CurrentCoins == expectedEarnings,
                "The final balance does not match the simulated earnings.");
            Require(gameSummary.OrdersCompleted == 4, "The game summary has an incorrect order count.");
            Require(gameSummary.FinalCoins == expectedEarnings, "The game summary balance is incorrect.");

            int balanceBeforeUpgrades = manager.CurrentCoins;
            Require(upgrades.TryPurchase(WorkshopUpgradeType.FestiveGarland),
                "The garland upgrade could not be purchased with sufficient coins.");
            Require(upgrades.HasGarland, "The garland purchase flag was not saved.");
            int afterFirstPurchase = manager.CurrentCoins;
            Require(!upgrades.TryPurchase(WorkshopUpgradeType.FestiveGarland),
                "The same upgrade could be purchased twice.");
            Require(manager.CurrentCoins == afterFirstPurchase,
                "A duplicate upgrade purchase changed the balance.");
            Require(upgrades.TryPurchase(WorkshopUpgradeType.ExtraShelf),
                "The extra-shelf upgrade could not be purchased.");
            Require(upgrades.TryPurchase(WorkshopUpgradeType.PremiumWrapping),
                "The premium-wrapping upgrade could not be purchased.");
            Require(manager.CurrentCoins == balanceBeforeUpgrades
                - LastGiftWorkshopUpgradeManager.GarlandCost
                - LastGiftWorkshopUpgradeManager.ExtraShelfCost
                - LastGiftWorkshopUpgradeManager.PremiumWrappingCost,
                "Upgrade costs were not deducted exactly once.");

            StringBuilderLike report = new StringBuilderLike();
            report.Add("PASSED");
            report.Add("Story campaign: 1 evening, 4 orders, scarf finale");
            report.Add("Order progression and completion events: OK");
            report.Add("Optional secret wishes skipped in the short campaign: OK");
            report.Add("Sorting event bonus recorded in the campaign balance: OK");
            report.Add("Three one-time workshop upgrades and coin deductions: OK");
            Debug.Log("[Last Gift Story Campaign Smoke Test]\n" + report.ToString());
            EditorUtility.DisplayDialog("Last Gift Story Campaign Smoke Test", report.ToString(), "OK");
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

    private static string ValidateCampaign(OrderCampaignDefinition campaign)
    {
        if (campaign.Days == null || campaign.Days.Count != 1)
            return "Expected exactly one Christmas Eve campaign day.";

        OrderDayDefinition day = campaign.Days[0];
        if (day == null || day.DayNumber != 1)
            return "The story campaign day is missing or has an incorrect day number.";
        if (day.Orders == null || day.Orders.Count != 4)
            return "Expected exactly four short story orders.";

        for (int i = 0; i < day.Orders.Count; i++)
        {
            GiftOrderDefinition order = day.Orders[i];
            if (order == null || order.GiftNames == null || order.GiftNames.Count == 0)
                return "Story order " + (i + 1) + " is missing.";
        }

        GiftOrderDefinition finale = day.Orders[3];
        if (!finale.IsStoryFinaleOrder)
            return "The last order is not marked as the story finale.";
        if (finale.GiftNames.Count != 1
            || !string.Equals(finale.GiftNames[0], "ШАРФ", StringComparison.OrdinalIgnoreCase))
            return "The finale must contain only the scarf gift.";
        if (!finale.CustomerWish.Contains("маме-медсестре"))
            return "The scarf order does not mention the boy's nurse mother.";

        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ShowFailure(string message)
    {
        Debug.LogError("[Last Gift Story Campaign Smoke Test] FAILED: " + message);
        EditorUtility.DisplayDialog("Last Gift Story Campaign Smoke Test", "FAILED\n\n" + message, "OK");
    }

    /// <summary>A tiny report builder avoids introducing any project package dependency.</summary>
    private sealed class StringBuilderLike
    {
        private readonly List<string> lines = new List<string>();
        public void Add(string line) => lines.Add(line);
        public override string ToString() => string.Join("\n", lines);
    }
}

/// <summary>
/// Exercises the optional customer queue against the existing short story campaign.
/// Run from Tools/The Last Gift/Run Order Queue Smoke Test.
/// </summary>
public static class LastGiftOrderQueueSmokeTest
{
    private const string CampaignAssetPath = "Assets/Resources/StoryCampaign.asset";
    private const int ExpectedUrgencyBonus = 8;

    [MenuItem("Tools/The Last Gift/Run Order Queue Smoke Test")]
    private static void Run()
    {
        OrderCampaignDefinition campaign =
            AssetDatabase.LoadAssetAtPath<OrderCampaignDefinition>(CampaignAssetPath);
        if (campaign == null)
        {
            ShowFailure("StoryCampaign asset was not found at: " + CampaignAssetPath);
            return;
        }

        GameObject testObject = new GameObject("LastGiftOrderQueueSmokeTest_Temporary");
        testObject.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            OrderProgressionManager manager = testObject.AddComponent<OrderProgressionManager>();
            manager.ConfigureCampaign(campaign);
            manager.ConfigureOrderQueue(true, 3, 120f, ExpectedUrgencyBonus);

            List<GiftOrderDefinition> startedOrders = new List<GiftOrderDefinition>();
            List<OrderCompletionData> completionResults = new List<OrderCompletionData>();
            GameSummaryData gameSummary = null;
            int daySummaries = 0;

            manager.OrderStarted += (order, showSecretWish) =>
            {
                startedOrders.Add(order);
                Require(!showSecretWish, "The compact story campaign should not add optional secret-wish popups.");
            };
            manager.OrderCompleted += result => completionResults.Add(result);
            manager.DayCompleted += summary => daySummaries++;
            manager.GameCompleted += summary => gameSummary = summary;

            manager.StartGame();
            Require(manager.OrderQueueEnabled, "The queue was not enabled.");
            Require(manager.CurrentOrder == null, "A queued campaign must wait for a player selection.");
            Require(manager.WaitingOrders.Count == 3, "The workshop should initially show three waiting customers.");

            for (int i = 0; i < manager.WaitingOrders.Count; i++)
            {
                Require(manager.WaitingOrders[i] != null && manager.WaitingOrders[i].Order != null,
                    "The queue contains an empty customer entry.");
                Require(!manager.WaitingOrders[i].Order.IsStoryFinaleOrder,
                    "The scarf finale appeared before the three regular orders were completed.");
            }

            Require(manager.WaitingOrders[0].WaitedSeconds != manager.WaitingOrders[1].WaitedSeconds
                && manager.WaitingOrders[1].WaitedSeconds != manager.WaitingOrders[2].WaitedSeconds,
                "Waiting customers should have distinct displayed waiting times.");

            OrderQueueEntry urgentEntry = null;
            for (int i = 0; i < manager.WaitingOrders.Count; i++)
            {
                if (manager.WaitingOrders[i].IsUrgent)
                {
                    urgentEntry = manager.WaitingOrders[i];
                    break;
                }
            }
            Require(urgentEntry != null, "The opening queue should include one urgent customer.");

            int expectedEarnings = 0;
            GiftOrderDefinition urgentOrder = urgentEntry.Order;
            Require(manager.SelectQueuedOrder(urgentOrder), "The urgent customer could not be selected.");
            Require(manager.CurrentOrder == urgentOrder, "The selected urgent order did not become active.");
            CompleteSelectedOrder(manager, completionResults, urgentOrder, ref expectedEarnings);
            Require(completionResults[0].UrgencyBonus == ExpectedUrgencyBonus,
                "A timely urgent order did not award its full bonus.");

            int balanceAfterUrgentCompletion = manager.CurrentCoins;
            manager.CompleteCurrentOrder(
                new PackingResult(true, true, urgentOrder.GiftNames.Count, urgentOrder.GiftNames.Count));
            Require(manager.CurrentCoins == balanceAfterUrgentCompletion,
                "Repeating completion awarded coins more than once.");
            Require(completionResults.Count == 1, "Repeating completion raised a second result event.");

            manager.ContinueAfterResult();
            Require(manager.CurrentOrder == null, "The queue should return to customer selection after a result.");

            int regularOrdersCompleted = 1;
            while (regularOrdersCompleted < 3)
            {
                GiftOrderDefinition nextRegularOrder = null;
                for (int i = 0; i < manager.WaitingOrders.Count; i++)
                {
                    OrderQueueEntry entry = manager.WaitingOrders[i];
                    if (entry != null && entry.Order != null && !entry.Order.IsStoryFinaleOrder)
                    {
                        nextRegularOrder = entry.Order;
                        break;
                    }
                }

                Require(nextRegularOrder != null, "A regular order disappeared before completion.");
                Require(manager.SelectQueuedOrder(nextRegularOrder), "A waiting regular order could not be selected.");
                CompleteSelectedOrder(manager, completionResults, nextRegularOrder, ref expectedEarnings);
                manager.ContinueAfterResult();
                regularOrdersCompleted++;
            }

            Require(manager.WaitingOrders.Count == 1,
                "The scarf finale should be the only order shown after all regular orders are complete.");
            OrderQueueEntry finaleEntry = manager.WaitingOrders[0];
            Require(finaleEntry.Order != null && finaleEntry.Order.IsStoryFinaleOrder,
                "The scarf finale did not appear at the expected point.");
            Require(finaleEntry.Order.GiftNames.Count == 1
                && string.Equals(finaleEntry.Order.GiftNames[0], "ШАРФ", StringComparison.OrdinalIgnoreCase),
                "The final story order must contain only the scarf.");

            Require(manager.SelectQueuedOrder(finaleEntry.Order), "The scarf finale could not be selected.");
            CompleteSelectedOrder(manager, completionResults, finaleEntry.Order, ref expectedEarnings);
            manager.ContinueAfterResult();

            Require(manager.IsCompleted, "The queued campaign did not reach its completed state.");
            Require(manager.WaitingOrders.Count == 0, "A completed order remained in the queue.");
            Require(startedOrders.Count == 4, "Expected exactly four selected orders.");
            Require(completionResults.Count == 4, "Expected exactly four completion results.");
            Require(daySummaries == 1, "The short story campaign should contain exactly one day.");
            Require(manager.TotalOrdersCompleted == 4, "The successful order count is incorrect.");
            Require(manager.TotalCoinsEarned == expectedEarnings,
                "The campaign total does not include the existing and urgency rewards exactly once.");
            Require(manager.CurrentCoins == expectedEarnings, "The final balance is incorrect.");
            Require(gameSummary != null && gameSummary.OrdersCompleted == 4,
                "The final game summary has an incorrect order count.");
            Require(gameSummary.FinalCoins == expectedEarnings, "The final game summary balance is incorrect.");

            EditorUtility.DisplayDialog(
                "Last Gift Order Queue Smoke Test",
                "PASSED\n\nThree customers can wait together and be selected in any order.\n"
                + "The urgent bonus is awarded once when completed in time.\n"
                + "Regular orders remain available; the scarf finale appears after them.\n"
                + "All four orders complete through the existing progression and reward system.",
                "OK");
            Debug.Log("[Last Gift Order Queue Smoke Test] PASSED");
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

    private static void CompleteSelectedOrder(OrderProgressionManager manager,
        List<OrderCompletionData> completionResults, GiftOrderDefinition order, ref int expectedEarnings)
    {
        Require(order != null && order.GiftNames != null && order.GiftNames.Count > 0,
            "The selected order has no required gifts.");

        int previousCount = completionResults.Count;
        manager.CompleteCurrentOrder(
            new PackingResult(true, true, order.GiftNames.Count, order.GiftNames.Count));

        Require(completionResults.Count == previousCount + 1,
            "A selected order did not produce exactly one completion event.");
        OrderCompletionData result = completionResults[completionResults.Count - 1];
        Require(result.Success, "A valid simulated order did not complete successfully.");
        Require(result.Order == order, "The completion event belongs to a different customer.");
        expectedEarnings += result.TotalEarned;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ShowFailure(string message)
    {
        Debug.LogError("[Last Gift Order Queue Smoke Test] FAILED: " + message);
        EditorUtility.DisplayDialog("Last Gift Order Queue Smoke Test", "FAILED\n\n" + message, "OK");
    }
}

#endif
