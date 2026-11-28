using System;
using UnityEngine;
using TheLastGift.UI;

/// <summary>
/// Connects the playable drag-and-drop UI to the campaign and reward manager.
/// The UI owns presentation and exact gift validation; the progression manager
/// owns order sequencing, day transitions and coin rewards.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OrderProgressionManager))]
[RequireComponent(typeof(LastGiftUIController))]
public sealed class OrderProgressionUIAdapter : MonoBehaviour
{
    private const string CampaignResourcePath = "ChristmasCampaign";

    private OrderProgressionManager progression;
    private LastGiftUIController ui;

    /// <summary>
    /// Single forwarding path used by the runtime handler and the editor smoke test.
    /// The assignment flag is forwarded unchanged to LastGiftUIController.
    /// </summary>
    public static void ApplyOrderAssignmentToUI(
        Action<GiftOrderDefinition, int, int, int, int, bool> setProgressionOrder,
        GiftOrderDefinition order,
        int currentDay,
        int currentOrderNumber,
        int currentBalance,
        int currentReputation,
        bool showSecretWish)
    {
        if (setProgressionOrder == null)
            return;

        setProgressionOrder(
            order,
            currentDay,
            currentOrderNumber,
            currentBalance,
            currentReputation,
            showSecretWish);
    }

    private void Awake()
    {
        progression = GetComponent<OrderProgressionManager>();
        ui = GetComponent<LastGiftUIController>();

        OrderCampaignDefinition campaign =
            Resources.Load<OrderCampaignDefinition>(CampaignResourcePath);

        if (campaign == null)
        {
            Debug.LogError(
                "Не найдена кампания. Проверь Assets/Resources/ChristmasCampaign.asset.",
                this);
            enabled = false;
            return;
        }

        progression.ConfigureCampaign(campaign);
    }

    private void Start()
    {
        if (progression == null || ui == null)
        {
            Debug.LogError("Не удалось подключить интерфейс к системе заказов.", this);
            enabled = false;
            return;
        }

        ui.ConfirmPacking = ConfirmPacking;
        ui.ConfirmWrapping = HandleWrappingCompleted;
        ui.SecretWishShown = progression.MarkCurrentSecretWishShown;
        ui.AwardSecretWishBonus = AwardSecretWishBonus;
        ui.Continue = progression.ContinueAfterResult;
        ui.StartGame = StartOrRestartGame;

        progression.OrderStarted += HandleOrderStarted;
        progression.OrderCompleted += HandleOrderCompleted;
        progression.DayCompleted += HandleDayCompleted;
        progression.GameCompleted += HandleGameCompleted;
    }

    private void OnDestroy()
    {
        if (progression != null)
        {
            progression.OrderStarted -= HandleOrderStarted;
            progression.OrderCompleted -= HandleOrderCompleted;
            progression.DayCompleted -= HandleDayCompleted;
            progression.GameCompleted -= HandleGameCompleted;
        }

        if (ui != null)
        {
            ui.ConfirmPacking = null;
            ui.ConfirmWrapping = null;
            ui.SecretWishShown = null;
            ui.AwardSecretWishBonus = null;
            ui.Continue = null;
            ui.StartGame = null;
        }
    }

    private void StartOrRestartGame()
    {
        if (progression.IsCompleted)
            progression.RestartGame();
        else
            progression.StartGame();
    }

    private int AwardSecretWishBonus(bool correctAnswer, int amount)
    {
        int awarded = progression == null ? 0 : progression.AwardSecretWishBonus(correctAnswer, amount);
        if (ui != null && progression != null)
            ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
        return awarded;
    }

    private void HandleOrderStarted(GiftOrderDefinition order, bool showSecretWish)
    {
        Debug.Log(
            $"[SecretWishFlow] Adapter received {order.CustomerName}: showSecretWish={showSecretWish}.",
            this);

        ApplyOrderAssignmentToUI(
            ui.SetProgressionOrder,
            order,
            progression.CurrentDay,
            progression.CurrentOrderNumber,
            progression.CurrentCoins,
            progression.TotalReputationEarned,
            showSecretWish);
    }

    private void ConfirmPacking()
    {
        GiftOrderDefinition order = progression.CurrentOrder;
        if (order == null)
        {
            ui.PackingFeedback("Сейчас нет активного заказа.", false);
            return;
        }

        // LastGiftUIController has already verified the exact gift combination.
        // Keep the packed box intact while the player chooses its wrapping.
        ui.ShowGiftWrappingScreen();
    }

    private void HandleWrappingCompleted(GiftWrappingSelection selection)
    {
        GiftOrderDefinition order = progression.CurrentOrder;
        if (order == null)
        {
            ui.PackingFeedback("Сейчас нет активного заказа.", false);
            return;
        }

        int matchingChoices = 0;
        if (selection.Paper == order.PreferredWrappingPaper) matchingChoices++;
        if (selection.Ribbon == order.PreferredWrappingRibbon) matchingChoices++;
        if (selection.Card == order.PreferredWrappingCard) matchingChoices++;

        bool suitableWrapping = matchingChoices >= 2;
        bool perfectWrapping = matchingChoices == 3;
        int requiredCount = order.GiftNames == null ? 0 : order.GiftNames.Count;

        // The manager validates the box again and owns the one-time reward guard.
        progression.CompleteCurrentOrder(
            new PackingResult(true, true, ui.PlacedGiftCount, requiredCount),
            suitableWrapping,
            perfectWrapping);
    }

    private void HandleOrderCompleted(OrderCompletionData result)
    {
        if (result.Success)
        {
            ui.OrderSuccess(result.TotalEarned, result.WrappingBonus, result.ReputationBonus);
            ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
        }
        else
        {
            ui.PackingFeedback("Заказ не выполнен. Проверь подарки и попробуй ещё раз.", false);
        }
    }

    private void HandleDayCompleted(DaySummaryData summary)
    {
        ui.SetProgressionBalance(summary.CurrentBalance, progression.TotalReputationEarned);
        ui.DayEnd(summary.OrdersCompleted, summary.CoinsEarned);
    }

    private void HandleGameCompleted(GameSummaryData summary)
    {
        ui.SetProgressionBalance(summary.FinalCoins, summary.ReputationEarned);
        ui.FinalScreen(summary);
    }
}
