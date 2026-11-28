#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TheLastGift.UI;

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
            List<bool> uiReceivedAssignments = new List<bool>();
            List<OrderCompletionData> completedOrders = new List<OrderCompletionData>();
            int secretWishScreenCalls = 0;
            int regularGameplayScreenCalls = 0;
            List<DaySummaryData> completedDays = new List<DaySummaryData>();
            GameSummaryData gameSummary = null;

            manager.OrderStarted += (order, showSecretWish) =>
            {
                // Use the same forwarding method as OrderProgressionUIAdapter.HandleOrderStarted.
                OrderProgressionUIAdapter.ApplyOrderAssignmentToUI(
                    (uiOrder, day, orderNumber, balance, reputation, uiShowSecretWish) =>
                    {
                        startedOrders.Add(uiOrder);
                        uiReceivedAssignments.Add(uiShowSecretWish);
                    },
                    order,
                    manager.CurrentDay,
                    manager.CurrentOrderNumber,
                    manager.CurrentCoins,
                    manager.TotalReputationEarned,
                    showSecretWish);
            };
            manager.OrderCompleted += result => completedOrders.Add(result);
            manager.DayCompleted += summary => completedDays.Add(summary);
            manager.GameCompleted += summary => gameSummary = summary;

            ValidateSecretWishSeedVariations();
            manager.StartGame();

            bool[] activeSequence = manager.GetSecretWishAssignments();
            Require(OrderProgressionManager.IsValidSecretWishSequence(activeSequence),
                "The generated secret-wish sequence is invalid.");
            Require(CountSecretWishes(activeSequence) == 4,
                "A new campaign must assign exactly four secret wishes.");
            expectedCoins += SumAssignedSecretWishBonuses(campaign, activeSequence);

            string initialSequenceKey = SequenceKey(activeSequence);
            manager.StartGame();
            Require(SequenceKey(manager.GetSecretWishAssignments()) == initialSequenceKey,
                "Calling StartGame during an active run changed the secret-wish sequence.");

            int attemptIndex = 0;
            while (manager.CurrentOrder != null && attemptIndex < totalOrders)
            {
                GiftOrderDefinition currentOrder = manager.CurrentOrder;
                int giftCount = currentOrder.GiftNames == null ? 0 : currentOrder.GiftNames.Count;
                bool deliberatelyFail = attemptIndex == 0;
                bool assignedWish = manager.IsSecretWishAssignedForCurrentOrder;

                Require(assignedWish == activeSequence[attemptIndex],
                    "The current order's secret-wish assignment does not match the fixed campaign sequence.");
                Require(SequenceKey(manager.GetSecretWishAssignments()) == initialSequenceKey,
                    "The secret-wish sequence changed during campaign progression.");
                Require(uiReceivedAssignments.Count == attemptIndex + 1,
                    "OrderStarted did not deliver an assignment for the current order.");

                bool uiAssignment = uiReceivedAssignments[attemptIndex];
                Require(uiAssignment == assignedWish,
                    "The assignment delivered to the UI differs from the manager's current assignment.");

                SecretCustomerWishDefinition uiWishData = currentOrder.SecretWish;
                bool hasValidUiWishData = uiWishData != null
                    && !string.IsNullOrWhiteSpace(uiWishData.Clue)
                    && uiWishData.GiftOptions != null
                    && uiWishData.GiftOptions.Count >= 2
                    && uiWishData.GiftOptions.Count <= 3
                    && !string.IsNullOrWhiteSpace(uiWishData.CorrectGiftName);

                bool uiSelectedSecretWishScreen = LastGiftUIController.DispatchGameplayScreen(
                    uiAssignment,
                    hasValidUiWishData,
                    false,
                    () =>
                    {
                        secretWishScreenCalls++;
                        manager.MarkCurrentSecretWishShown();
                    },
                    () => regularGameplayScreenCalls++);

                Require(uiSelectedSecretWishScreen == assignedWish,
                    "Gameplay selected the wrong screen for customer " + currentOrder.CustomerName + ".");
                int balanceBeforeSecretWish = manager.CurrentCoins;
                if (assignedWish)
                {
                    Require(manager.HasSecretWishBeenShownForCurrentOrder,
                        "An assigned secret wish was not recorded as displayed.");

                    int wrongAnswerReward = manager.AwardSecretWishBonus(false, currentOrder.SecretWish.BonusCoins);
                    Require(wrongAnswerReward == 0 && manager.CurrentCoins == balanceBeforeSecretWish,
                        "An incorrect secret-wish answer awarded coins.");

                    int expectedWishBonus = Mathf.Max(0, currentOrder.SecretWish.BonusCoins);
                    int wishBonus = manager.AwardSecretWishBonus(true, currentOrder.SecretWish.BonusCoins);
                    Require(wishBonus == expectedWishBonus,
                        "A correct secret-wish answer awarded an incorrect bonus.");
                    Require(manager.AwardSecretWishBonus(true, currentOrder.SecretWish.BonusCoins) == 0,
                        "The secret-wish bonus was awarded more than once.");
                }
                else
                {
                    Require(!manager.HasSecretWishBeenShownForCurrentOrder,
                        "An unassigned customer's secret wish was marked as displayed.");
                    Require(manager.AwardSecretWishBonus(true, currentOrder.SecretWish.BonusCoins) == 0,
                        "An unassigned secret wish awarded a bonus.");
                    Require(manager.CurrentCoins == balanceBeforeSecretWish,
                        "An unassigned secret wish changed the coin balance.");
                }

                PackingResult result = deliberatelyFail
                    ? new PackingResult(false, false, 0, giftCount)
                    : new PackingResult(true, true, giftCount, giftCount);

                bool suitableWrapping = !deliberatelyFail && attemptIndex != 1;
                bool perfectWrapping = suitableWrapping && attemptIndex != 2;
                manager.CompleteCurrentOrder(result, suitableWrapping, perfectWrapping);

                int coinsAfterFirstCompletion = manager.CurrentCoins;
                int reputationAfterFirstCompletion = manager.TotalReputationEarned;
                manager.CompleteCurrentOrder(result, true, true);
                Require(manager.CurrentCoins == coinsAfterFirstCompletion,
                    "Repeated completion awarded coins more than once for order " + (attemptIndex + 1) + ".");
                Require(manager.TotalReputationEarned == reputationAfterFirstCompletion,
                    "Repeated completion awarded reputation more than once for order " + (attemptIndex + 1) + ".");

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
                    int expectedWrappingBonus = suitableWrapping ? Mathf.Max(0, currentOrder.WrappingBonusCoins) : 0;
                    int expectedReputationBonus = perfectWrapping
                        ? Mathf.Max(0, currentOrder.PerfectWrappingReputationBonus) : 0;
                    Require(completion.TotalEarned == Mathf.Max(0, currentOrder.RewardCoins)
                        + Mathf.Max(0, currentOrder.PerfectPackingTip) + expectedWrappingBonus,
                        "The order reward, packing tip or wrapping bonus is incorrect for " + currentOrder.name + ".");
                    Require(completion.WrappingBonus == expectedWrappingBonus,
                        "Wrapping bonus did not respect the selected wrapping quality for " + currentOrder.name + ".");
                    Require(completion.ReputationBonus == expectedReputationBonus,
                        "The perfect-wrapping reputation bonus is incorrect for " + currentOrder.name + ".");
                }

                manager.ContinueAfterResult();
                attemptIndex++;
            }

            Require(manager.CurrentOrder == null, "The campaign did not finish after all configured orders.");
            Require(attemptIndex == totalOrders, "The test did not process every configured order.");
            Require(startedOrders.Count == totalOrders, "OrderStarted fired " + startedOrders.Count
                + " times instead of " + totalOrders + ".");
            Require(uiReceivedAssignments.Count == totalOrders,
                "The UI did not receive an assignment for every order.");
            Require(CountSecretWishes(uiReceivedAssignments) == 4,
                "The UI must receive exactly four true assignments across the nine orders.");
            Require(totalOrders - CountSecretWishes(uiReceivedAssignments) == 5,
                "The UI must receive exactly five false assignments across the nine orders.");
            Require(secretWishScreenCalls == 4,
                "The shared Gameplay route must call ShowSecretWish exactly four times.");
            Require(regularGameplayScreenCalls == 5,
                "The shared Gameplay route must select ordinary gameplay exactly five times.");
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
            Require(manager.TotalReputationEarned == SumWrappingReputationBonusesForSuccessfulOrders(campaign),
                "Perfect-wrapping reputation total is incorrect.");
            Require(manager.CurrentCoins == expectedCoins, "Final coin balance is incorrect.");
            Require(gameSummary.OrdersCompleted == expectedSuccessfulOrders, "Game summary has an incorrect order count.");
            Require(gameSummary.TotalCoinsEarned == expectedCoins, "Game summary has an incorrect coin total.");
            Require(gameSummary.FinalCoins == expectedCoins, "Game summary has an incorrect final balance.");
            Require(gameSummary.ReputationEarned == manager.TotalReputationEarned,
                "Game summary has an incorrect reputation total.");
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
            bool[] restartedSequence = manager.GetSecretWishAssignments();
            Require(OrderProgressionManager.IsValidSecretWishSequence(restartedSequence),
                "Restart generated an invalid secret-wish sequence.");
            Require(CountSecretWishes(restartedSequence) == 4,
                "Restart did not assign exactly four secret wishes.");
            Require(SequenceKey(restartedSequence) != initialSequenceKey,
                "Restart reused the previous secret-wish sequence.");
            Require(!manager.IsCompleted, "Restart did not clear the completed state.");
            Require(manager.CurrentDay == campaign.Days[0].DayNumber,
                "Restart did not return to day one.");
            Require(manager.CurrentOrderNumber == 1,
                "Restart did not return to the first order.");
            Require(manager.CurrentOrder == campaign.Days[0].Orders[0],
                "Restart did not restore the first campaign order.");
            Require(manager.CurrentCoins == 0 && manager.TotalOrdersCompleted == 0
                && manager.TotalReputationEarned == 0,
                "Restart did not reset the campaign balance, reputation and statistics.");

            StringBuilder report = new StringBuilder();
            report.AppendLine("PASSED");
            report.AppendLine("Campaign: " + campaign.name);
            report.AppendLine("Days checked: " + campaign.Days.Count);
            report.AppendLine("Orders checked: " + totalOrders);
            report.AppendLine("Deliberately failed orders: 1 (no reward awarded)");
            report.AppendLine("Successful orders: " + expectedSuccessfulOrders);
            report.AppendLine("Final coin balance: " + expectedCoins);
            report.AppendLine("Perfect wrapping reputation: " + manager.TotalReputationEarned);
            report.AppendLine("Unsuitable wrapping still completes the order without a bonus: OK");
            report.AppendLine("Suitable wrapping awards coins; perfect wrapping awards reputation: OK");
            report.AppendLine("Repeated completion does not duplicate rewards: OK");
            report.AppendLine("Day transitions and campaign completion: OK");
            report.AppendLine("Manager -> OrderStarted -> UI assignment: 4 true, 5 false across all 9 orders: OK");
            report.AppendLine("Gameplay dispatch: ShowSecretWish 4 calls; ordinary gameplay 5 calls: OK");
            report.AppendLine("Secret wishes: exactly 4 per run, at least 1 each day, max 2 consecutive: OK");
            report.AppendLine("Secret-wish assignments remain fixed during the run and across day transitions: OK");
            report.AppendLine("Different seeds produce different valid sequences: OK");
            report.AppendLine("Restart selects a new secret-wish sequence: OK");
            report.AppendLine("Only displayed, assigned wishes can award a one-time bonus: OK");
            report.AppendLine("Restart campaign: OK");

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

                SecretCustomerWishDefinition wish = order.SecretWish;
                if (wish == null || string.IsNullOrWhiteSpace(wish.Clue))
                    return "Customer '" + order.CustomerName + "' has no configured secret wish.";

                if (wish.GiftOptions == null || wish.GiftOptions.Count < 2 || wish.GiftOptions.Count > 3)
                    return "Secret wish for customer '" + order.CustomerName + "' must contain 2–3 answer options.";

                bool correctAnswerInOptions = false;
                for (int optionIndex = 0; optionIndex < wish.GiftOptions.Count; optionIndex++)
                {
                    string option = wish.GiftOptions[optionIndex];
                    if (string.IsNullOrWhiteSpace(option))
                        return "Secret wish for customer '" + order.CustomerName + "' contains an empty answer option.";

                    option = option.Trim();
                    bool optionExists = false;
                    for (int catalogIndex = 0; catalogIndex < availableGifts.Length; catalogIndex++)
                    {
                        if (string.Equals(option, availableGifts[catalogIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            optionExists = true;
                            break;
                        }
                    }

                    if (!optionExists)
                        return "Secret wish for customer '" + order.CustomerName
                            + "' contains a gift outside the catalog: '" + option + "'.";

                    for (int previousIndex = 0; previousIndex < optionIndex; previousIndex++)
                    {
                        if (string.Equals(option, wish.GiftOptions[previousIndex]?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                            return "Secret wish for customer '" + order.CustomerName
                                + "' contains duplicate answer option '" + option + "'.";
                    }

                    if (string.Equals(option, wish.CorrectGiftName?.Trim(), StringComparison.OrdinalIgnoreCase))
                        correctAnswerInOptions = true;
                }

                if (!correctAnswerInOptions)
                    return "Correct gift for customer '" + order.CustomerName
                        + "' is not included in the secret-wish answer options.";

                bool correctGiftInOrder = false;
                for (int giftIndex = 0; giftIndex < order.GiftNames.Count; giftIndex++)
                {
                    if (string.Equals(order.GiftNames[giftIndex]?.Trim(), wish.CorrectGiftName?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        correctGiftInOrder = true;
                        break;
                    }
                }

                if (!correctGiftInOrder)
                    return "Secret wish mismatch for customer '" + order.CustomerName
                        + "': correct gift '" + wish.CorrectGiftName
                        + "' is missing from the main order GiftNames.";

                int boxCapacity = order.BoxSize == GiftBoxSize.Small ? 4
                    : order.BoxSize == GiftBoxSize.Medium ? 6 : 9;
                if (order.GiftNames.Count > boxCapacity)
                    return "Order " + order.name + " does not fit its configured box size.";

                totalOrders++;
                if (!firstOrder)
                {
                    expectedCoins += Mathf.Max(0, order.RewardCoins);
                    expectedCoins += Mathf.Max(0, order.PerfectPackingTip);
                    // The second order is deliberately wrapped poorly to verify it still completes.
                    if (totalOrders > 2)
                        expectedCoins += Mathf.Max(0, order.WrappingBonusCoins);
                }

                firstOrder = false;
            }
        }

        return null;
    }

    private static void ValidateSecretWishSeedVariations()
    {
        HashSet<string> distinctSequences = new HashSet<string>();
        for (int seed = -16; seed < 80; seed++)
        {
            bool[] sequence = OrderProgressionManager.CreateSecretWishSequence(seed);
            Require(OrderProgressionManager.IsValidSecretWishSequence(sequence),
                "Seed " + seed + " generated an invalid secret-wish sequence.");
            Require(CountSecretWishes(sequence) == 4,
                "Seed " + seed + " did not generate exactly four secret wishes.");
            distinctSequences.Add(SequenceKey(sequence));
        }

        Require(distinctSequences.Count > 1,
            "Different seeds did not produce different secret-wish sequences.");
    }

    private static int CountSecretWishes(IList<bool> sequence)
    {
        int count = 0;
        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i])
                count++;
        }

        return count;
    }

    private static string SequenceKey(IList<bool> sequence)
    {
        char[] key = new char[sequence.Count];
        for (int i = 0; i < sequence.Count; i++)
            key[i] = sequence[i] ? '1' : '0';
        return new string(key);
    }

    private static int SumAssignedSecretWishBonuses(
        OrderCampaignDefinition campaign,
        IList<bool> assignments)
    {
        int total = 0;
        int orderIndex = 0;
        foreach (OrderDayDefinition day in campaign.Days)
        {
            foreach (GiftOrderDefinition order in day.Orders)
            {
                if (assignments[orderIndex] && order.SecretWish != null)
                    total += Mathf.Max(0, order.SecretWish.BonusCoins);
                orderIndex++;
            }
        }

        return total;
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

    private static int SumWrappingReputationBonusesForSuccessfulOrders(OrderCampaignDefinition campaign)
    {
        int total = 0;
        int processedOrders = 0;

        foreach (OrderDayDefinition day in campaign.Days)
        {
            foreach (GiftOrderDefinition order in day.Orders)
            {
                // First order fails, second has unsuitable wrapping, third is suitable but not perfect.
                if (processedOrders >= 3)
                    total += Mathf.Max(0, order.PerfectWrappingReputationBonus);

                processedOrders++;
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
