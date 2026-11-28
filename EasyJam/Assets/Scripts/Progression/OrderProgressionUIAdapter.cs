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
    private readonly ChristmasShopEventSchedule eventSchedule = new ChristmasShopEventSchedule();
    private bool daySummaryPending;
    private bool eventActive;
    private int activeEventOrderMilestone;

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
        ui.Continue = ContinueProgression;
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
            ui.Continue = null;
            ui.StartGame = null;
        }
    }


    private void StartOrRestartGame()
    {
        eventSchedule.Reset();
        daySummaryPending = false;
        eventActive = false;
        activeEventOrderMilestone = 0;

        if (progression.IsCompleted)
            progression.RestartGame();
        else
            progression.StartGame();
    }

    private void ContinueProgression()
    {
        // The day summary is a separate screen. Wait until the player dismisses it
        // before opening a queued event or returning to the next customer's order.
        if (daySummaryPending)
        {
            daySummaryPending = false;
            if (TryStartPendingShopEvent())
                return;

            if (!progression.IsCompleted)
                ui.Gameplay();
            return;
        }

        progression.ContinueAfterResult();

        bool hasAnotherOrder = !progression.IsCompleted && progression.CurrentOrder != null;
        eventSchedule.TryQueue(progression.TotalOrdersCompleted, hasAnotherOrder);

        // DayCompleted / GameCompleted may have replaced the result screen. Never
        // cover either summary with an event or immediately rebuild the shop screen.
        if (daySummaryPending || progression.IsCompleted)
            return;

        if (TryStartPendingShopEvent())
            return;

        ui.Gameplay();
    }

    private bool TryStartPendingShopEvent()
    {
        if (eventActive || progression == null || progression.IsCompleted
            || progression.CurrentOrder == null)
        {
            return false;
        }

        if (!eventSchedule.TryConsume(out ScheduledChristmasShopEvent scheduledEvent))
            return false;

        switch (scheduledEvent.Type)
        {
            case ChristmasShopEventType.MixedGifts:
                eventActive = true;
                activeEventOrderMilestone = scheduledEvent.OrderMilestone;
                ui.ShowMixedGiftSortingEvent(CompleteMixedGiftsEvent);
                return true;

            default:
                Debug.LogWarning("Неизвестный тип рождественского происшествия: " + scheduledEvent.Type, this);
                return false;
        }
    }

    private void CompleteMixedGiftsEvent()
    {
        if (!eventActive)
            return;

        eventActive = false;
        string rewardId = "mixed-gifts-" + activeEventOrderMilestone;
        int reward = progression.TryAwardEventReward(rewardId, 30) ? 30 : 0;
        ui.SetProgressionBalance(progression.CurrentCoins);
        ui.ShowChristmasEventSuccess(reward, ui.Gameplay);
    }

    private void HandleOrderStarted(GiftOrderDefinition order)
    {
        ui.SetProgressionOrder(
            order,
            progression.CurrentDay,
            progression.CurrentOrderNumber,
            progression.CurrentCoins);
    }

    private void ConfirmPacking()
    {
        GiftOrderDefinition order = progression.CurrentOrder;
        if (order == null)
        {
            ui.PackingFeedback("Сейчас нет активного заказа.", false);
            return;
        }

        int requiredCount = order.GiftNames == null ? 0 : order.GiftNames.Count;
        int placedCount = ui.PlacedGiftCount;

        // LastGiftUIController validates the exact gift combination before this
        // callback is invoked. The manager then awards the configured order reward.
        progression.CompleteCurrentOrder(
            new PackingResult(true, true, placedCount, requiredCount));
    }

    private void HandleOrderCompleted(OrderCompletionData result)
    {
        if (result.Success)
        {
            ui.OrderSuccess(result.TotalEarned);
            ui.SetProgressionBalance(progression.CurrentCoins);
        }
        else
        {
            ui.PackingFeedback("Заказ не выполнен. Проверь подарки и попробуй ещё раз.", false);
        }
    }

    private void HandleDayCompleted(DaySummaryData summary)
    {
        daySummaryPending = true;
        ui.SetProgressionBalance(summary.CurrentBalance);
        ui.DayEnd(summary.OrdersCompleted, summary.CoinsEarned);
    }

    private void HandleGameCompleted(GameSummaryData summary)
    {
        daySummaryPending = false;
        ui.SetProgressionBalance(summary.FinalCoins);
        ui.FinalScreen(summary);
    }
}
