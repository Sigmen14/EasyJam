using System.Collections.Generic;
using UnityEngine;
using TheLastGift.Packing;
using TheLastGift.UI;

/// <summary>
/// Connects the customer campaign to the existing geometric packing puzzle.
/// The board validates real piece placement; the progression manager owns
/// order sequencing, day transitions and rewards.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OrderProgressionManager))]
[RequireComponent(typeof(LastGiftUIController))]
public sealed class OrderProgressionUIAdapter : MonoBehaviour
{
    private const string CampaignResourcePath = "ChristmasCampaign";
    private const float CampaignCellSize = 0.9f;

    private OrderProgressionManager progression;
    private LastGiftUIController ui;
    private PackingBoardController packingBoard;

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

        ui.SetPackingBoardMode(true);
        ui.ConfirmPacking = ConfirmPacking;
        ui.ClearPacking = ResetPackingBoard;
        ui.LeaveGameplay = DisablePackingInput;
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
            ui.ClearPacking = null;
            ui.LeaveGameplay = null;
            ui.Continue = null;
            ui.StartGame = null;
        }

        if (packingBoard != null)
            Destroy(packingBoard.gameObject);
    }

    private void StartOrRestartGame()
    {
        if (progression.IsCompleted)
        {
            progression.RestartGame();
            return;
        }

        // Returning to the main menu does not reset campaign progression.
        // Rebuild the current order's puzzle when the player resumes.
        if (progression.CurrentOrder != null)
        {
            HandleOrderStarted(progression.CurrentOrder);
            return;
        }

        progression.StartGame();
    }

    private void HandleOrderStarted(GiftOrderDefinition order)
    {
        ui.SetProgressionOrder(
            order,
            progression.CurrentDay,
            progression.CurrentOrderNumber,
            progression.CurrentCoins);

        SetupPackingBoard(order);
    }

    private void SetupPackingBoard(GiftOrderDefinition order)
    {
        if (order == null)
        {
            DisablePackingInput();
            return;
        }

        GetBoardDimensions(order.BoxSize, out int width, out int height);
        List<GiftDefinition> gifts = ResolveOrderGifts(order);
        if (gifts == null)
        {
            DisablePackingInput();
            return;
        }

        if (packingBoard == null)
        {
            GameObject boardObject = new GameObject("Campaign Packing Board");
            packingBoard = boardObject.AddComponent<PackingBoardController>();
            packingBoard.ConfigureForCampaign(Camera.main, CampaignCellSize, Vector2.zero);
        }

        ui.SetPackingBoardDimensions(width, height);
        packingBoard.SetupBoard(width, height, gifts);
        packingBoard.SetInputEnabled(true);
    }

    private List<GiftDefinition> ResolveOrderGifts(GiftOrderDefinition order)
    {
        if (order.GiftNames == null || order.GiftNames.Count == 0)
        {
            ui.PackingFeedback("В заказе нет подарков.", false);
            return null;
        }

        List<GiftDefinition> catalog = progression.Campaign != null
            ? progression.Campaign.GiftCatalog
            : null;

        if (catalog == null || catalog.Count == 0)
        {
            ui.PackingFeedback("В кампании не настроен каталог фигур подарков.", false);
            return null;
        }

        List<GiftDefinition> gifts = new List<GiftDefinition>(order.GiftNames.Count);
        foreach (string giftName in order.GiftNames)
        {
            string normalizedName = (giftName ?? string.Empty).Trim();
            GiftDefinition match = null;

            foreach (GiftDefinition candidate in catalog)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.GiftName))
                    continue;

                if (string.Equals(candidate.GiftName.Trim(), normalizedName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    match = candidate;
                    break;
                }
            }

            if (match == null)
            {
                ui.PackingFeedback($"Для подарка «{giftName}» не найдена фигура в каталоге.", false);
                return null;
            }

            gifts.Add(match);
        }

        return gifts;
    }

    private static void GetBoardDimensions(GiftBoxSize size, out int width, out int height)
    {
        switch (size)
        {
            case GiftBoxSize.Small: width = 3; height = 2; break;
            case GiftBoxSize.Medium: width = 4; height = 3; break;
            default: width = 4; height = 4; break;
        }
    }

    private void ResetPackingBoard()
    {
        if (packingBoard == null)
        {
            ui.PackingFeedback("Игровая доска ещё не создана.", false);
            return;
        }

        packingBoard.ResetAllPiecesToTray();
        ui.PackingFeedback("Все фигуры возвращены на поднос.", true);
    }

    private void DisablePackingInput()
    {
        if (packingBoard != null)
            packingBoard.SetInputEnabled(false);
    }

    private void ConfirmPacking()
    {
        GiftOrderDefinition order = progression.CurrentOrder;
        if (order == null)
        {
            ui.PackingFeedback("Сейчас нет активного заказа.", false);
            return;
        }

        if (packingBoard == null)
        {
            ui.PackingFeedback("Игровая доска не создана.", false);
            return;
        }

        TheLastGift.Packing.PackingResult packingResult;
        if (!packingBoard.TryValidate(out packingResult))
        {
            ui.PackingFeedback(packingResult.Message, false);
            return;
        }

        packingBoard.SetInputEnabled(false);
        progression.CompleteCurrentOrder(
            new global::PackingResult(
                packingResult.IsValid,
                packingResult.AllItemsPlaced,
                packingResult.PlacedCount,
                packingResult.TotalCount));
    }

    private void HandleOrderCompleted(OrderCompletionData result)
    {
        if (result.Success)
        {
            DisablePackingInput();
            ui.OrderSuccess(result.TotalEarned);
            ui.SetProgressionBalance(progression.CurrentCoins);
        }
        else
        {
            ui.PackingFeedback("Укладка не прошла проверку. Попробуй переставить фигуры.", false);
        }
    }

    private void HandleDayCompleted(DaySummaryData summary)
    {
        DisablePackingInput();
        ui.SetProgressionBalance(summary.CurrentBalance);
        ui.DayEnd(summary.OrdersCompleted, summary.CoinsEarned);
    }

    private void HandleGameCompleted(GameSummaryData summary)
    {
        DisablePackingInput();
        ui.SetProgressionBalance(summary.FinalCoins);
        ui.FinalScreen(summary);
    }
}
