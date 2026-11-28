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
    private readonly List<GiftDefinition> runtimeGiftDefinitions = new List<GiftDefinition>();

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

        DestroyRuntimeGiftDefinitions();
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
        int giftCount = order.GiftNames == null ? 0 : order.GiftNames.Count;
        int totalCells = width * height;

        if (giftCount <= 0 || totalCells % giftCount != 0)
        {
            ui.PackingFeedback(
                "Не удалось создать головоломку: количество подарков не подходит для размера коробки.",
                false);
            DisablePackingInput();
            return;
        }

        if (packingBoard == null)
        {
            GameObject boardObject = new GameObject("Campaign Packing Board");
            packingBoard = boardObject.AddComponent<PackingBoardController>();
            packingBoard.ConfigureForCampaign(Camera.main, CampaignCellSize, Vector2.zero);
        }

        DestroyRuntimeGiftDefinitions();
        List<GiftDefinition> gifts = new List<GiftDefinition>(giftCount);
        int cellsPerGift = totalCells / giftCount;

        for (int i = 0; i < giftCount; i++)
        {
            string giftName = order.GiftNames[i];
            Vector2Int[] footprint = CreateFootprint(cellsPerGift, width, height, i);
            GiftDefinition definition = GiftDefinition.CreateRuntime(
                giftName,
                footprint,
                GetGiftTint(giftName),
                true,
                ui.GetPackingGiftSprite(giftName));

            gifts.Add(definition);
            runtimeGiftDefinitions.Add(definition);
        }

        packingBoard.SetupBoard(width, height, gifts);
        packingBoard.SetInputEnabled(true);
    }

    private static void GetBoardDimensions(GiftBoxSize size, out int width, out int height)
    {
        switch (size)
        {
            case GiftBoxSize.Small:
                width = 2;
                height = 2;
                break;
            case GiftBoxSize.Medium:
                width = 3;
                height = 2;
                break;
            default:
                width = 3;
                height = 3;
                break;
        }
    }

    private static Vector2Int[] CreateFootprint(int cellCount, int boardWidth, int boardHeight, int giftIndex)
    {
        if (cellCount == 2)
            return new[] { Vector2Int.zero, Vector2Int.right };

        if (cellCount == 3)
        {
            // Two L-triominoes tile a 3x2 box. A line plus two L pieces
            // tile a 3x3 box, giving the last day a less repetitive layout.
            if (boardWidth == 3 && boardHeight == 3 && giftIndex == 0)
                return new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 };

            return new[]
            {
                Vector2Int.zero,
                Vector2Int.right,
                Vector2Int.up
            };
        }

        if (cellCount == 4)
        {
            return new[]
            {
                Vector2Int.zero,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.one
            };
        }

        // Safe fallback for future order assets with a different box size.
        Vector2Int[] cells = new Vector2Int[cellCount];
        for (int i = 0; i < cellCount; i++)
            cells[i] = new Vector2Int(i, 0);
        return cells;
    }

    private static Color GetGiftTint(string giftName)
    {
        switch ((giftName ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "МИШКА":
                return new Color(0.70f, 0.43f, 0.27f, 1f);
            case "ПОДАРОК":
                return new Color(0.78f, 0.20f, 0.22f, 1f);
            case "ЛЕДЕНЕЦ":
                return new Color(0.90f, 0.72f, 0.47f, 1f);
            case "СНЕЖНЫЙ ШАР":
                return new Color(0.42f, 0.72f, 0.88f, 1f);
            default:
                return new Color(0.72f, 0.58f, 0.35f, 1f);
        }
    }

    private void DestroyRuntimeGiftDefinitions()
    {
        foreach (GiftDefinition definition in runtimeGiftDefinitions)
        {
            if (definition != null)
                Destroy(definition);
        }

        runtimeGiftDefinitions.Clear();
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
