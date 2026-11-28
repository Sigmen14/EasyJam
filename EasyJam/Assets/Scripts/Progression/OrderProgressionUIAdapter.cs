using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastGift.UI;
using TheLastGift.Audio;

/// <summary>Connects the playable order UI to progression and the short Christmas Eve story.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OrderProgressionManager))]
[RequireComponent(typeof(LastGiftUIController))]
public sealed class OrderProgressionUIAdapter : MonoBehaviour
{
    private const string StoryCampaignResourcePath = "StoryCampaign";
    private const string FallbackCampaignResourcePath = "ChristmasCampaign";
    private const int SortingEventReward = 10;
    private const int RushOrderBonus = 8;
    private const int HappyCustomerBonus = 6;
    private const int ChristmasSurpriseBonus = 10;
    private const int FestiveMoodBonus = 5;

    private OrderProgressionManager progression;
    private LastGiftUIController ui;
    private LastGiftStoryController story;
    private LastGiftWorkshopUpgradeManager workshopUpgrades;
    private readonly ChristmasShopEventSchedule eventSchedule = new ChristmasShopEventSchedule();
    private bool storyCampaignActive;
    private bool boyReturnShown;
    private bool endingSequencePending;
    private bool urgentWrappingBonusActive;
    private bool urgentWrappingQualified;
    private bool ribbonShortageActive;
    private bool festiveMoodBonusActive;

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
        progression.ConfigureOrderQueue(true, 3, 180f, RushOrderBonus);
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
        ui.LeaveGameplay = LeaveGameplay;
        ApplyWorkshopDecorations();

        progression.OrderStarted += HandleOrderStarted;
        progression.OrderQueueChanged += HandleOrderQueueChanged;
        progression.OrderQueueTimerChanged += HandleOrderQueueTimerChanged;
        progression.OrderCompleted += HandleOrderCompleted;
        progression.DayCompleted += HandleDayCompleted;
        progression.GameCompleted += HandleGameCompleted;
    }

    private void OnDestroy()
    {
        if (progression != null)
        {
            progression.OrderStarted -= HandleOrderStarted;
            progression.OrderQueueChanged -= HandleOrderQueueChanged;
            progression.OrderQueueTimerChanged -= HandleOrderQueueTimerChanged;
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
            ui.LeaveGameplay = null;
        }
    }

    private void StartOrRestartGame()
    {
        bool restart = progression.IsCompleted;
        if (restart)
            ResetRandomEventState();

        if (!storyCampaignActive || story == null)
        {
            if (restart) progression.RestartGame();
            else progression.StartGame();
            if (progression.CurrentOrder != null)
                ui.Gameplay();
            return;
        }

        // The opening story is already told inside LastGiftIntroController:
        // its captions appear during the cinematic. Do not replay the same
        // exposition as a second dialogue after the intro/tutorial.
        if (restart)
        {
            workshopUpgrades.ResetForNewGame();
            boyReturnShown = false;
            endingSequencePending = false;
        }

        if (restart) progression.RestartGame();
        else progression.StartGame();

        if (progression.CurrentOrder != null)
            ui.Gameplay();
    }

    private void LeaveGameplay()
    {
        if (progression != null)
            progression.ReturnToMenu();

        if (workshopUpgrades != null)
            workshopUpgrades.ResetForNewGame();

        eventSchedule.Reset();
        boyReturnShown = false;
        endingSequencePending = false;

        if (ui != null)
        {
            ui.ResetCampaignState();
            ui.MainMenu();
        }
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
        LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.NewOrder);
        ApplyWorkshopDecorations();
        ApplyOrderAssignmentToUI(ui.SetProgressionOrder, order,
            progression.CurrentDay, progression.CurrentOrderNumber,
            progression.CurrentCoins, progression.TotalReputationEarned, showSecretWish);
        ui.SetActiveOrderQueueEntry(progression.ActiveQueueEntry);
    }

    private void HandleOrderQueueChanged(IReadOnlyList<OrderQueueEntry> entries)
    {
        if (ui == null || progression == null || !progression.OrderQueueEnabled
            || progression.CurrentOrder != null || progression.IsCompleted)
            return;

        ui.ShowOrderQueue(entries, SelectQueuedOrder);
    }

    private void HandleOrderQueueTimerChanged(IReadOnlyList<OrderQueueEntry> entries)
    {
        if (ui == null || progression == null || !progression.OrderQueueEnabled
            || progression.IsCompleted)
            return;

        if (progression.CurrentOrder == null)
            ui.UpdateOrderQueueWaitTimes(entries);
        else
            ui.UpdateActiveOrderQueueWaitTime(progression.ActiveQueueEntry);
    }

    private void SelectQueuedOrder(GiftOrderDefinition order)
    {
        if (progression != null && progression.SelectQueuedOrder(order))
            ui.Gameplay();
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

        bool suitableWrapping = matchingChoices >= (ribbonShortageActive ? 3 : 2);
        urgentWrappingQualified = urgentWrappingBonusActive && matchingChoices == 3;

        progression.CompleteCurrentOrder(
            new PackingResult(true, true, ui.PlacedGiftCount, order.GiftNames == null ? 0 : order.GiftNames.Count),
            suitableWrapping, matchingChoices == 3);
    }

    private void HandleOrderCompleted(OrderCompletionData result)
    {
        if (!result.Success)
        {
            LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.Error);
            ui.PackingFeedback("Заказ не выполнен. Проверь подарки и попробуй ещё раз.", false);
            return;
        }

        int eventBonus = 0;
        if (urgentWrappingBonusActive)
        {
            if (urgentWrappingQualified)
                eventBonus += RushOrderBonus;
            urgentWrappingBonusActive = false;
            urgentWrappingQualified = false;
        }

        if (festiveMoodBonusActive)
        {
            eventBonus += FestiveMoodBonus;
            festiveMoodBonusActive = false;
        }

        ribbonShortageActive = false;
        if (eventBonus > 0)
            progression.AwardBonusCoins(eventBonus);

        bool allowWorkshop = !storyCampaignActive || result.Order == null || !result.Order.IsStoryFinaleOrder;
        ui.OrderSuccess(result.TotalEarned + eventBonus, result.WrappingBonus,
            result.ReputationBonus, allowWorkshop, result.UrgencyBonus);
        ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
    }

    private void ContinueAfterOrderResult()
    {
        GiftOrderDefinition completedOrder = progression.CurrentOrder;
        if (storyCampaignActive && story != null
            && completedOrder != null && completedOrder.IsStoryFinaleOrder)
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
        if (storyCampaignActive && story != null
            && completedOrders == 3 && !boyReturnShown)
        {
            boyReturnShown = true;
            story.PlayBoyReturnAndDiscount(AdvanceToNextOrder);
            return;
        }

        Action afterDialogue = () =>
        {
            bool hasAnotherOrder = progression.OrderQueueEnabled
                ? progression.HasPendingOrders
                : progression.CurrentDayData != null
                    && progression.CurrentDayData.Orders != null
                    && progression.CurrentOrderNumber < progression.CurrentDayData.Orders.Count;

            if (eventSchedule.TryQueue(progression.TotalOrdersCompleted, hasAnotherOrder)
                && eventSchedule.TryConsume(out ScheduledChristmasShopEvent scheduledEvent))
                ShowScheduledEvent(scheduledEvent);
            else
                AdvanceToNextOrder();
        };

        if (!storyCampaignActive || story == null)
        {
            afterDialogue();
            return;
        }

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

    private void ShowScheduledEvent(ScheduledChristmasShopEvent scheduledEvent)
    {
        switch (scheduledEvent.Type)
        {
            case ChristmasShopEventType.MixedGifts:
                ShowSortingEvent(scheduledEvent);
                break;

            case ChristmasShopEventType.RushOrder:
                urgentWrappingBonusActive = true;
                urgentWrappingQualified = false;
                ui.ShowRandomChristmasEvent(
                    "СРОЧНЫЙ ЗАКАЗ",
                    "Следующему покупателю нужен подарок к празднику без промедления. Подготовь красивую упаковку и учти вкусы покупателя.",
                    "ЗАДАЧА: совпади со всеми 3 предпочтениями в оформлении. НАГРАДА: +8 монет за идеальную упаковку.",
                    AdvanceToNextOrder);
                break;

            case ChristmasShopEventType.HappyCustomer:
                ShowImmediateBonusEvent(
                    "СЧАСТЛИВЫЙ ПОКУПАТЕЛЬ",
                    "Покупатель улыбнулся: «Такой красивый подарок — настоящее рождественское чудо. Спасибо!»",
                    HappyCustomerBonus);
                break;

            case ChristmasShopEventType.RibbonShortage:
                ribbonShortageActive = true;
                ui.ShowRandomChristmasEvent(
                    "ЗАКАНЧИВАЕТСЯ ЛЕНТА",
                    "Запас упаковочной ленты почти закончился. На один заказ её хватит только на самый аккуратный вариант.",
                    "ЭФФЕКТ НА 1 ЗАКАЗ: бонус за оформление будет начислен только при совпадении всех 3 предпочтений. Основная награда за заказ не меняется.",
                    AdvanceToNextOrder);
                break;

            case ChristmasShopEventType.ChristmasSurprise:
                ShowImmediateBonusEvent(
                    "РОЖДЕСТВЕНСКИЙ СЮРПРИЗ",
                    "Под прилавком нашлась маленькая коробочка с монетами. Похоже, Рождество приготовило приятный подарок для мастерской!",
                    ChristmasSurpriseBonus);
                break;

            case ChristmasShopEventType.FestiveMood:
                festiveMoodBonusActive = true;
                ui.ShowRandomChristmasEvent(
                    "ПРАЗДНИЧНОЕ НАСТРОЕНИЕ",
                    "Покупатели напевают рождественскую песню, а в мастерской становится особенно уютно. Кажется, впереди ещё один удачный заказ.",
                    "БОНУС: +5 монет к следующему успешно выполненному заказу.",
                    AdvanceToNextOrder);
                break;

            default:
                AdvanceToNextOrder();
                break;
        }
    }

    private void ShowImmediateBonusEvent(string title, string description, int reward)
    {
        progression.AwardBonusCoins(reward);
        ui.SetProgressionBalance(progression.CurrentCoins, progression.TotalReputationEarned);
        ui.ShowRandomChristmasEvent(
            title, description, "БОНУС В КАССУ: +" + reward + " МОНЕТ ✦",
            AdvanceToNextOrder);
    }

    private void ResetRandomEventState()
    {
        eventSchedule.Reset();
        urgentWrappingBonusActive = false;
        urgentWrappingQualified = false;
        ribbonShortageActive = false;
        festiveMoodBonusActive = false;
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

        LastGiftEndingController ending = GetComponent<LastGiftEndingController>();
        if (ending == null)
            ending = gameObject.AddComponent<LastGiftEndingController>();

        ending.Play(ui);
    }
}
