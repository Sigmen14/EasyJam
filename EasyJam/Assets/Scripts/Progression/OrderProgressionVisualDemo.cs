using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A visible, interactive demonstration of the order progression system.
/// Packing is intentionally simulated by the two result buttons; no real packing board is used.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderProgressionVisualDemo : MonoBehaviour
{
    [SerializeField] private OrderProgressionManager progression;
    [SerializeField] private OrderCampaignDefinition campaign;

    // Applied centrally so every label, heading, button and status message
    // remains visually consistent when the demo UI is generated.
    private const float UiFontScale = 1.35f;

    private readonly List<DaySummaryData> completedDays = new List<DaySummaryData>();

    private Canvas canvas;
    private Font font;
    private Text headerDayText;
    private Text headerCoinText;
    private Text orderLabelText;
    private Text customerNameText;
    private Text customerWishText;
    private Text giftListText;
    private Text boxSizeText;
    private Text rewardText;
    private Text dayTitleText;
    private Text orderProgressText;
    private Text timelineText;
    private Text resultText;
    private Button successButton;
    private Button failButton;
    private Button continueButton;
    private OrderCompletionData lastResult;
    private GameSummaryData finalSummary;

    private void Awake()
    {
        EnsureGameViewCamera();

        if (progression == null)
            progression = GetComponent<OrderProgressionManager>();

        // Recover missing scene references instead of failing at startup.
        if (campaign == null && progression != null)
            campaign = progression.Campaign;

        if (campaign == null)
            campaign = Resources.Load<OrderCampaignDefinition>("ChristmasCampaign");

        if (progression == null)
            progression = gameObject.AddComponent<OrderProgressionManager>();

        if (campaign != null && progression != null)
            progression.ConfigureCampaign(campaign);

        if (progression == null || campaign == null)
        {
            Debug.LogError(
                "OrderProgressionVisualDemo: Campaign asset was not found. Ensure Assets/Resources/ChristmasCampaign.asset exists.",
                this);
            enabled = false;
            return;
        }

        BuildInterface();
    }

    private void Start()
    {
        progression.OrderStarted += HandleOrderStarted;
        progression.OrderCompleted += HandleOrderCompleted;
        progression.DayCompleted += HandleDayCompleted;
        progression.GameCompleted += HandleGameCompleted;

        progression.StartGame();
        RefreshInterface();
    }

    private void OnDestroy()
    {
        if (progression == null)
            return;

        progression.OrderStarted -= HandleOrderStarted;
        progression.OrderCompleted -= HandleOrderCompleted;
        progression.DayCompleted -= HandleDayCompleted;
        progression.GameCompleted -= HandleGameCompleted;
    }

    private void HandleOrderStarted(GiftOrderDefinition order)
    {
        lastResult = null;
        RefreshInterface();
    }

    private void HandleOrderCompleted(OrderCompletionData result)
    {
        lastResult = result;
        RefreshInterface();
    }

    private void HandleDayCompleted(DaySummaryData summary)
    {
        completedDays.Add(summary);
        RefreshInterface();
    }

    private void HandleGameCompleted(GameSummaryData summary)
    {
        finalSummary = summary;
        RefreshInterface();
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Order Progression Demo Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        CreateStretchPanel("Warm Background", canvas.transform, new Color32(244, 239, 229, 255));

        RectTransform header = CreateRect("Header", canvas.transform,
            new Vector2(0f, 394f), new Vector2(1600f, 112f));
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color32(34, 48, 69, 255);

        CreateText(header, "Game Title", "ПОСЛЕДНИЙ ПОДАРОК",
            new Vector2(-390f, -33f), new Vector2(740f, 48f), 30,
            Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        CreateText(header, "Demo Subtitle", "МАСТЕРСКАЯ ПОДАРКОВ  •  ДЕМОНСТРАЦИЯ ПРОГРЕССИИ",
            new Vector2(-390f, -78f), new Vector2(780f, 28f), 14,
            new Color32(207, 216, 226, 255), TextAnchor.MiddleLeft, FontStyle.Normal);
        headerDayText = CreateText(header, "Header Day", "ДЕНЬ 1 / 3",
            new Vector2(420f, -34f), new Vector2(230f, 34f), 20,
            new Color32(255, 225, 166, 255), TextAnchor.MiddleRight, FontStyle.Bold);
        headerCoinText = CreateText(header, "Header Coins", "МОНЕТЫ  0",
            new Vector2(630f, -76f), new Vector2(250f, 26f), 16,
            Color.white, TextAnchor.MiddleRight, FontStyle.Bold);

        RectTransform orderCard = CreateCard("Customer Order Card",
            new Vector2(-245f, -22f), new Vector2(900f, 650f));
        RectTransform progressCard = CreateCard("Progress Card",
            new Vector2(465f, -22f), new Vector2(420f, 650f));

        orderLabelText = CreateText(orderCard, "Order Label", "ТЕКУЩИЙ ЗАКАЗ",
            new Vector2(0f, 278f), new Vector2(820f, 28f), 16,
            new Color32(140, 112, 82, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
        customerNameText = CreateText(orderCard, "Customer Name", "Имя клиента",
            new Vector2(0f, 220f), new Vector2(820f, 64f), 42,
            new Color32(36, 49, 68, 255), TextAnchor.MiddleCenter, FontStyle.Bold);

        CreatePanel(orderCard, "Wish Card", new Vector2(0f, 100f),
            new Vector2(820f, 150f), new Color32(247, 244, 237, 255));
        CreateText(orderCard, "Wish Caption", "ПОЖЕЛАНИЕ КЛИЕНТА",
            new Vector2(0f, 145f), new Vector2(760f, 26f), 14,
            new Color32(143, 111, 77, 255), TextAnchor.MiddleLeft, FontStyle.Bold);
        customerWishText = CreateText(orderCard, "Customer Wish", "",
            new Vector2(0f, 82f), new Vector2(760f, 92f), 22,
            new Color32(57, 64, 72, 255), TextAnchor.MiddleLeft, FontStyle.Normal);

        CreateText(orderCard, "Gift List Caption", "ПОДАРКИ В ЗАКАЗЕ",
            new Vector2(-205f, -5f), new Vector2(400f, 30f), 16,
            new Color32(140, 112, 82, 255), TextAnchor.MiddleLeft, FontStyle.Bold);
        giftListText = CreateText(orderCard, "Gift List", "",
            new Vector2(-205f, -130f), new Vector2(400f, 220f), 18,
            new Color32(42, 54, 68, 255), TextAnchor.UpperLeft, FontStyle.Normal);

        CreatePanel(orderCard, "Gift Box Backdrop", new Vector2(275f, -110f),
            new Vector2(190f, 190f), new Color32(247, 244, 237, 255));
        CreatePanel(orderCard, "Gift Box Body", new Vector2(275f, -115f),
            new Vector2(112f, 104f), new Color32(205, 151, 77, 255));
        CreatePanel(orderCard, "Gift Box Ribbon Vertical", new Vector2(275f, -115f),
            new Vector2(18f, 104f), new Color32(155,  50, 62, 255));
        CreatePanel(orderCard, "Gift Box Ribbon Horizontal", new Vector2(275f, -115f),
            new Vector2(112f, 18f), new Color32(155,  50, 62, 255));
        CreateText(orderCard, "Gift Box Caption", "КОРОБКА",
            new Vector2(275f, -215f), new Vector2(190f, 26f), 14,
            new Color32(140, 112, 82, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
        boxSizeText = CreateText(orderCard, "Gift Box Size", "СРЕДНЯЯ",
            new Vector2(275f, -245f), new Vector2(190f, 24f), 16,
            new Color32(42, 54, 68, 255), TextAnchor.MiddleCenter, FontStyle.Bold);

        CreatePanel(orderCard, "Reward Bar", new Vector2(0f, -282f),
            new Vector2(820f, 48f), new Color32(235, 226, 207, 255));
        rewardText = CreateText(orderCard, "Reward Text", "НАГРАДА ЗА ЗАКАЗ",
            new Vector2(0f, -282f), new Vector2(790f, 42f), 18,
            new Color32(94, 72, 46, 255), TextAnchor.MiddleCenter, FontStyle.Bold);

        CreateText(progressCard, "Progress Caption", "ПРОГРЕСС КАМПАНИИ",
            new Vector2(0f, 278f), new Vector2(360f, 28f), 16,
            new Color32(140, 112, 82, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
        dayTitleText = CreateText(progressCard, "Day Title", "ДЕНЬ 1",
            new Vector2(0f, 220f), new Vector2(360f, 56f), 36,
            new Color32(36, 49, 68, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
        orderProgressText = CreateText(progressCard, "Order Progress", "ЗАКАЗ 1 ИЗ 3",
            new Vector2(0f, 170f), new Vector2(360f, 30f), 18,
            new Color32(86, 97, 108, 255), TextAnchor.MiddleCenter, FontStyle.Bold);

        CreatePanel(progressCard, "Coins Panel", new Vector2(0f, 110f),
            new Vector2(350f, 70f), new Color32(247, 244, 237, 255));
        CreateText(progressCard, "Coins Label", "ТЕКУЩИЙ БАЛАНС",
            new Vector2(0f, 128f), new Vector2(320f, 26f), 12,
            new Color32(140, 112, 82, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText(progressCard, "Coins Hint", "Монеты за выполненные заказы",
            new Vector2(0f, 94f), new Vector2(320f, 26f), 13,
            new Color32(75, 85, 95, 255), TextAnchor.MiddleCenter, FontStyle.Normal);

        CreateText(progressCard, "Timeline Caption", "ТРИ ДНЯ КАМПАНИИ",
            new Vector2(0f, 38f), new Vector2(350f, 26f), 14,
            new Color32(140, 112, 82, 255), TextAnchor.MiddleLeft, FontStyle.Bold);
        timelineText = CreateText(progressCard, "Timeline", "",
            new Vector2(0f, -38f), new Vector2(350f, 115f), 16,
            new Color32(49, 60, 73, 255), TextAnchor.UpperLeft, FontStyle.Normal);

        CreatePanel(progressCard, "Result Panel", new Vector2(0f, -145f),
            new Vector2(350f, 112f), new Color32(247, 244, 237, 255));
        resultText = CreateText(progressCard, "Result Text", "ВЫБЕРИТЕ РЕЗУЛЬТАТ УПАКОВКИ",
            new Vector2(0f, -145f), new Vector2(320f, 96f), 16,
            new Color32(60, 70, 80, 255), TextAnchor.MiddleCenter, FontStyle.Bold);

        successButton = CreateButton(progressCard, "Success Button", "УПАКОВАНО",
            new Vector2(-88f, -235f), new Vector2(166f, 48f),
            new Color32(55, 126, 91, 255), SubmitSuccessfulPacking);
        failButton = CreateButton(progressCard, "Fail Button", "ОШИБКА",
            new Vector2(88f, -235f), new Vector2(166f, 48f),
            new Color32(166, 73, 67, 255), SubmitFailedPacking);
        continueButton = CreateButton(progressCard, "Continue Button", "СЛЕДУЮЩИЙ ЗАКАЗ",
            new Vector2(0f, -295f), new Vector2(342f, 48f),
            new Color32(36, 49, 68, 255), ContinueToNextOrder);
        continueButton.gameObject.SetActive(false);

        CreateText(canvas.transform, "Footer Note",
            "ДЕМОРЕЖИМ: кнопки имитируют результат упаковки, чтобы показать работу заказов и наград.",
            new Vector2(0f, -430f), new Vector2(1350f, 30f), 15,
            new Color32(116, 108, 95, 255), TextAnchor.MiddleCenter, FontStyle.Normal);
    }

    /// <summary>
    /// The demo scene is intentionally self-contained and may not include a world camera.
    /// Create a simple camera so Unity's Game view has an active display renderer.
    /// </summary>
    private void EnsureGameViewCamera()
    {
        if (Camera.allCamerasCount > 0)
            return;

        GameObject cameraObject = new GameObject("Order Progression Demo Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera demoCamera = cameraObject.AddComponent<Camera>();
        demoCamera.clearFlags = CameraClearFlags.SolidColor;
        demoCamera.backgroundColor = new Color32(244, 239, 229, 255);
        demoCamera.orthographic = true;
        demoCamera.orthographicSize = 5f;
        demoCamera.nearClipPlane = 0.1f;
        demoCamera.farClipPlane = 100f;
        demoCamera.cullingMask = 0;
        demoCamera.targetDisplay = 0;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem",
            typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private RectTransform CreateCard(string name, Vector2 position, Vector2 size)
    {
        return CreatePanel(canvas.transform, name, position, size, Color.white);
    }

    private RectTransform CreatePanel(Transform parent, string name, Vector2 position,
        Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void CreateStretchPanel(string name, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Text CreateText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, int fontSize, Color color, TextAnchor alignment, FontStyle style)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * UiFontScale));
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(fontSize * 0.9f));
        text.resizeTextMaxSize = Mathf.Max(text.fontSize, Mathf.RoundToInt(fontSize * UiFontScale));
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Vector2 position, Vector2 size, Color color, Action action)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
        button.colors = colors;
        button.onClick.AddListener(() => action());

        Text labelText = CreateText(rect, name + " Label", label, Vector2.zero,
            size - new Vector2(12f, 8f), 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        labelText.raycastTarget = false;
        return button;
    }

    private void SubmitSuccessfulPacking()
    {
        SubmitPackingResult(true);
    }

    private void SubmitFailedPacking()
    {
        SubmitPackingResult(false);
    }

    private void SubmitPackingResult(bool success)
    {
        if (progression == null || progression.CurrentOrder == null || lastResult != null || finalSummary != null)
            return;

        int total = progression.CurrentOrder.GiftNames == null
            ? 0
            : progression.CurrentOrder.GiftNames.Count;

        PackingResult result = success
            ? new PackingResult(true, true, total, total)
            : new PackingResult(false, false, 0, total);

        progression.CompleteCurrentOrder(result);
    }

    private void ContinueToNextOrder()
    {
        if (progression == null || lastResult == null || finalSummary != null)
            return;

        progression.ContinueAfterResult();
        lastResult = null;
        RefreshInterface();
    }

    private void RefreshInterface()
    {
        if (progression == null || headerDayText == null)
            return;

        headerCoinText.text = "МОНЕТЫ  " + progression.CurrentCoins;
        timelineText.text = BuildTimelineText();

        if (finalSummary != null)
        {
            headerDayText.text = "ФИНАЛ КАМПАНИИ";
            dayTitleText.text = "ФИНАЛ";
            orderProgressText.text = "ВСЕ " + finalSummary.DaysPlayed + " ДНЯ ПРОЙДЕНЫ";
            orderLabelText.text = "РОЖДЕСТВЕНСКАЯ КАМПАНИЯ ЗАВЕРШЕНА";
            customerNameText.text = "Все подарки готовы!";
            customerWishText.text = "Вы прошли все дни и выполнили заказы клиентов. Отличная работа!";
            giftListText.text = "УСПЕШНЫЕ ЗАКАЗЫ: " + finalSummary.OrdersCompleted + "\nЗАРАБОТАНО МОНЕТ: " + finalSummary.TotalCoinsEarned;
            boxSizeText.text = "ИТОГ: " + finalSummary.FinalCoins + " МОНЕТ";
            rewardText.text = "КАМПАНИЯ ПРОЙДЕНА";
            resultText.text = "ВСЕ ЗАКАЗЫ ОБРАБОТАНЫ\nИТОГОВЫЙ БАЛАНС: " + finalSummary.FinalCoins;
            successButton.gameObject.SetActive(false);
            failButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            return;
        }

        GiftOrderDefinition order = progression.CurrentOrder;
        if (order == null)
        {
            headerDayText.text = "КАМПАНИЯ";
            dayTitleText.text = "КАМПАНИЯ";
            orderProgressText.text = "НЕТ АКТИВНОГО ЗАКАЗА";
            successButton.gameObject.SetActive(false);
            failButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            return;
        }

        headerDayText.text = "ДЕНЬ " + progression.CurrentDay + " / " + campaign.Days.Count;
        dayTitleText.text = "ДЕНЬ " + progression.CurrentDay;
        int totalOrders = progression.CurrentDayData == null ? 0 : progression.CurrentDayData.Orders.Count;
        orderProgressText.text = "ЗАКАЗ " + progression.CurrentOrderNumber + " ИЗ " + totalOrders;
        orderLabelText.text = "ЗАКАЗ ДЛЯ КЛИЕНТА  •  ДЕНЬ " + order.Day;
        customerNameText.text = order.CustomerName;
        customerWishText.text = string.IsNullOrEmpty(order.CustomerWish)
            ? "У клиента пока нет особых пожеланий."
            : "«" + order.CustomerWish + "»";

        giftListText.text = BuildGiftList(order);
        boxSizeText.text = GetBoxSizeLabel(order.BoxSize);
        rewardText.text = "НАГРАДА  " + order.RewardCoins + " МОНЕТ   +   БОНУС  " + order.PerfectPackingTip;

        successButton.gameObject.SetActive(lastResult == null);
        failButton.gameObject.SetActive(lastResult == null);
        continueButton.gameObject.SetActive(lastResult != null);

        if (lastResult == null)
        {
            resultText.text = "ПОСЛЕ УПАКОВКИ\nНажмите одну из кнопок ниже, чтобы увидеть результат.";
        }
        else if (lastResult.Success)
        {
            resultText.text = "ЗАКАЗ ВЫПОЛНЕН!\n+" + lastResult.TotalEarned + " МОНЕТ";
        }
        else
        {
            resultText.text = "УПАКОВКА НЕ УДАЛАСЬ\nНАГРАДА НЕ НАЧИСЛЕНА";
        }
    }

    private string BuildGiftList(GiftOrderDefinition order)
    {
        if (order.GiftNames == null || order.GiftNames.Count == 0)
            return "В этом заказе пока нет подарков.";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < order.GiftNames.Count; i++)
            builder.Append("   ").Append(i + 1).Append(". ").Append(order.GiftNames[i]).AppendLine();

        return builder.ToString();
    }

    private string BuildTimelineText()
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < campaign.Days.Count; i++)
        {
            OrderDayDefinition day = campaign.Days[i];
            DaySummaryData summary = completedDays.Find(item => item.Day == day.DayNumber);

            if (summary != null)
            {
                builder.Append("✓  ДЕНЬ ").Append(day.DayNumber)
                    .Append(" — ").Append(summary.OrdersCompleted).Append("/")
                    .Append(summary.OrdersTotal).AppendLine(" заказов");
            }
            else if (finalSummary == null && progression.CurrentDay == day.DayNumber)
            {
                builder.Append("▶  ДЕНЬ ").Append(day.DayNumber)
                    .Append(" — заказ ").Append(progression.CurrentOrderNumber)
                    .Append("/").Append(day.Orders.Count).AppendLine();
            }
            else
            {
                builder.Append("○  ДЕНЬ ").Append(day.DayNumber).AppendLine();
            }
        }

        return builder.ToString();
    }

    private string GetBoxSizeLabel(GiftBoxSize size)
    {
        switch (size)
        {
            case GiftBoxSize.Small:
                return "МАЛЕНЬКАЯ";
            case GiftBoxSize.Medium:
                return "СРЕДНЯЯ";
            case GiftBoxSize.Large:
                return "БОЛЬШАЯ";
            default:
                return size.ToString().ToUpperInvariant();
        }
    }
}
