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
#endif
