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
        ui.SetProgressionBalance(summary.CurrentBalance);
        ui.DayEnd(summary.OrdersCompleted, summary.CoinsEarned);
    }

    private void HandleGameCompleted(GameSummaryData summary)
    {
        ui.SetProgressionBalance(summary.FinalCoins);
        ui.FinalScreen(summary);
    }
}
