using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TheLastGift.UI
{
    public sealed class LastGiftUIController : MonoBehaviour
    {
        public Action ConfirmPacking, Continue, StartGame;

        [Header("Настройки интерфейса")]
        [SerializeField, Range(0.8f, 1.8f)]
        [Tooltip("Общий размер шрифта игрового интерфейса.")]
        private float uiFontScale = 1.25f;

        [SerializeField, Range(0.65f, 1f)]
        [Tooltip("Минимальный размер шрифта при автоматической подгонке текста.")]
        private float minimumFontScale = 0.85f;

        [SerializeField]
        [Tooltip("Необязательно: собственный шрифт. Если поле пустое, используется встроенный шрифт Unity.")]
        private Font customFont;

        private Canvas canvas;
        private Font font;
        private GameObject root;
        private Text feedback;
        private RectTransform packingRect;
        private Text packingHint;
        private int day = 1;
        private int coins = 0;
        private int order = 1;
        private int placedGiftCount = 0;
        private readonly List<string> placedGiftNames = new List<string>();
        private bool tutorialShown = false;

        private sealed class CustomerOrderData
        {
            public readonly string CustomerName;
            public readonly string Wish;
            public readonly string[] RequiredGifts;

            public CustomerOrderData(string customerName, string wish, params string[] requiredGifts)
            {
                CustomerName = customerName;
                Wish = wish;
                RequiredGifts = requiredGifts;
            }
        }

        private static readonly string[] AllGiftNames =
        {
            "МИШКА", "ПОДАРОК", "ЛЕДЕНЕЦ", "СНЕЖНЫЙ ШАР"
        };

        private CustomerOrderData currentOrder;

        private Sprite teddySprite;
        private Sprite giftBoxSprite;
        private Sprite candyCaneSprite;
        private Sprite snowGlobeSprite;
        private Texture2D giftAtlas;

        private readonly Color cream = new Color32(250, 239, 215, 255);
        private readonly Color paper = new Color32(255, 249, 235, 255);
        private readonly Color brown = new Color32(82, 52, 36, 255);
        private readonly Color wood = new Color32(125, 82, 52, 255);
        private readonly Color red = new Color32(159, 52, 48, 255);
        private readonly Color green = new Color32(48, 96, 65, 255);
        private readonly Color gold = new Color32(218, 166, 74, 255);
        private readonly Color paleBlue = new Color32(224, 239, 244, 255);

        private void Awake()
        {
            EnsureCamera();
            font = customFont != null
                ? customFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadGiftSprites();
            SetupCanvas();
            MainMenu();
        }

        // A Screen Space Overlay canvas can display without a camera, but the
        // Game view still reports "No cameras rendering". Supply a basic 2D
        // camera for scenes where the prototype UI was added to an empty scene.
        private void EnsureCamera()
        {
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
                sceneCamera = FindObjectOfType<Camera>();

            if (sceneCamera != null)
            {
                sceneCamera.enabled = true;
                return;
            }

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera fallbackCamera = cameraObject.GetComponent<Camera>();
            fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
            fallbackCamera.backgroundColor = new Color32(38, 29, 25, 255);
            fallbackCamera.orthographic = true;
            fallbackCamera.orthographicSize = 5f;
            fallbackCamera.nearClipPlane = 0.1f;
            fallbackCamera.farClipPlane = 1000f;
            fallbackCamera.depth = -10f;
            fallbackCamera.enabled = true;
        }

        private void LoadGiftSprites()
        {
            TextAsset iconData = Resources.Load<TextAsset>("LastGift/GiftIconsBase64");
            if (iconData == null)
            {
                Debug.LogError("Не найден набор картинок подарков: Resources/LastGift/GiftIconsBase64.txt");
                return;
            }

            try
            {
                byte[] imageBytes = Convert.FromBase64String(iconData.text.Trim());
                giftAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(giftAtlas, imageBytes, false))
                {
                    Debug.LogError("Не удалось загрузить картинки подарков.");
                    Destroy(giftAtlas);
                    giftAtlas = null;
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("Ошибка чтения картинок подарков: " + exception.Message);
                return;
            }

            teddySprite = Sprite.Create(giftAtlas, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 96);
            giftBoxSprite = Sprite.Create(giftAtlas, new Rect(64, 0, 64, 64), new Vector2(0.5f, 0.5f), 96);
            candyCaneSprite = Sprite.Create(giftAtlas, new Rect(0, 64, 64, 64), new Vector2(0.5f, 0.5f), 96);
            snowGlobeSprite = Sprite.Create(giftAtlas, new Rect(64, 64, 64, 64), new Vector2(0.5f, 0.5f), 96);
        }

        private void SetupCanvas()
        {
            canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(
                    "LastGift Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            else if (!eventSystem.GetComponent<BaseInputModule>())
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private void Build(string screenName)
        {
            if (root != null)
                Destroy(root);

            root = new GameObject(screenName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Panel(root.transform, "Фон", Vector2.zero, Vector2.one, cream);
            Panel(root.transform, "Деревянная рамка", new Vector2(0.012f, 0.012f), new Vector2(0.988f, 0.988f), wood);
            Panel(root.transform, "Бумажный фон", new Vector2(0.019f, 0.019f), new Vector2(0.981f, 0.981f), cream);

            Label("❄     ✦     ❅     ✦     ❄     ✦     ❅     ✦     ❄", 20, red,
                new Vector2(0.02f, 0.925f), new Vector2(0.98f, 0.985f), TextAnchor.MiddleCenter);
            Label("✦     ❅     ✦     ❄     ✦     ❅     ✦     ❄", 18, green,
                new Vector2(0.02f, 0.025f), new Vector2(0.98f, 0.075f), TextAnchor.MiddleCenter);
        }

        public void MainMenu()
        {
            Build("Главное меню");
            Card(
                "Добро пожаловать в «Последний подарок»",
                "Ты открыла маленькую рождественскую лавку!\nСобирай подарки по пожеланиям покупателей.\nЗа выполненные заказы получай монеты.\nВыбирай подарки и перетаскивай их в коробку мышью.");

            ButtonAt("НАЧАТЬ ИГРУ", green, () =>
            {
                if (!tutorialShown)
                    ShowTutorial();
                else
                {
                    StartGame?.Invoke();
                    Gameplay();
                }
            }, new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.275f));

            ButtonAt("ВЫЙТИ", wood, Application.Quit,
                new Vector2(0.41f, 0.085f), new Vector2(0.59f, 0.16f));
        }

        public void ShowTutorial()
        {
            Build("Обучение");
            Card(
                "Как играть",
                "1. Слева прочитай, что нужно покупателю.\n2. Справа выбери нужные подарки.\n3. Зажми подарок мышью и перетащи его в коробку по центру.\n4. Когда всё готово, нажми «ПРОВЕРИТЬ ЗАКАЗ».\n\nПеретаскивай подарки по одному.");

            ButtonAt("ПОНЯТНО, НАЧИНАЕМ!", green, () =>
            {
                tutorialShown = true;
                StartGame?.Invoke();
                Gameplay();
            }, new Vector2(0.27f, 0.085f), new Vector2(0.73f, 0.19f));
        }

        public void Gameplay()
        {
            if (currentOrder == null)
            {
                SetStatus("Не удалось загрузить заказ. Вернись в меню и начни игру заново.", false);
                return;
            }

            Build("Игровой экран");
            placedGiftCount = 0;
            placedGiftNames.Clear();

            Panel(root.transform, "Верхняя панель", new Vector2(0.02f, 0.875f), new Vector2(0.98f, 0.925f), wood);
            Label("ПОСЛЕДНИЙ ПОДАРОК ✦", 25, cream,
                new Vector2(0.035f, 0.875f), new Vector2(0.34f, 0.925f), TextAnchor.MiddleLeft);
            Label("ДЕНЬ " + day, 21, cream,
                new Vector2(0.37f, 0.875f), new Vector2(0.51f, 0.925f), TextAnchor.MiddleCenter);
            Label("✦ " + coins, 21, gold,
                new Vector2(0.54f, 0.875f), new Vector2(0.66f, 0.925f), TextAnchor.MiddleCenter);
            Label("ЗАКАЗ №" + order.ToString("00"), 21, cream,
                new Vector2(0.75f, 0.875f), new Vector2(0.96f, 0.925f), TextAnchor.MiddleRight);

            BuildCustomerPanel();
            BuildPackingArea();
            BuildGiftShelf();

            feedback = Label("Перетащи подарки с правой полки в коробку по центру.",
                18, brown, new Vector2(0.285f, 0.18f), new Vector2(0.715f, 0.235f), TextAnchor.MiddleCenter);

            ButtonAt("В МЕНЮ", wood, MainMenu,
                new Vector2(0.035f, 0.075f), new Vector2(0.19f, 0.145f));

            ButtonAt("ПРОВЕРИТЬ ЗАКАЗ", green, CheckOrder,
                new Vector2(0.405f, 0.065f), new Vector2(0.665f, 0.155f));

            ButtonAt("ОЧИСТИТЬ КОРОБКУ", red, ClearPackingBox,
                new Vector2(0.70f, 0.075f), new Vector2(0.965f, 0.145f));
        }

        private void BuildCustomerPanel()
        {
            Panel(root.transform, "Карточка покупателя",
                new Vector2(0.025f, 0.205f), new Vector2(0.265f, 0.855f), paper);

            Label("ВАШ ПОКУПАТЕЛЬ", 18, green,
                new Vector2(0.04f, 0.795f), new Vector2(0.25f, 0.845f), TextAnchor.MiddleCenter);

            Panel(root.transform, "Портрет покупателя",
                new Vector2(0.055f, 0.66f), new Vector2(0.125f, 0.775f), gold);
            Label("☺", 66, red,
                new Vector2(0.055f, 0.66f), new Vector2(0.125f, 0.775f), TextAnchor.MiddleCenter);
            Label(currentOrder.CustomerName, 18, brown,
                new Vector2(0.135f, 0.69f), new Vector2(0.25f, 0.77f), TextAnchor.MiddleCenter);

            Panel(root.transform, "Пожелание", new Vector2(0.045f, 0.47f), new Vector2(0.245f, 0.65f), cream);
            Label(currentOrder.Wish, 17, brown,
                new Vector2(0.05f, 0.475f), new Vector2(0.24f, 0.645f), TextAnchor.MiddleCenter);

            Label("В ЗАКАЗЕ НУЖНО", 17, green,
                new Vector2(0.045f, 0.425f), new Vector2(0.245f, 0.465f), TextAnchor.MiddleCenter);

            float itemWidth = 0.06f;
            float itemGap = 0.006f;
            float totalWidth = currentOrder.RequiredGifts.Length * itemWidth
                + (currentOrder.RequiredGifts.Length - 1) * itemGap;
            float itemStart = 0.145f - totalWidth * 0.5f;

            for (int i = 0; i < currentOrder.RequiredGifts.Length; i++)
            {
                string gift = currentOrder.RequiredGifts[i];
                float x = itemStart + i * (itemWidth + itemGap);
                AddRequestedGift(GetGiftSprite(gift), GetGiftDisplayName(gift),
                    new Vector2(x, 0.285f), new Vector2(x + itemWidth, 0.415f));
            }
        }

        private void BuildPackingArea()
        {
            Label("КОРОБКА ДЛЯ УПАКОВКИ", 21, green,
                new Vector2(0.29f, 0.785f), new Vector2(0.71f, 0.84f), TextAnchor.MiddleCenter);
            Label("Перетаскивай сюда подарки с полки справа", 16, brown,
                new Vector2(0.29f, 0.745f), new Vector2(0.71f, 0.785f), TextAnchor.MiddleCenter);

            Panel(root.transform, "Стол упаковки",
                new Vector2(0.285f, 0.265f), new Vector2(0.715f, 0.725f), new Color32(216, 190, 156, 255));

            packingRect = Panel(root.transform, "Коробка",
                new Vector2(0.315f, 0.295f), new Vector2(0.685f, 0.695f), paper);

            for (int i = 1; i < 3; i++)
            {
                float p = i / 3f;
                Panel(packingRect, "Вертикальная линия",
                    new Vector2(p - 0.0015f, 0f), new Vector2(p + 0.0015f, 1f), new Color32(225, 209, 181, 255));
                Panel(packingRect, "Горизонтальная линия",
                    new Vector2(0f, p - 0.002f), new Vector2(1f, p + 0.002f), new Color32(225, 209, 181, 255));
            }

            packingHint = Label("ПЕРЕТАЩИ СЮДА ПОДАРОК", 19, wood,
                new Vector2(0.32f, 0.43f), new Vector2(0.68f, 0.56f), TextAnchor.MiddleCenter);
        }

        private void BuildGiftShelf()
        {
            Panel(root.transform, "Полка с подарками",
                new Vector2(0.735f, 0.205f), new Vector2(0.975f, 0.855f), paper);
            Label("ПОДАРКИ НА ПОЛКЕ", 18, green,
                new Vector2(0.75f, 0.795f), new Vector2(0.96f, 0.845f), TextAnchor.MiddleCenter);
            Label("Зажми предмет и перетащи его", 14, brown,
                new Vector2(0.75f, 0.755f), new Vector2(0.96f, 0.795f), TextAnchor.MiddleCenter);

            AddShelfGift("МИШКА", teddySprite, new Vector2(0.755f, 0.535f), new Vector2(0.855f, 0.735f), wood);
            AddShelfGift("ПОДАРОК", giftBoxSprite, new Vector2(0.865f, 0.535f), new Vector2(0.965f, 0.735f), red);
            AddShelfGift("ЛЕДЕНЕЦ", candyCaneSprite, new Vector2(0.755f, 0.305f), new Vector2(0.855f, 0.505f), red);
            AddShelfGift("СНЕЖНЫЙ ШАР", snowGlobeSprite, new Vector2(0.865f, 0.305f), new Vector2(0.965f, 0.505f), green);
        }

        private void AddRequestedGift(Sprite sprite, string caption, Vector2 min, Vector2 max)
        {
            Image image = ImageAt(root.transform, "Подарок в заказе", min, max, sprite, Color.white);
            if (image == null)
            {
                Label(caption, 13, brown, min, max, TextAnchor.MiddleCenter);
                return;
            }

            image.preserveAspect = true;
            Label(caption, 13, brown,
                new Vector2(min.x - 0.005f, min.y - 0.04f),
                new Vector2(max.x + 0.005f, min.y + 0.005f),
                TextAnchor.MiddleCenter);
        }

        private void AddShelfGift(string caption, Sprite sprite, Vector2 min, Vector2 max, Color tileColor)
        {
            GameObject tile = new GameObject("Подарок " + caption, typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(root.transform, false);

            RectTransform rect = tile.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = tile.GetComponent<Image>();
            background.color = tileColor;
            background.raycastTarget = true;

            Image icon = ImageAt(tile.transform, "Картинка " + caption,
                new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.94f), sprite, Color.white);

            if (icon == null)
            {
                ChildLabel(tile.transform, caption, 15, paper, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.70f), TextAnchor.MiddleCenter);
            }
            else
            {
                icon.preserveAspect = true;
                Text captionText = ChildLabel(tile.transform, caption, 13, paper,
                    new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.24f), TextAnchor.MiddleCenter);
                captionText.raycastTarget = false;
            }

            LastGiftDraggableGift draggable = tile.AddComponent<LastGiftDraggableGift>();
            draggable.Initialize(this, sprite, caption);
        }

        private void CheckOrder()
        {
            if (placedGiftCount == 0)
            {
                SetStatus("Сначала перетащи подарки с полки в коробку.", false);
                return;
            }

            List<string> missing = new List<string>();
            List<string> unexpected = new List<string>();

            for (int i = 0; i < AllGiftNames.Length; i++)
            {
                string gift = AllGiftNames[i];
                int requiredCount = CountGift(currentOrder.RequiredGifts, gift);
                int placedCount = CountGift(placedGiftNames, gift);

                if (placedCount < requiredCount)
                    missing.Add(GetGiftDisplayName(gift) + " (ещё " + (requiredCount - placedCount) + ")");
                else if (placedCount > requiredCount)
                    unexpected.Add(GetGiftDisplayName(gift) + " (лишний ×" + (placedCount - requiredCount) + ")");
            }

            if (missing.Count > 0 || unexpected.Count > 0)
            {
                string message = "Заказ пока собран неверно. ";
                if (missing.Count > 0)
                    message += "Не хватает: " + string.Join(", ", missing.ToArray()) + ". ";
                if (unexpected.Count > 0)
                    message += "Лишние подарки: " + string.Join(", ", unexpected.ToArray()) + ".";
                SetStatus(message, false);
                return;
            }

            if (ConfirmPacking != null)
                ConfirmPacking.Invoke();
            else
                OrderSuccess(25);
        }

        public int PlacedGiftCount => placedGiftCount;

        /// <summary>Applies an order supplied by OrderProgressionManager.</summary>
        public void SetProgressionOrder(GiftOrderDefinition orderData, int currentDay,
            int currentOrderNumber, int currentBalance)
        {
            if (orderData == null)
            {
                currentOrder = null;
                return;
            }

            List<string> requiredGifts = new List<string>();
            if (orderData.GiftNames != null)
            {
                for (int i = 0; i < orderData.GiftNames.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(orderData.GiftNames[i]))
                        requiredGifts.Add(orderData.GiftNames[i].Trim().ToUpperInvariant());
                }
            }

            currentOrder = new CustomerOrderData(
                orderData.CustomerName,
                orderData.CustomerWish,
                requiredGifts.ToArray());

            day = currentDay;
            order = currentOrderNumber;
            coins = currentBalance;
        }

        public void SetProgressionBalance(int currentBalance)
        {
            coins = currentBalance;
        }

        private void ClearPackingBox()
        {
            if (packingRect == null)
                return;

            for (int i = packingRect.childCount - 1; i >= 0; i--)
            {
                Transform child = packingRect.GetChild(i);
                if (child.name.StartsWith("Упакованный подарок", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }

            placedGiftCount = 0;
            placedGiftNames.Clear();
            if (packingHint != null)
                packingHint.gameObject.SetActive(true);

            SetStatus("Коробка очищена. Можно собрать заказ заново.", true);
        }

        private int CountGift(IList<string> gifts, string giftName)
        {
            int count = 0;
            for (int i = 0; i < gifts.Count; i++)
            {
                if (string.Equals(gifts[i], giftName, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }

        private Sprite GetGiftSprite(string giftName)
        {
            switch (giftName)
            {
                case "МИШКА": return teddySprite;
                case "ПОДАРОК": return giftBoxSprite;
                case "ЛЕДЕНЕЦ": return candyCaneSprite;
                case "СНЕЖНЫЙ ШАР": return snowGlobeSprite;
                default: return null;
            }
        }

        private string GetGiftDisplayName(string giftName)
        {
            switch (giftName)
            {
                case "МИШКА": return "мишка";
                case "ПОДАРОК": return "подарок";
                case "ЛЕДЕНЕЦ": return "леденец";
                case "СНЕЖНЫЙ ШАР": return "снежный шар";
                default: return giftName.ToLowerInvariant();
            }
        }

        public void TryDropGift(Sprite sprite, string giftName, Vector2 screenPosition)
        {
            if (packingRect == null || !RectTransformUtility.RectangleContainsScreenPoint(packingRect, screenPosition, null))
            {
                SetStatus("Отпусти подарок над коробкой, чтобы положить его внутрь.", false);
                return;
            }

            if (sprite == null)
            {
                SetStatus("Не удалось загрузить картинку подарка.", false);
                return;
            }

            if (placedGiftCount >= 9)
            {
                SetStatus("Коробка заполнена. В ней больше нет места.", false);
                return;
            }

            int column = placedGiftCount % 3;
            int row = placedGiftCount / 3;

            GameObject placed = new GameObject("Упакованный подарок " + giftName, typeof(RectTransform), typeof(Image));
            placed.transform.SetParent(packingRect, false);

            RectTransform rect = placed.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(column / 3f, row / 3f);
            rect.anchorMax = new Vector2((column + 1) / 3f, (row + 1) / 3f);
            rect.offsetMin = new Vector2(7, 7);
            rect.offsetMax = new Vector2(-7, -7);

            Image image = placed.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            placedGiftCount++;
            placedGiftNames.Add(giftName);

            if (packingHint != null)
                packingHint.gameObject.SetActive(false);

            SetStatus("Подарок «" + giftName + "» помещён в коробку. Можно добавить ещё.", true);
        }

        public GameObject CreateDragPreview(Sprite sprite, Vector2 screenPosition)
        {
            if (sprite == null)
                return null;

            GameObject preview = new GameObject("Перетаскиваемый подарок", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            preview.transform.SetParent(canvas.transform, false);

            RectTransform rect = preview.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(125, 125);
            rect.position = screenPosition;

            Image image = preview.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            CanvasGroup group = preview.GetComponent<CanvasGroup>();
            group.alpha = 0.85f;
            group.blocksRaycasts = false;

            preview.transform.SetAsLastSibling();
            return preview;
        }

        public void MoveDragPreview(GameObject preview, Vector2 screenPosition)
        {
            if (preview != null)
                preview.transform.position = screenPosition;
        }

        public void PackingFeedback(string message, bool success)
        {
            SetStatus(message, success);
            if (success && feedback != null)
                StartCoroutine(Pop(feedback.transform));
        }

        public void OrderSuccess(int earned)
        {
            coins += earned;
            order++;
            currentOrder = null;
            Build("Заказ выполнен");
            RectTransform successCard = Card(
                "Заказ выполнен!",
                "Покупатель доволен!\nТы заработала монеты и помогла сделать чей-то праздник теплее.");
            ChildLabel(successCard, "+ " + earned + " МОНЕТ ✦", 44, gold,
                new Vector2(0.10f, 0.21f), new Vector2(0.90f, 0.41f), TextAnchor.MiddleCenter);
            Button("СЛЕДУЮЩИЙ ПОКУПАТЕЛЬ", green, () =>
            {
                Continue?.Invoke();
                Gameplay();
            }, 0, -250, 430, 70);
        }

        public void DayEnd(int orders, int earnings)
        {
            day++;
            Build("Конец дня");
            Card("Рабочий день окончен!", "Выполнено заказов: " + orders + "\nЗаработано монет: " + earnings + "\n\nОтличная работа! Завтра лавка снова откроется.");
            Button("НАЧАТЬ НОВЫЙ ДЕНЬ", green, () =>
            {
                Continue?.Invoke();
                Gameplay();
            }, 0, -250, 380, 70);
        }

        public void FinalScreen(GameSummaryData summary)
        {
            Build("Финал");
            string body = summary == null
                ? "Спасибо, что наполнила лавку теплом и заботой!\\nПусть в каждом подарке будет немного рождественского чуда."
                : "Ты завершила все три дня!\\nВыполнено заказов: " + summary.OrdersCompleted
                    + "\\nЗаработано монет: " + summary.TotalCoinsEarned
                    + "\\nИтоговый баланс: " + summary.FinalCoins
                    + "\\n\\nСпасибо, что наполнила лавку рождественским чудом!";
            Card("Последний подарок", body);
            Button("ИГРАТЬ СНОВА", green, () =>
            {
                day = 1;
                coins = 0;
                order = 1;
                currentOrder = null;
                placedGiftCount = 0;
                placedGiftNames.Clear();
                StartGame?.Invoke();
                MainMenu();
            }, 0, -250, 340, 70);
        }

        public void FinalScreen()
        {
            FinalScreen(null);
        }

        private void SetStatus(string message, bool success)
        {
            if (feedback == null)
                return;

            feedback.text = message;
            feedback.color = success ? green : red;
        }

        private RectTransform Card(string title, string body)
        {
            bool isTutorial = title == "Как играть";
            bool isOrderSuccess = title == "Заказ выполнен!";
            Vector2 cardMin = isTutorial
                ? new Vector2(0.10f, 0.28f)
                : isOrderSuccess ? new Vector2(0.12f, 0.26f) : new Vector2(0.12f, 0.36f);
            Vector2 cardMax = isTutorial
                ? new Vector2(0.90f, 0.90f)
                : isOrderSuccess ? new Vector2(0.88f, 0.92f) : new Vector2(0.88f, 0.88f);

            Panel(root.transform, "Тень карточки",
                cardMin, cardMax, new Vector2(14, -12), new Vector2(14, -12), wood);
            RectTransform card = Panel(root.transform, "Карточка",
                cardMin, cardMax, Vector2.zero, Vector2.zero, paper);

            // Text is parented to the card itself, not positioned over the whole
            // screen. This keeps the title and body inside the panel at every size.
            CreateText(card, title, isTutorial ? 40 : 42, green,
                new Vector2(0.055f, isTutorial || isOrderSuccess ? 0.84f : 0.77f),
                new Vector2(0.945f, 0.97f), TextAnchor.MiddleCenter, Vector2.zero);

            Vector2 bodyMin = isOrderSuccess ? new Vector2(0.075f, 0.44f) : new Vector2(0.075f, 0.10f);
            Vector2 bodyMax = isOrderSuccess
                ? new Vector2(0.925f, 0.79f)
                : new Vector2(0.925f, isTutorial ? 0.81f : 0.70f);
            CreateText(card, body, isTutorial ? 27 : 29, brown,
                bodyMin, bodyMax, TextAnchor.MiddleCenter, Vector2.zero);

            return card;
        }

        private RectTransform Panel(Transform parent, string objectName, Vector2 min, Vector2 max, Color color)
        {
            return Panel(parent, objectName, min, max, Vector2.zero, Vector2.zero, color);
        }

        private RectTransform Panel(Transform parent, string objectName, Vector2 min, Vector2 max, Vector2 size, Vector2 position, Color color)
        {
            GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(string value, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment)
        {
            return CreateText(root.transform, value, size, color, min, max, alignment, Vector2.zero);
        }

        private Text ChildLabel(Transform parent, string value, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment)
        {
            return CreateText(parent, value, size, color, min, max, alignment, Vector2.zero);
        }

        private Text CreateText(Transform parent, string value, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment, Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject("Текст", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            int readableSize = Mathf.Max(14, Mathf.RoundToInt(size * uiFontScale));
            int minimumReadableSize = Mathf.Min(readableSize,
                Mathf.Max(12, Mathf.RoundToInt(size * minimumFontScale)));
            text.fontSize = readableSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumReadableSize;
            text.resizeTextMaxSize = readableSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Image ImageAt(Transform parent, string objectName, Vector2 min, Vector2 max, Sprite sprite, Color color)
        {
            if (sprite == null)
                return null;

            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void Button(string caption, Color color, Action action, float x, float y, float width, float height)
        {
            GameObject buttonObject = new GameObject("Кнопка " + caption, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);

            Image background = buttonObject.GetComponent<Image>();
            background.color = color;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = Color.Lerp(color, Color.white, 0.12f);
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());

            Text label = ChildLabel(buttonObject.transform, caption, 18, paper,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
        }

        private void ButtonAt(string caption, Color color, Action action, Vector2 min, Vector2 max)
        {
            GameObject buttonObject = new GameObject("Кнопка " + caption, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = buttonObject.GetComponent<Image>();
            background.color = color;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = Color.Lerp(color, Color.white, 0.12f);
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());

            Text label = ChildLabel(buttonObject.transform, caption, 18, paper,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
        }

        private IEnumerator Pop(Transform target)
        {
            target.localScale = Vector3.one * 0.85f;
            float elapsed = 0f;

            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (target != null)
                    target.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, elapsed / 0.2f);
                yield return null;
            }

            if (target != null)
                target.localScale = Vector3.one;
        }
    }
}