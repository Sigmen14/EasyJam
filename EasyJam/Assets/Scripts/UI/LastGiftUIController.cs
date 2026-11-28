using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TheLastGift.Audio;

namespace TheLastGift.UI
{
    public sealed class LastGiftUIController : MonoBehaviour
    {
        public Action ConfirmPacking, Continue, StartGame;
        public Action<GiftWrappingSelection> ConfirmWrapping;

        private const string PackingMiniGameSceneName = "SampleScene";
        private static LastGiftUIController activeInstance;
        private Camera shopCamera;
        private bool packingMiniGameOpen;
        private GameObject wrappingRoot;
        private bool wrappingSubmitted;
        private GiftWrappingPaper selectedPaper;
        private GiftWrappingRibbon selectedRibbon;
        private GiftWrappingCard selectedCard;

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
        private Texture2D christmasBackgroundTexture;
        private Sprite christmasBackground;
        private GameObject root;
        private Text feedback;
        private RectTransform packingRect;
        private Text packingHint;
        private int day = 1;
        private int coins = 0;
        private int reputation = 0;
        private int order = 1;
        private int placedGiftCount = 0;
        private readonly List<string> placedGiftNames = new List<string>();
        private bool tutorialShown = false;

        private sealed class CustomerOrderData
        {
            public readonly string CustomerName;
            public readonly string Wish;
            public readonly string[] RequiredGifts;
            public readonly GiftBoxSize BoxSize;
            public readonly GiftOrderDefinition Definition;

            public CustomerOrderData(GiftOrderDefinition definition, params string[] requiredGifts)
            {
                Definition = definition;
                CustomerName = definition.CustomerName;
                Wish = definition.CustomerWish;
                BoxSize = definition.BoxSize;
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
        private Texture2D customerPortraitAtlas;
        private readonly Sprite[] customerPortraits = new Sprite[9];

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
            activeInstance = this;
            EnsureCamera();
            LastGiftAudioManager.EnsureInstance();
            font = customFont != null
                ? customFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadGiftSprites();
            LoadCustomerPortraits();
            christmasBackgroundTexture = LastGiftChristmasBackground.CreateTexture();
            christmasBackground = Sprite.Create(
                christmasBackgroundTexture,
                new Rect(0f, 0f, christmasBackgroundTexture.width, christmasBackgroundTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            SetupCanvas();
            MainMenu();
        }

        private void OnDestroy()
        {
            if (christmasBackground != null)
                Destroy(christmasBackground);
            if (christmasBackgroundTexture != null)
                Destroy(christmasBackgroundTexture);

            for (int i = 0; i < customerPortraits.Length; i++)
            {
                if (customerPortraits[i] != null)
                    Destroy(customerPortraits[i]);
            }

            if (activeInstance == this)
                activeInstance = null;
        }

        // A Screen Space Overlay canvas can display without a camera, but the
        // Game view still reports "No cameras rendering". Supply a basic 2D
        // camera for scenes where the prototype UI was added to an empty scene.
        private void EnsureCamera()
        {
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
                sceneCamera = FindFirstObjectByType<Camera>();

            if (sceneCamera != null)
            {
                sceneCamera.enabled = true;
                shopCamera = sceneCamera;
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
            shopCamera = fallbackCamera;
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

        private void LoadCustomerPortraits()
        {
            customerPortraitAtlas = Resources.Load<Texture2D>("LastGift/CustomerPortraits");
            if (customerPortraitAtlas == null)
            {
                Debug.LogWarning("Не найден набор портретов покупателей: Resources/LastGift/CustomerPortraits.jpg");
                return;
            }

            customerPortraitAtlas.filterMode = FilterMode.Bilinear;
            customerPortraitAtlas.wrapMode = TextureWrapMode.Clamp;

            int cellWidth = customerPortraitAtlas.width / 3;
            int cellHeight = customerPortraitAtlas.height / 3;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    int portraitIndex = row * 3 + column;
                    int textureY = (2 - row) * cellHeight;
                    Rect portraitRect = new Rect(
                        column * cellWidth,
                        textureY,
                        cellWidth,
                        cellHeight);

                    customerPortraits[portraitIndex] = Sprite.Create(
                        customerPortraitAtlas,
                        portraitRect,
                        new Vector2(0.5f, 0.5f),
                        96f);
                    customerPortraits[portraitIndex].name = "Портрет покупателя " + (portraitIndex + 1);
                }
            }
        }

        private Sprite GetCurrentCustomerPortrait()
        {
            if (currentOrder == null || customerPortraits.Length == 0)
                return null;

            // Match each campaign customer to the correct person in the portrait atlas.
            // Indices use the atlas order: left-to-right, top-to-bottom.
            int portraitIndex;
            switch (currentOrder.CustomerName.Trim().ToUpperInvariant())
            {
                case "ЭММА": portraitIndex = 0; break;   // young woman
                case "НОЙ": portraitIndex = 1; break;    // young man with glasses
                case "ОЛИВЕР": portraitIndex = 3; break; // man in green
                case "ИТАН": portraitIndex = 5; break;   // man with winter scarf
                case "ЛИЛИ": portraitIndex = 2; break;   // woman with white hair
                case "МИЯ": portraitIndex = 4; break;    // blonde young woman
                case "АВА": portraitIndex = 6; break;    // red-haired woman
                case "ФРЕЯ": portraitIndex = 8; break;   // woman in cream sweater
                case "ТЕО": portraitIndex = 7; break;    // young man in red sweater
                default:
                    // Keep a deterministic fallback for any future customer.
                    portraitIndex = ((Mathf.Max(1, day) - 1) * 3
                        + (Mathf.Max(1, order) - 1)) % customerPortraits.Length;
                    break;
            }

            return customerPortraits[portraitIndex];
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

            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
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
            if (wrappingRoot != null)
            {
                Destroy(wrappingRoot);
                wrappingRoot = null;
            }

            if (root != null)
                Destroy(root);

            root = new GameObject(screenName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image background = ImageAt(
                root.transform,
                "Рождественский фон мастерской",
                Vector2.zero,
                Vector2.one,
                christmasBackground,
                Color.white);

            if (background == null)
                Panel(root.transform, "Фон", Vector2.zero, Vector2.one, cream);
            else
                background.transform.SetAsFirstSibling();

            // A slim wooden frame keeps the art visible while preserving a
            // clear edge around the customer, packing and gift panels.
            Panel(root.transform, "Рамка — верх", new Vector2(0f, 0.986f), new Vector2(1f, 1f), wood);
            Panel(root.transform, "Рамка — низ", new Vector2(0f, 0f), new Vector2(1f, 0.014f), wood);
            Panel(root.transform, "Рамка — слева", new Vector2(0f, 0.014f), new Vector2(0.012f, 0.986f), wood);
            Panel(root.transform, "Рамка — справа", new Vector2(0.988f, 0.014f), new Vector2(1f, 0.986f), wood);

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
            }, new Vector2(0.25f, 0.18f), new Vector2(0.49f, 0.275f));

            ButtonAt("НАСТРОЙКИ", wood, SettingsMenu,
                new Vector2(0.51f, 0.18f), new Vector2(0.75f, 0.275f));

            ButtonAt("ВЫЙТИ", wood, Application.Quit,
                new Vector2(0.41f, 0.085f), new Vector2(0.59f, 0.16f));
        }

        private void SettingsMenu()
        {
            LastGiftAudioManager.EnsureInstance();
            Build("Настройки");

            Vector2 cardMin = new Vector2(0.16f, 0.18f);
            Vector2 cardMax = new Vector2(0.84f, 0.89f);
            Panel(root.transform, "Тень настроек",
                cardMin, cardMax, new Vector2(14, -12), new Vector2(14, -12), wood);
            RectTransform card = Panel(root.transform, "Карточка настроек",
                cardMin, cardMax, Vector2.zero, Vector2.zero, paper);

            CreateText(card, "НАСТРОЙКИ", 42, green,
                new Vector2(0.10f, 0.83f), new Vector2(0.90f, 0.97f),
                TextAnchor.MiddleCenter, Vector2.zero);

            CreateVolumeControl(card, "ОБЩАЯ ГРОМКОСТЬ",
                LastGiftAudioManager.MasterVolume, 0.68f,
                LastGiftAudioManager.SetMasterVolume);
            CreateVolumeControl(card, "МУЗЫКА",
                LastGiftAudioManager.MusicVolume, 0.45f,
                LastGiftAudioManager.SetMusicVolume);
            CreateVolumeControl(card, "ЗВУКОВЫЕ ЭФФЕКТЫ",
                LastGiftAudioManager.SfxVolume, 0.22f,
                LastGiftAudioManager.SetSfxVolume);

            ButtonAt("НАЗАД В МЕНЮ", wood, MainMenu,
                new Vector2(0.38f, 0.085f), new Vector2(0.62f, 0.16f));
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
                new Vector2(0.54f, 0.875f), new Vector2(0.65f, 0.925f), TextAnchor.MiddleCenter);
            Label("РЕП. " + reputation, 18, gold,
                new Vector2(0.66f, 0.875f), new Vector2(0.78f, 0.925f), TextAnchor.MiddleCenter);
            Label("ЗАКАЗ №" + order.ToString("00"), 21, cream,
                new Vector2(0.80f, 0.875f), new Vector2(0.96f, 0.925f), TextAnchor.MiddleRight);

            BuildCustomerPanel();
            BuildPackingArea();
            BuildGiftShelf();

            ButtonAt("ГОЛОВОЛОМКА 4×4", gold, OpenPackingMiniGame,
                new Vector2(0.045f, 0.215f), new Vector2(0.245f, 0.27f));

            feedback = Label("Перетащи подарки с правой полки в коробку по центру.",
                18, brown, new Vector2(0.285f, 0.18f), new Vector2(0.715f, 0.235f), TextAnchor.MiddleCenter);

            ButtonAt("В МЕНЮ", wood, MainMenu,
                new Vector2(0.035f, 0.075f), new Vector2(0.19f, 0.145f));

            ButtonAt("ПРОВЕРИТЬ ЗАКАЗ", green, CheckOrder,
                new Vector2(0.405f, 0.065f), new Vector2(0.665f, 0.155f));

            ButtonAt("ОЧИСТИТЬ КОРОБКУ", red, ClearPackingBox,
                new Vector2(0.70f, 0.075f), new Vector2(0.965f, 0.145f));
        }

        /// <summary>Opens gift wrapping after the existing exact-content validation has passed.</summary>
        public void ShowGiftWrappingScreen()
        {
            GiftWrappingSelection fallback = currentOrder != null && currentOrder.Definition != null
                ? new GiftWrappingSelection(
                    currentOrder.Definition.PreferredWrappingPaper,
                    currentOrder.Definition.PreferredWrappingRibbon,
                    currentOrder.Definition.PreferredWrappingCard)
                : new GiftWrappingSelection(GiftWrappingPaper.Red, GiftWrappingRibbon.BigRed, GiftWrappingCard.MerryChristmas);

            if (root == null || canvas == null || currentOrder == null)
            {
                Debug.LogWarning("Экран оформления недоступен. Заказ будет завершён без блокировки.", this);
                ConfirmWrapping?.Invoke(fallback);
                return;
            }

            // Start from neutral choices for every order; customer preferences are shown as a hint.
            selectedPaper = GiftWrappingPaper.Blue;
            selectedRibbon = GiftWrappingRibbon.ThinBlue;
            selectedCard = GiftWrappingCard.MerryChristmas;
            wrappingSubmitted = false;
            root.SetActive(false);
            CreateWrappingScreen();
        }

        private void CreateWrappingScreen()
        {
            if (wrappingRoot != null)
                Destroy(wrappingRoot);

            wrappingRoot = new GameObject("Экран оформления подарка", typeof(RectTransform));
            wrappingRoot.transform.SetParent(canvas.transform, false);

            RectTransform screenRect = wrappingRoot.GetComponent<RectTransform>();
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.offsetMin = Vector2.zero;
            screenRect.offsetMax = Vector2.zero;

            Panel(wrappingRoot.transform, "Затемнение", Vector2.zero, Vector2.one,
                new Color(0.035f, 0.075f, 0.12f, 0.92f));
            Panel(wrappingRoot.transform, "Панель оформления",
                new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.945f), paper);

            CreateText(wrappingRoot.transform, "УКРАШЕНИЕ ПОДАРКА", 34, green,
                new Vector2(0.08f, 0.855f), new Vector2(0.92f, 0.93f), TextAnchor.MiddleCenter, Vector2.zero);

            string hint = currentOrder.Definition != null
                ? currentOrder.Definition.WrappingHint
                : string.Empty;
            if (string.IsNullOrWhiteSpace(hint))
                hint = "Подбери бумагу, ленту и открытку, чтобы порадовать покупателя.";
            CreateText(wrappingRoot.transform, currentOrder.CustomerName + ": " + hint, 18, brown,
                new Vector2(0.075f, 0.785f), new Vector2(0.625f, 0.85f), TextAnchor.MiddleCenter, Vector2.zero);

            CreateText(wrappingRoot.transform, "УПАКОВОЧНАЯ БУМАГА", 18, green,
                new Vector2(0.07f, 0.705f), new Vector2(0.62f, 0.76f), TextAnchor.MiddleLeft, Vector2.zero);
            GiftWrappingPaper[] papers = {
                GiftWrappingPaper.Red, GiftWrappingPaper.Green, GiftWrappingPaper.Blue,
                GiftWrappingPaper.Gold, GiftWrappingPaper.Blush
            };
            string[] paperNames = { "КРАСНАЯ", "ЗЕЛЁНАЯ", "СИНЯЯ", "ЗОЛОТАЯ", "НЕЖНАЯ" };
            for (int i = 0; i < papers.Length; i++)
            {
                GiftWrappingPaper option = papers[i];
                float left = 0.07f + i * 0.112f;
                CreateWrappingOptionButton(wrappingRoot.transform, paperNames[i], GetWrappingPaperColor(option),
                    new Vector2(left, 0.605f), new Vector2(left + 0.102f, 0.695f),
                    selectedPaper == option, () => { selectedPaper = option; RefreshWrappingScreen(); });
            }

            CreateText(wrappingRoot.transform, "ЛЕНТА И СТИЛЬ", 18, green,
                new Vector2(0.07f, 0.505f), new Vector2(0.62f, 0.56f), TextAnchor.MiddleLeft, Vector2.zero);
            GiftWrappingRibbon[] ribbons = {
                GiftWrappingRibbon.BigRed, GiftWrappingRibbon.BigGold, GiftWrappingRibbon.ClassicGreen,
                GiftWrappingRibbon.ThinBlue, GiftWrappingRibbon.SoftPink
            };
            string[] ribbonNames = { "БОЛЬШАЯ", "БОЛЬШАЯ", "КЛАССИКА", "ТОНКАЯ", "НЕЖНАЯ" };
            for (int i = 0; i < ribbons.Length; i++)
            {
                GiftWrappingRibbon option = ribbons[i];
                float left = 0.07f + i * 0.112f;
                CreateWrappingOptionButton(wrappingRoot.transform, ribbonNames[i], GetWrappingRibbonColor(option),
                    new Vector2(left, 0.405f), new Vector2(left + 0.102f, 0.495f),
                    selectedRibbon == option, () => { selectedRibbon = option; RefreshWrappingScreen(); });
            }

            CreateText(wrappingRoot.transform, "ПРАЗДНИЧНАЯ ОТКРЫТКА", 18, green,
                new Vector2(0.07f, 0.305f), new Vector2(0.62f, 0.36f), TextAnchor.MiddleLeft, Vector2.zero);
            GiftWrappingCard[] cards = {
                GiftWrappingCard.MerryChristmas, GiftWrappingCard.WarmWishes,
                GiftWrappingCard.DreamsComeTrue, GiftWrappingCard.WithLove,
                GiftWrappingCard.HappyHolidays
            };
            string[] cardNames = { "РОЖДЕСТВО", "ТЁПЛЫХ ПРАЗДНИКОВ", "МЕЧТЫ", "С ЛЮБОВЬЮ", "СЧАСТЬЯ" };
            for (int i = 0; i < cards.Length; i++)
            {
                GiftWrappingCard option = cards[i];
                float left = 0.07f + i * 0.112f;
                CreateWrappingOptionButton(wrappingRoot.transform, cardNames[i], new Color32(236, 218, 180, 255),
                    new Vector2(left, 0.205f), new Vector2(left + 0.102f, 0.295f),
                    selectedCard == option, () => { selectedCard = option; RefreshWrappingScreen(); });
            }

            Panel(wrappingRoot.transform, "Подложка предпросмотра",
                new Vector2(0.665f, 0.285f), new Vector2(0.94f, 0.765f), cream);
            CreateText(wrappingRoot.transform, "ТВОЙ ПОДАРОК", 18, green,
                new Vector2(0.68f, 0.70f), new Vector2(0.925f, 0.755f), TextAnchor.MiddleCenter, Vector2.zero);
            DrawWrappingPreview();

            ButtonAtOnParent(wrappingRoot.transform, "НАЗАД", wood, BackToGameplay,
                new Vector2(0.07f, 0.075f), new Vector2(0.24f, 0.155f));
            ButtonAtOnParent(wrappingRoot.transform, "ЗАВЕРШИТЬ УПАКОВКУ", green, FinishGiftWrapping,
                new Vector2(0.66f, 0.075f), new Vector2(0.94f, 0.165f));
        }

        private void RefreshWrappingScreen()
        {
            if (wrappingSubmitted)
                return;
            CreateWrappingScreen();
        }

        private void BackToGameplay()
        {
            if (wrappingSubmitted)
                return;

            if (wrappingRoot != null)
            {
                Destroy(wrappingRoot);
                wrappingRoot = null;
            }

            if (root != null)
                root.SetActive(true);
        }

        private void FinishGiftWrapping()
        {
            if (wrappingSubmitted)
                return;

            wrappingSubmitted = true;
            GiftWrappingSelection selection = new GiftWrappingSelection(selectedPaper, selectedRibbon, selectedCard);
            if (ConfirmWrapping != null)
                ConfirmWrapping.Invoke(selection);
            else
                OrderSuccess(25);
        }

        private void DrawWrappingPreview()
        {
            Color ribbonColor = GetWrappingRibbonColor(selectedRibbon);
            float ribbonWidth = selectedRibbon == GiftWrappingRibbon.BigRed || selectedRibbon == GiftWrappingRibbon.BigGold
                ? 0.09f
                : selectedRibbon == GiftWrappingRibbon.ClassicGreen ? 0.055f : 0.028f;

            Panel(wrappingRoot.transform, "Коробка предпросмотра",
                new Vector2(0.755f, 0.415f), new Vector2(0.87f, 0.675f), GetWrappingPaperColor(selectedPaper));
            Panel(wrappingRoot.transform, "Вертикальная лента",
                new Vector2(0.81f - ribbonWidth * 0.5f, 0.415f),
                new Vector2(0.81f + ribbonWidth * 0.5f, 0.675f), ribbonColor);
            Panel(wrappingRoot.transform, "Горизонтальная лента",
                new Vector2(0.755f, 0.52f - ribbonWidth * 0.5f),
                new Vector2(0.87f, 0.52f + ribbonWidth * 0.5f), ribbonColor);

            RectTransform bowLeft = Panel(wrappingRoot.transform, "Бант слева",
                new Vector2(0.785f, 0.65f), new Vector2(0.81f, 0.715f), ribbonColor);
            bowLeft.localRotation = Quaternion.Euler(0f, 0f, 38f);
            RectTransform bowRight = Panel(wrappingRoot.transform, "Бант справа",
                new Vector2(0.81f, 0.65f), new Vector2(0.835f, 0.715f), ribbonColor);
            bowRight.localRotation = Quaternion.Euler(0f, 0f, -38f);
            Panel(wrappingRoot.transform, "Узел банта",
                new Vector2(0.802f, 0.66f), new Vector2(0.818f, 0.685f), gold);

            Panel(wrappingRoot.transform, "Открытка предпросмотра",
                new Vector2(0.685f, 0.305f), new Vector2(0.92f, 0.385f), new Color32(255, 249, 235, 255));
            CreateText(wrappingRoot.transform, GetWrappingCardText(selectedCard), 15, brown,
                new Vector2(0.695f, 0.31f), new Vector2(0.91f, 0.38f), TextAnchor.MiddleCenter, Vector2.zero);
        }

        private void CreateWrappingOptionButton(Transform parent, string caption, Color color,
            Vector2 min, Vector2 max, bool selected, Action onClick)
        {
            GameObject buttonObject = new GameObject("Выбор " + caption,
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

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
            button.onClick.AddListener(() => onClick?.Invoke());

            if (selected)
            {
                Outline outline = buttonObject.AddComponent<Outline>();
                outline.effectColor = gold;
                outline.effectDistance = new Vector2(4f, 4f);
            }

            Color labelColor = color.grayscale > 0.72f ? brown : paper;
            Text label = ChildLabel(buttonObject.transform, caption, 13, labelColor,
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), TextAnchor.MiddleCenter);
            label.raycastTarget = false;
        }

        private void ButtonAtOnParent(Transform parent, string caption, Color color, Action action,
            Vector2 min, Vector2 max)
        {
            GameObject buttonObject = new GameObject("Кнопка " + caption,
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

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

        private Color GetWrappingPaperColor(GiftWrappingPaper option)
        {
            switch (option)
            {
                case GiftWrappingPaper.Red: return red;
                case GiftWrappingPaper.Green: return green;
                case GiftWrappingPaper.Blue: return new Color32(55, 112, 166, 255);
                case GiftWrappingPaper.Gold: return gold;
                default: return new Color32(235, 178, 191, 255);
            }
        }

        private Color GetWrappingRibbonColor(GiftWrappingRibbon option)
        {
            switch (option)
            {
                case GiftWrappingRibbon.BigRed: return red;
                case GiftWrappingRibbon.BigGold: return gold;
                case GiftWrappingRibbon.ClassicGreen: return green;
                case GiftWrappingRibbon.ThinBlue: return new Color32(55, 112, 166, 255);
                default: return new Color32(218, 132, 155, 255);
            }
        }

        private string GetWrappingCardText(GiftWrappingCard option)
        {
            switch (option)
            {
                case GiftWrappingCard.WarmWishes: return "Тёплых и уютных праздников!";
                case GiftWrappingCard.DreamsComeTrue: return "Пусть мечты сбываются!";
                case GiftWrappingCard.WithLove: return "С теплом и любовью!";
                case GiftWrappingCard.HappyHolidays: return "Радости и чудес!";
                default: return "Счастливого Рождества!";
            }
        }

        private void OpenPackingMiniGame()
        {
            if (packingMiniGameOpen || canvas == null)
                return;

            packingMiniGameOpen = true;
            canvas.enabled = false;
            if (shopCamera != null)
                shopCamera.enabled = false;

            StartCoroutine(LoadPackingMiniGame());
        }

        private IEnumerator LoadPackingMiniGame()
        {
            AsyncOperation loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(
                    PackingMiniGameSceneName, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                Debug.LogError("Не удалось открыть головоломку: " + exception.Message, this);
                RestoreShopAfterMiniGame();
                yield break;
            }

            if (loadOperation == null)
            {
                Debug.LogError("Сцена головоломки не найдена в Build Settings: " + PackingMiniGameSceneName, this);
                RestoreShopAfterMiniGame();
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            Scene miniGameScene = SceneManager.GetSceneByName(PackingMiniGameSceneName);
            if (!miniGameScene.IsValid() || !miniGameScene.isLoaded)
            {
                Debug.LogError("Сцена головоломки не загрузилась: " + PackingMiniGameSceneName, this);
                RestoreShopAfterMiniGame();
                yield break;
            }

            SceneManager.SetActiveScene(miniGameScene);
        }

        public static void ReturnFromPackingMiniGame()
        {
            if (activeInstance == null || !activeInstance.packingMiniGameOpen)
                return;

            activeInstance.StartCoroutine(activeInstance.UnloadPackingMiniGame());
        }

        private IEnumerator UnloadPackingMiniGame()
        {
            Scene miniGameScene = SceneManager.GetSceneByName(PackingMiniGameSceneName);
            if (miniGameScene.IsValid() && miniGameScene.isLoaded)
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(miniGameScene);
                if (unloadOperation != null)
                {
                    while (!unloadOperation.isDone)
                        yield return null;
                }
            }

            RestoreShopAfterMiniGame();
        }

        private void RestoreShopAfterMiniGame()
        {
            Scene shopScene = gameObject.scene;
            if (shopScene.IsValid() && shopScene.isLoaded)
                SceneManager.SetActiveScene(shopScene);

            if (shopCamera != null)
                shopCamera.enabled = true;
            if (canvas != null)
                canvas.enabled = true;

            packingMiniGameOpen = false;
        }

        private void BuildCustomerPanel()
        {
            Panel(root.transform, "Карточка покупателя",
                new Vector2(0.025f, 0.205f), new Vector2(0.265f, 0.855f), paper);

            Label("ВАШ ПОКУПАТЕЛЬ", 18, green,
                new Vector2(0.04f, 0.795f), new Vector2(0.25f, 0.845f), TextAnchor.MiddleCenter);

            Panel(root.transform, "Портрет покупателя",
                new Vector2(0.055f, 0.66f), new Vector2(0.125f, 0.775f), gold);
            Sprite customerPortrait = GetCurrentCustomerPortrait();
            if (customerPortrait != null)
            {
                ImageAt(root.transform, "Фото покупателя",
                    new Vector2(0.059f, 0.664f), new Vector2(0.121f, 0.771f),
                    customerPortrait, Color.white);
            }
            else
            {
                Label("☺", 66, red,
                    new Vector2(0.055f, 0.66f), new Vector2(0.125f, 0.775f), TextAnchor.MiddleCenter);
            }
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
            Label("КОРОБКА ДЛЯ УПАКОВКИ — " + GetBoxSizeLabel(currentOrder.BoxSize), 21, green,
                new Vector2(0.29f, 0.785f), new Vector2(0.71f, 0.84f), TextAnchor.MiddleCenter);
            Label("Перетаскивай сюда подарки с полки справа", 16, brown,
                new Vector2(0.29f, 0.745f), new Vector2(0.71f, 0.785f), TextAnchor.MiddleCenter);

            Panel(root.transform, "Стол упаковки",
                new Vector2(0.285f, 0.265f), new Vector2(0.715f, 0.725f), new Color32(216, 190, 156, 255));

            packingRect = Panel(root.transform, "Коробка",
                new Vector2(0.315f, 0.295f), new Vector2(0.685f, 0.695f), paper);

            int columns = GetBoxColumns(currentOrder.BoxSize);
            int rows = GetBoxRows(currentOrder.BoxSize);
            for (int i = 1; i < columns; i++)
            {
                float p = i / (float)columns;
                Panel(packingRect, "Вертикальная линия",
                    new Vector2(p - 0.0015f, 0f), new Vector2(p + 0.0015f, 1f), new Color32(225, 209, 181, 255));
            }
            for (int i = 1; i < rows; i++)
            {
                float p = i / (float)rows;
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

            currentOrder = new CustomerOrderData(orderData, requiredGifts.ToArray());

            day = currentDay;
            order = currentOrderNumber;
            coins = currentBalance;
        }

        public void SetProgressionBalance(int currentBalance, int currentReputation = 0)
        {
            coins = currentBalance;
            reputation = currentReputation;
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

        private int GetBoxColumns(GiftBoxSize size)
        {
            return size == GiftBoxSize.Small ? 2 : 3;
        }

        private int GetBoxRows(GiftBoxSize size)
        {
            switch (size)
            {
                case GiftBoxSize.Small: return 2;
                case GiftBoxSize.Medium: return 2;
                default: return 3;
            }
        }

        private string GetBoxSizeLabel(GiftBoxSize size)
        {
            switch (size)
            {
                case GiftBoxSize.Small: return "МАЛЕНЬКАЯ";
                case GiftBoxSize.Medium: return "СРЕДНЯЯ";
                default: return "БОЛЬШАЯ";
            }
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

            int columns = GetBoxColumns(currentOrder.BoxSize);
            int rows = GetBoxRows(currentOrder.BoxSize);
            int capacity = columns * rows;
            if (placedGiftCount >= capacity)
            {
                SetStatus("Коробка заполнена. Очисти её или проверь заказ.", false);
                return;
            }

            int column = placedGiftCount % columns;
            int row = placedGiftCount / columns;

            GameObject placed = new GameObject("Упакованный подарок " + giftName, typeof(RectTransform), typeof(Image));
            placed.transform.SetParent(packingRect, false);

            RectTransform rect = placed.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(column / (float)columns, row / (float)rows);
            rect.anchorMax = new Vector2((column + 1) / (float)columns, (row + 1) / (float)rows);
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
            OrderSuccess(earned, 0, 0);
        }

        public void OrderSuccess(int earned, int wrappingBonus, int reputationBonus)
        {
            coins += earned;
            order++;
            currentOrder = null;
            Build("Заказ выполнен");
            string resultMessage = "Покупатель доволен!\nТы заработала монеты и помогла сделать чей-то праздник теплее.";
            if (wrappingBonus > 0)
                resultMessage += "\nБонус за удачное оформление: +" + wrappingBonus + " монет.";
            if (reputationBonus > 0)
                resultMessage += "\nИдеальное сочетание! Репутация +" + reputationBonus + ".";
            RectTransform successCard = Card("Заказ выполнен!", resultMessage);
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
                ? "Спасибо, что наполнила лавку теплом и заботой!\nПусть в каждом подарке будет немного рождественского чуда."
                : "Ты завершила все три дня!\nВыполнено заказов: " + summary.OrdersCompleted
                    + "\nЗаработано монет: " + summary.TotalCoinsEarned
                    + "\nИтоговый баланс: " + summary.FinalCoins
                    + "\nРепутация: " + summary.ReputationEarned
                    + "\n\nСпасибо, что наполнила лавку рождественским чудом!";
            Card("Последний подарок", body);
            Button("ИГРАТЬ СНОВА", green, () =>
            {
                day = 1;
                coins = 0;
                reputation = 0;
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

        private void CreateVolumeControl(
            Transform parent,
            string caption,
            float initialValue,
            float top,
            Action<float> onValueChanged)
        {
            ChildLabel(parent, caption, 23, brown,
                new Vector2(0.16f, top),
                new Vector2(0.70f, top + 0.09f),
                TextAnchor.MiddleLeft);

            Text valueLabel = ChildLabel(parent,
                Mathf.RoundToInt(initialValue * 100f) + "%", 21, brown,
                new Vector2(0.72f, top),
                new Vector2(0.84f, top + 0.09f),
                TextAnchor.MiddleRight);

            Slider slider = CreateVolumeSlider(parent, caption, top);
            slider.SetValueWithoutNotify(Mathf.Clamp01(initialValue));
            slider.onValueChanged.AddListener(value =>
            {
                valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
                onValueChanged?.Invoke(value);
            });
        }

        private Slider CreateVolumeSlider(Transform parent, string sliderName, float top)
        {
            GameObject sliderObject = new GameObject(
                "Ползунок " + sliderName,
                typeof(RectTransform),
                typeof(Slider));
            sliderObject.transform.SetParent(parent, false);

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.16f, top - 0.09f);
            sliderRect.anchorMax = new Vector2(0.84f, top - 0.02f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            GameObject backgroundObject = new GameObject(
                "Дорожка", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color32(117, 86, 62, 255);
            backgroundImage.raycastTarget = true;

            GameObject fillAreaObject = new GameObject("Область заполнения", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(12f, 0f);
            fillAreaRect.offsetMax = new Vector2(-12f, 0f);

            GameObject fillObject = new GameObject("Заполнение", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = gold;
            fillImage.raycastTarget = false;

            GameObject handleAreaObject = new GameObject("Область бегунка", typeof(RectTransform));
            handleAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(12f, 0f);
            handleAreaRect.offsetMax = new Vector2(-12f, 0f);

            GameObject handleObject = new GameObject("Бегунок", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(handleAreaObject.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(26f, 26f);

            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = paper;
            handleImage.raycastTarget = true;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            return slider;
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