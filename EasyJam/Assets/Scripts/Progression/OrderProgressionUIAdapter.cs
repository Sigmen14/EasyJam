using System;
using UnityEngine;
using TheLastGift.UI;

/// <summary>Connects the playable order UI to progression and the short Christmas Eve story.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OrderProgressionManager))]
[RequireComponent(typeof(LastGiftUIController))]
public sealed class OrderProgressionUIAdapter : MonoBehaviour
{
    private const string StoryCampaignResourcePath = "StoryCampaign";
    private const string FallbackCampaignResourcePath = "ChristmasCampaign";
    private const int SortingEventReward = 10;

    private OrderProgressionManager progression;
    private LastGiftUIController ui;
    private LastGiftStoryController story;
    private LastGiftWorkshopUpgradeManager workshopUpgrades;
    private readonly ChristmasShopEventSchedule eventSchedule = new ChristmasShopEventSchedule();
    private bool storyCampaignActive;
    private bool startSequencePending;
    private bool boyReturnShown;
    private bool endingSequencePending;

    public static void ApplyOrderAssignmentToUI(
        Action<GiftOrderDefinition, int, int, int, int, bool> setProgressionOrder,
        GiftOrderDefinition order, int currentDay, int currentOrderNumber,
        int currentBalance, int currentReputation, bool showSecretWish)
    {
        if (setProgressionOrder == null) return;
        setProgressionOrder(order, currentDay, currentOrderNumber,
            currentBalance, currentReputation, showSecretWish);
    }

    private void Awake()
    {
        progression = GetComponent<OrderProgressionManager>();
        ui = GetComponent<LastGiftUIController>();
        story = GetComponent<LastGiftStoryController>();
        if (story == null) story = gameObject.AddComponent<LastGiftStoryController>();
        story.Configure(ui);

        workshopUpgrades = GetComponent<LastGiftWorkshopUpgradeManager>();
        if (workshopUpgrades == null) workshopUpgrades = gameObject.AddComponent<LastGiftWorkshopUpgradeManager>();

        OrderCampaignDefinition campaign = Resources.Load<OrderCampaignDefinition>(StoryCampaignResourcePath);
        storyCampaignActive = campaign != null;
        if (campaign == null) campaign = Resources.Load<OrderCampaignDefinition>(FallbackCampaignResourcePath);
        if (campaign == null)
        {
            Debug.LogError("Не найдена сюжетная или резервная кампания The Last Gift.", this);
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
        ui.Continue = ContinueAfterOrderResult;
        ui.OpenWorkshop = OpenWorkshop;
        ui.PurchaseWorkshopUpgrade = PurchaseWorkshopUpgrade;
        ui.StartGame = StartOrRestartGame;
        ApplyWorkshopDecorations();

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
            ui.OpenWorkshop = null;
            ui.PurchaseWorkshopUpgrade = null;
            ui.StartGame = null;
        }
    }

    private void StartOrRestartGame()
    {
        if (startSequencePending) return;
        if (!storyCampaignActive || story == null)
        {
            if (progression.IsCompleted) progression.RestartGame();
            else progression.StartGame();
            ui.Gameplay();
            return;
        }

        // The opening story is already told inside LastGiftIntroController:
        // its captions appear during the cinematic. Do not replay the same
        // exposition as a second dialogue after the intro/tutorial.
        bool restart = progression.IsCompleted;
        if (restart)
        {
            workshopUpgrades.ResetForNewGame();
            eventSchedule.Reset();
            boyReturnShown = false;
            endingSequencePending = false;
        }

        if (restart) progression.RestartGame();
        else progression.StartGame();

        if (progression.CurrentOrder != null)
            ui.Gameplay();
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
        ApplyWorkshopDecorations();
        ApplyOrderAssignmentToUI(ui.SetProgressionOrder, order,
            progression.CurrentDay, progression.CurrentOrderNumber,
            progression.CurrentCoins, progression.TotalReputationEarned, showSecretWish);
    }

    private void ConfirmPacking()
    {
        if (progression.CurrentOrder == null)
        {
            ui.PackingFeedback("Сейчас нет активного заказа.", false);
            return;
        }
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

        progression.CompleteCurrentOrder(
            new PackingResult(true, true, ui.PlacedGiftCount, order.GiftNames == null ? 0 : order.GiftNames.Count),
            matchingChoices >= 2, matchingChoices == 3);
    }

    private void HandleOrderCompleted(OrderCompletionData result)
    {
        if (!result.Success)
        {
            ui.PackingFeedback("Заказ не выполнен. Проверь подарки и попробуй ещё раз.", false);
            return;
        }

        bool allowWorkshop = !storyCampaignActive || result.Order == null || !result.Order.IsStoryFinaleOrder;
        ui.OrderSuccess(result.TotalEarned, result.WrappingBonus, result.ReputationBonus, allowWorkshop);
        ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
    }

    private void ContinueAfterOrderResult()
    {
        if (!storyCampaignActive || story == null)
        {
            AdvanceToNextOrder();
            return;
        }

        GiftOrderDefinition completedOrder = progression.CurrentOrder;
        if (completedOrder != null && completedOrder.IsStoryFinaleOrder)
        {
            if (endingSequencePending) return;
            endingSequencePending = true;
            story.PlayEnding(() =>
            {
                endingSequencePending = false;
                progression.ContinueAfterResult();
            });
            return;
        }

        int completedOrders = progression.TotalOrdersCompleted;
        if (completedOrders == 3 && !boyReturnShown)
        {
            boyReturnShown = true;
            story.PlayBoyReturnAndDiscount(AdvanceToNextOrder);
            return;
        }

        Action afterDialogue = () =>
        {
            bool hasAnotherOrder = progression.CurrentDayData != null
                && progression.CurrentDayData.Orders != null
                && progression.CurrentOrderNumber < progression.CurrentDayData.Orders.Count;
            if (eventSchedule.TryQueue(progression.TotalOrdersCompleted, hasAnotherOrder)
                && eventSchedule.TryConsume(out ScheduledChristmasShopEvent scheduledEvent))
                ShowSortingEvent(scheduledEvent);
            else
                AdvanceToNextOrder();
        };

        if (completedOrders == 1 || completedOrders == 2)
            story.PlayDuringProgression(completedOrders, afterDialogue);
        else
            afterDialogue();
    }

    private void AdvanceToNextOrder()
    {
        progression.ContinueAfterResult();
        if (progression.CurrentOrder != null) ui.Gameplay();
    }

    private void ShowSortingEvent(ScheduledChristmasShopEvent scheduledEvent)
    {
        // The schedule entry is consumed before presenting the event, so one milestone
        // cannot be queued twice if the UI is interrupted or rebuilt.
        ui.ShowMixedGiftSortingEvent(() =>
        {
            progression.AwardBonusCoins(SortingEventReward);
            ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
            ui.ShowChristmasEventSuccess(SortingEventReward, AdvanceToNextOrder);
        });
    }

    private void OpenWorkshop()
    {
        ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
        ApplyWorkshopDecorations();
        ui.ShowWorkshopScreen();
    }

    private bool PurchaseWorkshopUpgrade(WorkshopUpgradeType upgrade)
    {
        bool purchased = workshopUpgrades != null && workshopUpgrades.TryPurchase(upgrade);
        if (purchased)
        {
            ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
            ApplyWorkshopDecorations();
        }
        return purchased;
    }

    private void ApplyWorkshopDecorations()
    {
        if (ui == null || workshopUpgrades == null) return;
        ui.SetWorkshopDecorations(workshopUpgrades.HasGarland,
            workshopUpgrades.HasExtraShelf, workshopUpgrades.HasPremiumWrapping);
    }

    private void HandleDayCompleted(DaySummaryData summary)
    {
        ui.SetProgressionBalance(summary.CurrentBalance, progression.TotalReputationEarned);
        if (!storyCampaignActive || progression.Campaign == null || progression.Campaign.Days.Count > 1)
            ui.DayEnd(summary.OrdersCompleted, summary.CoinsEarned);
    }

    private void HandleGameCompleted(GameSummaryData summary)
    {
        ui.SetProgressionBalance(summary.FinalCoins, summary.ReputationEarned);
        ui.FinalScreen(summary);
    }
}
