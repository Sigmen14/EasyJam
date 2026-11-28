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
        public Action ConfirmPacking, Continue, StartGame, OpenWorkshop;
        public Func<WorkshopUpgradeType, bool> PurchaseWorkshopUpgrade;
        public Action<GiftWrappingSelection> ConfirmWrapping;
        public Action SecretWishShown;
        public Func<bool, int, int> AwardSecretWishBonus;

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
        private bool secretWishAnswered;
        private bool introTransitionPending;
        private bool introCompleted;
        private bool workshopGarland;
        private bool workshopExtraShelf;
        private bool workshopPremiumWrapping;
        private Text workshopStatus;
        private Action workshopReturnAction;
        private int lastResultEarned;
        private int lastResultWrappingBonus;
        private int lastResultReputationBonus;
        private bool lastResultCanOpenWorkshop;
        private Sprite scarfSprite;
        private Texture2D scarfTexture;

        private static readonly string[] SortingEventGiftNames =
        {
            "МИШКА", "ПОДАРОК", "ЛЕДЕНЕЦ", "СНЕЖНЫЙ ШАР"
        };

        private static readonly string[] SortingEventTargetNames =
        {
            "ОБЪЯТИЯ", "ПОД ЁЛКУ", "СЛАДОСТИ", "ЗИМНЕЕ ЧУДО"
        };

        private readonly RectTransform[] sortingEventTargets = new RectTransform[4];
        private readonly GameObject[] sortingEventCards = new GameObject[4];
        private readonly bool[] sortingEventPlaced = new bool[4];
        private Text sortingEventStatus;
        private Text sortingEventTimer;
        private Coroutine sortingEventTimerRoutine;
        private Action sortingEventCompleted;
        private int sortingEventPlacedCount;
        private int sortingEventSecondsRemaining;
        private bool sortingEventSolved;

        private sealed class CustomerOrderData
        {
            public readonly string CustomerName;
            public readonly string Wish;
            public readonly string[] RequiredGifts;
            public readonly GiftBoxSize BoxSize;
            public readonly GiftOrderDefinition Definition;
            public readonly bool SecretWishAssigned;
            public readonly bool HasValidSecretWishData;

            public CustomerOrderData(GiftOrderDefinition definition, bool secretWishAssigned, params string[] requiredGifts)
            {
                Definition = definition;
                CustomerName = definition.CustomerName;
                Wish = definition.CustomerWish;
                BoxSize = definition.BoxSize;
                RequiredGifts = requiredGifts;

                SecretWishAssigned = secretWishAssigned;
                SecretCustomerWishDefinition secretWish = definition.SecretWish;
                HasValidSecretWishData = secretWish != null
                    && !string.IsNullOrWhiteSpace(secretWish.Clue)
                    && secretWish.GiftOptions != null
                    && secretWish.GiftOptions.Count >= 2
                    && secretWish.GiftOptions.Count <= 3
                    && !string.IsNullOrWhiteSpace(secretWish.CorrectGiftName);
            }
        }

        private static readonly string[] AllGiftNames =
        {
            "МИШКА", "ПОДАРОК", "ЛЕДЕНЕЦ", "СНЕЖНЫЙ ШАР", "ШАРФ"
        };

        /// <summary>
        /// Shared by Gameplay and the editor smoke test: saved wish data alone never
        /// requests the question screen; this decision requires this run's assignment.
        /// </summary>
        public static bool DispatchGameplayScreen(
            bool secretWishAssigned,
            bool hasValidSecretWishData,
            bool secretWishAnswered,
            Action showSecretWishScreen,
            Action showRegularGameplayScreen)
        {
            bool shouldShowSecretWish = secretWishAssigned
                && hasValidSecretWishData
                && !secretWishAnswered;

            if (shouldShowSecretWish)
                showSecretWishScreen?.Invoke();
            else
                showRegularGameplayScreen?.Invoke();

            return shouldShowSecretWish;
        }

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
            scarfSprite = CreateScarfSprite();
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
            if (sortingEventTimerRoutine != null)
                StopCoroutine(sortingEventTimerRoutine);

            if (christmasBackground != null)
                Destroy(christmasBackground);
            if (christmasBackgroundTexture != null)
                Destroy(christmasBackgroundTexture);
            if (scarfSprite != null)
                Destroy(scarfSprite);
            if (scarfTexture != null)
                Destroy(scarfTexture);

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

        private Sprite CreateScarfSprite()
        {
            const int size = 64;
            scarfTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            scarfTexture.name = "Шарф — процедурная иконка";
            scarfTexture.filterMode = FilterMode.Bilinear;
            scarfTexture.wrapMode = TextureWrapMode.Clamp;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 fabric = new Color32(158, 48, 52, 255);
            Color32 goldStripe = new Color32(232, 184, 96, 255);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            FillScarfRect(pixels, size, 13, 39, 51, 53, fabric);
            FillScarfRect(pixels, size, 17, 8, 29, 47, fabric);
            FillScarfRect(pixels, size, 35, 7, 47, 42, fabric);
            for (int y = 13; y < 53; y += 10)
            {
                FillScarfRect(pixels, size, 17, y, 29, y + 2, goldStripe);
                if (y < 40) FillScarfRect(pixels, size, 35, y, 47, y + 2, goldStripe);
            }
            for (int x = 18; x < 29; x += 3) FillScarfRect(pixels, size, x, 4, x + 1, 8, goldStripe);
            for (int x = 36; x < 47; x += 3) FillScarfRect(pixels, size, x, 3, x + 1, 7, goldStripe);
            scarfTexture.SetPixels32(pixels);
            scarfTexture.Apply(false, false);
            return Sprite.Create(scarfTexture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 96f);
        }

        private static void FillScarfRect(Color32[] pixels, int textureSize,
            int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            for (int y = Mathf.Max(0, yMin); y < Mathf.Min(textureSize, yMax); y++)
                for (int x = Mathf.Max(0, xMin); x < Mathf.Min(textureSize, xMax); x++)
                    pixels[y * textureSize + x] = color;
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
                case "МИША": portraitIndex = 7; break;   // use the closest existing youthful male portrait
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
                "Добро пожаловать в маленькую рождественскую лавку!\nСобирай подарки по пожеланиям покупателей.\nЗа выполненные заказы получай монеты.\nВыбирай подарки и перетаскивай их в коробку мышью.",
                true);

            ButtonAt("НАЧАТЬ ИГРУ", green, BeginGameFromMenu,
                new Vector2(0.25f, 0.18f), new Vector2(0.49f, 0.275f));

            ButtonAt("НАСТРОЙКИ", wood, SettingsMenu,
                new Vector2(0.51f, 0.18f), new Vector2(0.75f, 0.275f));

            ButtonAt("ВЫЙТИ", wood, Application.Quit,
                new Vector2(0.41f, 0.085f), new Vector2(0.59f, 0.16f));
        }

        private void BeginGameFromMenu()
        {
            if (introTransitionPending)
                return;

            // Once completed or skipped, returning to the menu must not replay the intro.
            if (introCompleted)
            {
                ContinueStartGameFlow();
                return;
            }

            introTransitionPending = true;
            bool started = LastGiftIntroController.TryPlay(() =>
            {
                introTransitionPending = false;
                introCompleted = true;
                ContinueStartGameFlow();
            });

            if (!started)
                introTransitionPending = false;
        }

        private void ContinueStartGameFlow()
        {
            if (!tutorialShown)
            {
                ShowTutorial();
                return;
            }

            if (StartGame != null) StartGame.Invoke();
            else Gameplay();
        }

        private void SettingsMenu()
        {
            LastGiftAudioManager.EnsureInstance();
            Build("Настройки");

            Vector2 cardMin = new Vector2(0.16f, 0.18f);
            Vector2 cardMax = new Vector2(0.84f, 0.89f);
            RectTransform card = FestivePanel(root.transform, "Праздничная панель настроек",
                cardMin, cardMax, paper);

            FestiveHeader(card, "Заголовок настроек", "НАСТРОЙКИ",
                new Vector2(0.08f, 0.835f), new Vector2(0.92f, 0.975f), red, 34);

            FestivePanel(card, "Карточка общей громкости",
                new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.80f), cream);
            FestivePanel(card, "Карточка музыки",
                new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.57f), cream);
            FestivePanel(card, "Карточка звуковых эффектов",
                new Vector2(0.08f, 0.09f), new Vector2(0.92f, 0.34f), cream);

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
                if (StartGame != null) StartGame.Invoke();
                else Gameplay();
            }, new Vector2(0.27f, 0.085f), new Vector2(0.73f, 0.19f));
        }

        public void Gameplay()
        {
            if (currentOrder == null)
            {
                SetStatus("Не удалось загрузить заказ. Вернись в меню и начни игру заново.", false);
                return;
            }

            bool showSecretWish = DispatchGameplayScreen(
                currentOrder.SecretWishAssigned,
                currentOrder.HasValidSecretWishData,
                secretWishAnswered,
                ShowSecretWish,
                BuildGameplayScreen);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[SecretWishFlow] Gameplay decision for {currentOrder.CustomerName}: " +
                $"assigned={currentOrder.SecretWishAssigned}, answered={secretWishAnswered}, " +
                $"showSecretWish={showSecretWish}.",
                this);
#endif
        }

        private void BuildGameplayScreen()
        {
            Build("Игровой экран");
            placedGiftCount = 0;
            placedGiftNames.Clear();

            FestivePanel(root.transform, "Верхняя праздничная панель",
                new Vector2(0.02f, 0.875f), new Vector2(0.98f, 0.925f), wood);
            Label(workshopGarland ? "✦ ПОСЛЕДНИЙ ПОДАРОК ✦" : "ПОСЛЕДНИЙ ПОДАРОК ✦", 25, cream,
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

            FestivePanel(root.transform, "Подложка подсказки игроку",
                new Vector2(0.275f, 0.172f), new Vector2(0.725f, 0.242f), cream);
            feedback = Label("Перетащи подарки с правой полки в коробку по центру.",
                18, brown, new Vector2(0.285f, 0.18f), new Vector2(0.715f, 0.235f), TextAnchor.MiddleCenter);

            ButtonAt("В МЕНЮ", wood, MainMenu,
                new Vector2(0.035f, 0.075f), new Vector2(0.19f, 0.145f));

            ButtonAt("ПРОВЕРИТЬ ЗАКАЗ", green, CheckOrder,
                new Vector2(0.405f, 0.065f), new Vector2(0.665f, 0.155f));

            ButtonAt("ОЧИСТИТЬ КОРОБКУ", red, ClearPackingBox,
                new Vector2(0.70f, 0.075f), new Vector2(0.965f, 0.145f));
        }

        private void ShowSecretWish()
        {
            if (currentOrder == null)
                return;

            if (!currentOrder.SecretWishAssigned || !currentOrder.HasValidSecretWishData)
            {
                Debug.LogError(
                    $"[SecretWishFlow] Refusing to show an unassigned or invalid wish for {currentOrder.CustomerName}.",
                    this);
                secretWishAnswered = true;
                BuildGameplayScreen();
                return;
            }

            SecretCustomerWishDefinition wish = currentOrder.Definition.SecretWish;
            Debug.Log($"[SecretWishFlow] ShowSecretWish invoked for {currentOrder.CustomerName}.", this);
            Build("Тайное желание покупателя");
            FestivePanel(root.transform, "Праздничная карточка тайного желания",
                new Vector2(0.10f, 0.14f), new Vector2(0.90f, 0.90f), paper);

            FestiveHeader(root.transform, "Лента тайного желания", "ТАЙНОЕ ЖЕЛАНИЕ ПОКУПАТЕЛЯ",
                new Vector2(0.14f, 0.785f), new Vector2(0.86f, 0.895f), red, 24);
            Panel(root.transform, "Рамка портрета",
                new Vector2(0.17f, 0.55f), new Vector2(0.30f, 0.74f), gold);

            Sprite portrait = GetCurrentCustomerPortrait();
            if (portrait != null)
                ImageAt(root.transform, "Портрет покупателя",
                    new Vector2(0.175f, 0.555f), new Vector2(0.295f, 0.735f), portrait, Color.white);
            else
                Label("☺", 64, red,
                    new Vector2(0.17f, 0.55f), new Vector2(0.30f, 0.74f), TextAnchor.MiddleCenter);

            Label(currentOrder.CustomerName, 19, brown,
                new Vector2(0.15f, 0.49f), new Vector2(0.32f, 0.55f), TextAnchor.MiddleCenter);
            FestivePanel(root.transform, "Праздничная карточка реплики покупателя",
                new Vector2(0.34f, 0.55f), new Vector2(0.83f, 0.74f), cream);
            Label("«" + wish.Clue + "»", 24, brown,
                new Vector2(0.36f, 0.56f), new Vector2(0.81f, 0.73f), TextAnchor.MiddleCenter);
            Label("Какой подарок подойдёт этому человеку?", 23, green,
                new Vector2(0.18f, 0.41f), new Vector2(0.82f, 0.49f), TextAnchor.MiddleCenter);

            float totalWidth = 0.72f;
            float gap = 0.025f;
            float optionWidth = (totalWidth - gap * (wish.GiftOptions.Count - 1)) / wish.GiftOptions.Count;
            float startX = (1f - totalWidth) * 0.5f;
            for (int i = 0; i < wish.GiftOptions.Count; i++)
            {
                float x = startX + i * (optionWidth + gap);
                AddSecretWishOption(wish.GiftOptions[i],
                    new Vector2(x, 0.22f), new Vector2(x + optionWidth, 0.40f));
            }

            Label("За правильный ответ: +" + wish.BonusCoins + " монет",
                17, wood, new Vector2(0.20f, 0.14f), new Vector2(0.80f, 0.21f), TextAnchor.MiddleCenter);

            // The progression manager records that this exact order's assigned clue was displayed.
            SecretWishShown?.Invoke();
        }

        private void AddSecretWishOption(string giftName, Vector2 min, Vector2 max)
        {
            string displayName = GetGiftDisplayName(giftName);
            GameObject optionObject = new GameObject("Вариант подарка " + displayName,
                typeof(RectTransform), typeof(Image), typeof(Button));
            optionObject.transform.SetParent(root.transform, false);

            RectTransform rect = optionObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = optionObject.GetComponent<Image>();
            background.color = paleBlue;
            background.raycastTarget = true;
            AddGoldOutline(optionObject, 2.5f);

            Button button = optionObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = paleBlue;
            colors.highlightedColor = Color.Lerp(paleBlue, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(paleBlue, gold, 0.25f);
            colors.selectedColor = Color.Lerp(paleBlue, gold, 0.15f);
            button.colors = colors;
            button.onClick.AddListener(() => SelectSecretWish(giftName));

            Image icon = ImageAt(optionObject.transform, "Иконка варианта",
                new Vector2(0.16f, 0.29f), new Vector2(0.84f, 0.92f),
                GetGiftSprite(giftName), Color.white);
            ChildLabel(optionObject.transform, displayName, 16, brown,
                new Vector2(0.04f, icon != null ? 0.04f : 0.04f),
                new Vector2(0.96f, icon != null ? 0.30f : 0.96f), TextAnchor.MiddleCenter);
        }

        private void SelectSecretWish(string selectedGift)
        {
            if (currentOrder == null || !currentOrder.SecretWishAssigned
                || !currentOrder.HasValidSecretWishData || secretWishAnswered)
                return;

            secretWishAnswered = true;
            SecretCustomerWishDefinition wish = currentOrder.Definition.SecretWish;
            bool correct = string.Equals(selectedGift, wish.CorrectGiftName,
                StringComparison.OrdinalIgnoreCase);

            int awarded = AwardSecretWishBonus != null
                ? AwardSecretWishBonus(correct, wish.BonusCoins)
                : 0;
            ShowSecretWishResult(correct, awarded);
        }

        private void ShowSecretWishResult(bool correct, int awarded)
        {
            Build(correct ? "Угаданное желание" : "Ответ на тайное желание");
            string body = correct
                ? "Верно! Покупатель будет очень рад такому подарку."
                : "Похоже, этот подарок не совсем подходит. Ничего страшного — заказ остаётся в силе, и ты всё равно можешь собрать коробку.";

            if (correct && awarded > 0)
                body += "\n\n+" + awarded + " МОНЕТ ✦";

            Card(correct ? "Отличный выбор!" : "Попробуем другой подарок в следующий раз", body);
            ButtonAt("ПЕРЕЙТИ К СБОРКЕ КОРОБКИ", green, Gameplay,
                new Vector2(0.27f, 0.085f), new Vector2(0.73f, 0.19f));
        }

        /// <summary>Opens gift wrapping after the existing exact-content validation has passed.</summary>
        public void ShowGiftWrappingScreen()
        {
            GiftWrappingSelection fallback = currentOrder != null && currentOrder.Definition != null
                ? new GiftWrappingSelection(
                    currentOrder.Definition.PreferredWrappingPaper,
                    currentOrder.Definition.PreferredWrappingRibbon,
                    currentOrder.Definition.PreferredWrappingCard)
                : new GiftWrappingSelection(GiftWrappingPaper.Blue, GiftWrappingRibbon.ThinBlue, GiftWrappingCard.WarmWishes);

            if (root == null || canvas == null || currentOrder == null)
            {
                Debug.LogWarning("Экран оформления недоступен. Заказ будет завершён без блокировки.", this);
                ConfirmWrapping?.Invoke(fallback);
                return;
            }

            // Start from neutral choices for every order; customer preferences are shown as a hint.
            selectedPaper = workshopPremiumWrapping ? GiftWrappingPaper.Gold : GiftWrappingPaper.Blue;
            selectedRibbon = workshopPremiumWrapping ? GiftWrappingRibbon.BigGold : GiftWrappingRibbon.ThinBlue;
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
            FestivePanel(wrappingRoot.transform, "Большая праздничная панель оформления",
                new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.945f), paper);

            FestiveHeader(wrappingRoot.transform, "Лента оформления подарка", "УКРАШЕНИЕ ПОДАРКА",
                new Vector2(0.08f, 0.855f), new Vector2(0.92f, 0.935f), red, 30);

            string hint = currentOrder.Definition != null
                ? currentOrder.Definition.WrappingHint
                : string.Empty;
            if (string.IsNullOrWhiteSpace(hint))
                hint = "Подбери бумагу, ленту и открытку, чтобы порадовать покупателя.";
            FestivePanel(wrappingRoot.transform, "Карточка пожелания покупателя",
                new Vector2(0.065f, 0.778f), new Vector2(0.635f, 0.852f), cream);
            CreateText(wrappingRoot.transform, currentOrder.CustomerName + ": " + hint, 18, brown,
                new Vector2(0.075f, 0.785f), new Vector2(0.625f, 0.85f), TextAnchor.MiddleCenter, Vector2.zero);

            FestiveHeader(wrappingRoot.transform, "Заголовок выбора бумаги", "УПАКОВОЧНАЯ БУМАГА",
                new Vector2(0.07f, 0.705f), new Vector2(0.62f, 0.76f), green, 15, false);
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

            FestiveHeader(wrappingRoot.transform, "Заголовок выбора ленты", "ЛЕНТА И СТИЛЬ",
                new Vector2(0.07f, 0.505f), new Vector2(0.62f, 0.56f), red, 15, false);
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

            FestiveHeader(wrappingRoot.transform, "Заголовок выбора открытки", "ПРАЗДНИЧНАЯ ОТКРЫТКА",
                new Vector2(0.07f, 0.305f), new Vector2(0.62f, 0.36f), green, 15, false);
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

            FestivePanel(wrappingRoot.transform, "Праздничная рамка предпросмотра",
                new Vector2(0.665f, 0.285f), new Vector2(0.94f, 0.765f), cream);
            FestiveHeader(wrappingRoot.transform, "Заголовок предпросмотра", "ТВОЙ ПОДАРОК",
                new Vector2(0.68f, 0.70f), new Vector2(0.925f, 0.755f), green, 15, false);
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
            // Keep the escape route available even if completion could not be processed.
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

            FestivePanel(wrappingRoot.transform, "Рамка открытки предпросмотра",
                new Vector2(0.685f, 0.305f), new Vector2(0.92f, 0.385f), paper);
            CreateText(wrappingRoot.transform, GetWrappingCardText(selectedCard), 15, brown,
                new Vector2(0.700f, 0.318f), new Vector2(0.905f, 0.372f), TextAnchor.MiddleCenter, Vector2.zero);
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

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = gold;
            outline.effectDistance = selected ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
            outline.useGraphicAlpha = false;

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
            AddGoldOutline(buttonObject, 2f);

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
            FestivePanel(root.transform, "Карточка покупателя",
                new Vector2(0.025f, 0.205f), new Vector2(0.265f, 0.855f), paper);

            Panel(root.transform, "Золотая основа заголовка покупателя",
                new Vector2(0.038f, 0.792f), new Vector2(0.252f, 0.848f), gold);
            Panel(root.transform, "Бордовая лента заголовка покупателя",
                new Vector2(0.042f, 0.800f), new Vector2(0.248f, 0.840f), red);

            Label("ВАШ ПОКУПАТЕЛЬ", 18, paper,
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

            FestivePanel(root.transform, "Пожелание покупателя",
                new Vector2(0.045f, 0.47f), new Vector2(0.245f, 0.65f), cream);
            Label(currentOrder.Wish, 17, brown,
                new Vector2(0.05f, 0.475f), new Vector2(0.24f, 0.645f), TextAnchor.MiddleCenter);

            FestiveHeader(root.transform, "Метка состава заказа", "В ЗАКАЗЕ НУЖНО",
                new Vector2(0.045f, 0.425f), new Vector2(0.245f, 0.465f), green, 13, false);

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
            FestivePanel(root.transform, "Стол упаковки",
                new Vector2(0.285f, 0.265f), new Vector2(0.715f, 0.725f),
                new Color32(216, 190, 156, 255));

            Panel(root.transform, "Золотая основа заголовка коробки",
                new Vector2(0.298f, 0.782f), new Vector2(0.702f, 0.842f), gold);
            Panel(root.transform, "Зелёная лента заголовка коробки",
                new Vector2(0.304f, 0.790f), new Vector2(0.696f, 0.834f), green);
            Label("КОРОБКА ДЛЯ УПАКОВКИ — " + GetBoxSizeLabel(currentOrder.BoxSize), 21, paper,
                new Vector2(0.29f, 0.785f), new Vector2(0.71f, 0.84f), TextAnchor.MiddleCenter);
            FestivePanel(root.transform, "Подсказка над коробкой",
                new Vector2(0.29f, 0.745f), new Vector2(0.71f, 0.785f), cream);
            Label("Перетаскивай сюда подарки с полки справа", 16, brown,
                new Vector2(0.30f, 0.748f), new Vector2(0.70f, 0.782f), TextAnchor.MiddleCenter);

            FestivePanel(root.transform, "Коробка — декоративная рамка",
                new Vector2(0.300f, 0.280f), new Vector2(0.700f, 0.710f), paper);
            packingRect = Panel(root.transform, "Коробка",
                new Vector2(0.315f, 0.295f), new Vector2(0.685f, 0.695f), cream);

            int columns = GetBoxColumns(currentOrder.BoxSize);
            int rows = GetBoxRows(currentOrder.BoxSize);
            for (int i = 1; i < columns; i++)
            {
                float p = i / (float)columns;
                Panel(packingRect, "Вертикальная линия",
                    new Vector2(p - 0.0015f, 0f), new Vector2(p + 0.0015f, 1f), new Color32(204, 174, 123, 255));
            }
            for (int i = 1; i < rows; i++)
            {
                float p = i / (float)rows;
                Panel(packingRect, "Горизонтальная линия",
                    new Vector2(0f, p - 0.002f), new Vector2(1f, p + 0.002f), new Color32(204, 174, 123, 255));
            }

            packingHint = Label("ПЕРЕТАЩИ СЮДА ПОДАРОК", 19, wood,
                new Vector2(0.32f, 0.43f), new Vector2(0.68f, 0.56f), TextAnchor.MiddleCenter);
        }

        private void BuildGiftShelf()
        {
            FestivePanel(root.transform, "Полка с подарками",
                new Vector2(0.735f, 0.205f), new Vector2(0.975f, 0.855f), paper);

            Panel(root.transform, "Золотая основа заголовка полки",
                new Vector2(0.746f, 0.792f), new Vector2(0.964f, 0.848f), gold);
            Panel(root.transform, "Зелёная лента заголовка полки",
                new Vector2(0.750f, 0.800f), new Vector2(0.960f, 0.840f), green);

            Label(workshopExtraShelf ? "ПОДАРКИ И НОВАЯ ПОЛКА ✦" : "ПОДАРКИ НА ПОЛКЕ", 18, paper,
                new Vector2(0.75f, 0.795f), new Vector2(0.96f, 0.845f), TextAnchor.MiddleCenter);
            Label("Зажми предмет и перетащи его", 14, brown,
                new Vector2(0.75f, 0.755f), new Vector2(0.96f, 0.795f), TextAnchor.MiddleCenter);

            if (currentOrder.Definition != null && currentOrder.Definition.IsStoryFinaleOrder)
            {
                AddShelfGift("ШАРФ", scarfSprite, new Vector2(0.795f, 0.425f), new Vector2(0.915f, 0.695f), red);
            }
            else
            {
                AddShelfGift("МИШКА", teddySprite, new Vector2(0.755f, 0.535f), new Vector2(0.855f, 0.735f), wood);
                AddShelfGift("ПОДАРОК", giftBoxSprite, new Vector2(0.865f, 0.535f), new Vector2(0.965f, 0.735f), red);
                AddShelfGift("ЛЕДЕНЕЦ", candyCaneSprite, new Vector2(0.755f, 0.305f), new Vector2(0.855f, 0.505f), red);
                AddShelfGift("СНЕЖНЫЙ ШАР", snowGlobeSprite, new Vector2(0.865f, 0.305f), new Vector2(0.965f, 0.505f), green);
            }
            if (workshopExtraShelf)
            {
                Panel(root.transform, "Дополнительная декоративная полка",
                    new Vector2(0.755f, 0.245f), new Vector2(0.965f, 0.275f), gold);
                Label("НОВАЯ ПОЛКА ✦", 12, brown,
                    new Vector2(0.76f, 0.245f), new Vector2(0.96f, 0.275f), TextAnchor.MiddleCenter);
            }
        }

        private void AddRequestedGift(Sprite sprite, string caption, Vector2 min, Vector2 max)
        {
            Panel(root.transform, "Золотая рамка требуемого подарка", min, max, gold);
            Vector2 tileInset = new Vector2(0.004f, 0.004f);
            Panel(root.transform, "Кремовая подложка требуемого подарка",
                min + tileInset, max - tileInset, cream);

            Vector2 imageInset = new Vector2(0.008f, 0.012f);
            Image image = ImageAt(root.transform, "Подарок в заказе",
                min + imageInset, max - imageInset, sprite, Color.white);
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
            AddGoldOutline(tile, 2.5f);

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
            SetProgressionOrder(orderData, currentDay, currentOrderNumber,
                currentBalance, reputation, false);
        }

        public void SetProgressionOrder(GiftOrderDefinition orderData, int currentDay,
            int currentOrderNumber, int currentBalance, int currentReputation, bool showSecretWish)
        {
            if (orderData == null)
            {
                currentOrder = null;
                secretWishAnswered = false;
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

            currentOrder = new CustomerOrderData(orderData, showSecretWish, requiredGifts.ToArray());
            secretWishAnswered = false;

            Debug.Log(
                $"[SecretWishFlow] UI received {orderData.CustomerName}: " +
                $"showSecretWish={showSecretWish}, hasValidSecretWishData={currentOrder.HasValidSecretWishData}.",
                this);

            day = currentDay;
            order = currentOrderNumber;
            coins = currentBalance;
            reputation = currentReputation;
        }

        public void SetProgressionBalance(int currentBalance, int currentReputation = 0)
        {
            coins = currentBalance;
            reputation = currentReputation;
        }

        public void SetWorkshopDecorations(bool garland, bool extraShelf, bool premiumWrapping)
        {
            workshopGarland = garland;
            workshopExtraShelf = extraShelf;
            workshopPremiumWrapping = premiumWrapping;
        }

        public void ShowStoryDialogue(string speaker, string line, Action onContinue)
        {
            Build("Сюжетный диалог");
            FestivePanel(root.transform, "Праздничная карточка диалога",
                new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.86f), paper);
            FestiveHeader(root.transform, "Заголовок реплики", speaker.ToUpperInvariant(),
                new Vector2(0.18f, 0.70f), new Vector2(0.82f, 0.81f), red, 26);
            CreateText(root.transform, line, 25, brown,
                new Vector2(0.19f, 0.39f), new Vector2(0.81f, 0.68f),
                TextAnchor.MiddleCenter, Vector2.zero);
            ButtonAt("ПРОДОЛЖИТЬ", green, () => onContinue?.Invoke(),
                new Vector2(0.34f, 0.07f), new Vector2(0.66f, 0.16f));
        }

        public void ShowWorkshopScreen()
        {
            workshopReturnAction = RenderOrderSuccessScreen;
            Build("Улучшения мастерской");
            FestivePanel(root.transform, "Панель улучшений мастерской",
                new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.92f), paper);
            FestiveHeader(root.transform, "Заголовок улучшений", "МАСТЕРСКАЯ",
                new Vector2(0.14f, 0.82f), new Vector2(0.86f, 0.91f), red, 30);
            Label("БАЛАНС: " + coins + " МОНЕТ", 22, gold,
                new Vector2(0.18f, 0.75f), new Vector2(0.82f, 0.81f), TextAnchor.MiddleCenter);
            DrawWorkshopUpgrade(WorkshopUpgradeType.FestiveGarland,
                "РОЖДЕСТВЕНСКАЯ ГИРЛЯНДА", "Добавит тёплые огоньки в заголовок лавки.",
                LastGiftWorkshopUpgradeManager.GarlandCost, workshopGarland,
                new Vector2(0.15f, 0.57f), new Vector2(0.85f, 0.71f));
            DrawWorkshopUpgrade(WorkshopUpgradeType.ExtraShelf,
                "ДОПОЛНИТЕЛЬНАЯ ПОЛКА", "Небольшое украшение и новая полка в мастерской.",
                LastGiftWorkshopUpgradeManager.ExtraShelfCost, workshopExtraShelf,
                new Vector2(0.15f, 0.39f), new Vector2(0.85f, 0.53f));
            DrawWorkshopUpgrade(WorkshopUpgradeType.PremiumWrapping,
                "ПРЕМИАЛЬНАЯ УПАКОВКА", "Золотая бумага и лента станут выбором по умолчанию.",
                LastGiftWorkshopUpgradeManager.PremiumWrappingCost, workshopPremiumWrapping,
                new Vector2(0.15f, 0.21f), new Vector2(0.85f, 0.35f));
            workshopStatus = Label("Улучшения сохраняются до конца этого прохождения.",
                16, green, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.20f), TextAnchor.MiddleCenter);
            ButtonAt("НАЗАД К ЗАКАЗУ", green, ReturnFromWorkshop,
                new Vector2(0.34f, 0.055f), new Vector2(0.66f, 0.135f));
        }

        private void DrawWorkshopUpgrade(WorkshopUpgradeType upgrade, string title, string description,
            int cost, bool purchased, Vector2 min, Vector2 max)
        {
            FestivePanel(root.transform, "Карточка улучшения " + title, min, max, cream);
            Label(title, 17, brown, new Vector2(min.x + 0.02f, min.y + 0.075f),
                new Vector2(min.x + 0.43f, max.y - 0.015f), TextAnchor.MiddleLeft);
            Label(description, 13, brown, new Vector2(min.x + 0.02f, min.y + 0.015f),
                new Vector2(min.x + 0.48f, min.y + 0.08f), TextAnchor.MiddleLeft);
            string caption = purchased ? "КУПЛЕНО" : "КУПИТЬ · " + cost + " ✦";
            ButtonAt(caption, purchased ? wood : green, () => TryPurchaseWorkshopUpgrade(upgrade),
                new Vector2(max.x - 0.25f, min.y + 0.035f),
                new Vector2(max.x - 0.025f, max.y - 0.035f));
        }

        private void TryPurchaseWorkshopUpgrade(WorkshopUpgradeType upgrade)
        {
            if (PurchaseWorkshopUpgrade != null && PurchaseWorkshopUpgrade.Invoke(upgrade))
            {
                ShowWorkshopScreen();
                return;
            }
            if (workshopStatus != null)
            {
                workshopStatus.text = "Недостаточно монет или улучшение уже куплено.";
                workshopStatus.color = red;
            }
        }

        private void ReturnFromWorkshop()
        {
            Action returnAction = workshopReturnAction;
            workshopReturnAction = null;
            returnAction?.Invoke();
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
                case "ШАРФ": return scarfSprite;
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
                case "ШАРФ": return "шарф";
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

        /// <summary>
        /// Opens the short in-shop sorting challenge. Gift visuals come from the same
        /// resource atlas used by the regular customer order shelf.
        /// </summary>
        public void ShowMixedGiftSortingEvent(Action onCompleted)
        {
            if (sortingEventTimerRoutine != null)
                StopCoroutine(sortingEventTimerRoutine);

            sortingEventCompleted = onCompleted;
            sortingEventPlacedCount = 0;
            sortingEventSolved = false;
            Array.Clear(sortingEventPlaced, 0, sortingEventPlaced.Length);
            Array.Clear(sortingEventCards, 0, sortingEventCards.Length);
            Array.Clear(sortingEventTargets, 0, sortingEventTargets.Length);

            Build("Перепутанные подарки");

            FestivePanel(root.transform, "Праздничная карточка события",
                new Vector2(0.065f, 0.15f), new Vector2(0.935f, 0.95f), paper);
            FestiveHeader(root.transform, "Лента рождественского происшествия",
                "РОЖДЕСТВЕНСКОЕ ПРОИСШЕСТВИЕ ✦",
                new Vector2(0.10f, 0.83f), new Vector2(0.90f, 0.95f), red, 25);
            FestivePanel(root.transform, "Описание происшествия",
                new Vector2(0.08f, 0.745f), new Vector2(0.92f, 0.82f), cream);
            Label("Эльфы перепутали подарки! Перетащи каждый подарок в правильное место.",
                21, brown, new Vector2(0.095f, 0.75f), new Vector2(0.905f, 0.815f), TextAnchor.MiddleCenter);

            int[] shuffledGiftIndices = { 0, 1, 2, 3 };
            for (int i = shuffledGiftIndices.Length - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                int temporary = shuffledGiftIndices[i];
                shuffledGiftIndices[i] = shuffledGiftIndices[swapIndex];
                shuffledGiftIndices[swapIndex] = temporary;
            }

            const float tileWidth = 0.17f;
            const float tileGap = 0.035f;
            float tileStart = (1f - (tileWidth * 4f + tileGap * 3f)) * 0.5f;

            for (int visualIndex = 0; visualIndex < shuffledGiftIndices.Length; visualIndex++)
            {
                int giftIndex = shuffledGiftIndices[visualIndex];
                string giftName = SortingEventGiftNames[giftIndex];
                float left = tileStart + visualIndex * (tileWidth + tileGap);

                GameObject card = new GameObject(
                    "Карточка подарка " + giftName,
                    typeof(RectTransform), typeof(Image));
                card.transform.SetParent(root.transform, false);

                RectTransform cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(left, 0.44f);
                cardRect.anchorMax = new Vector2(left + tileWidth, 0.64f);
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;

                Image cardBackground = card.GetComponent<Image>();
                cardBackground.color = giftIndex % 2 == 0 ? cream : paleBlue;
                cardBackground.raycastTarget = true;
                AddGoldOutline(card, 2.5f);

                Image giftImage = ImageAt(card.transform, "Изображение " + giftName,
                    new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.91f),
                    GetGiftSprite(giftName), Color.white);

                if (giftImage == null)
                {
                    ChildLabel(card.transform, GetGiftDisplayName(giftName), 17, brown,
                        new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.88f), TextAnchor.MiddleCenter);
                }

                Text giftCaption = ChildLabel(card.transform, GetGiftDisplayName(giftName).ToUpperInvariant(),
                    14, brown, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.25f), TextAnchor.MiddleCenter);
                giftCaption.raycastTarget = false;

                LastGiftEventDraggableGift draggable = card.AddComponent<LastGiftEventDraggableGift>();
                draggable.Initialize(this, GetGiftSprite(giftName), giftName);
                sortingEventCards[giftIndex] = card;
            }

            for (int targetIndex = 0; targetIndex < SortingEventTargetNames.Length; targetIndex++)
            {
                float left = tileStart + targetIndex * (tileWidth + tileGap);
                RectTransform target = Panel(root.transform,
                    "Место сортировки " + SortingEventTargetNames[targetIndex],
                    new Vector2(left, 0.20f),
                    new Vector2(left + tileWidth, 0.40f),
                    targetIndex % 2 == 0 ? new Color32(249, 239, 216, 255) : new Color32(224, 239, 232, 255));
                AddGoldOutline(target.gameObject, 2.5f);

                sortingEventTargets[targetIndex] = target;
                ChildLabel(target, SortingEventTargetNames[targetIndex], 15, green,
                    new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.98f), TextAnchor.MiddleCenter);
            }

            sortingEventStatus = Label("Перетащи подарки в нужные места. Ошибка не страшна — попробуй ещё раз!",
                17, brown, new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.17f), TextAnchor.MiddleCenter);
            sortingEventTimer = Label("ОСТАЛОСЬ: 30 С", 17, red,
                new Vector2(0.36f, 0.045f), new Vector2(0.64f, 0.10f), TextAnchor.MiddleCenter);

            sortingEventSecondsRemaining = 30;
            sortingEventTimerRoutine = StartCoroutine(SortingEventCountdown());
        }

        /// <summary>Called by the event-specific drag component when a gift is released.</summary>
        public void TrySortMixedGift(string giftName, Vector2 screenPosition)
        {
            if (sortingEventSolved || string.IsNullOrEmpty(giftName))
                return;

            int giftIndex = Array.IndexOf(SortingEventGiftNames, giftName.ToUpperInvariant());
            if (giftIndex < 0 || sortingEventPlaced[giftIndex])
                return;

            int targetIndex = -1;
            for (int i = 0; i < sortingEventTargets.Length; i++)
            {
                RectTransform target = sortingEventTargets[i];
                if (target != null && RectTransformUtility.RectangleContainsScreenPoint(
                    target, screenPosition, null))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                SetSortingEventStatus("Отпусти подарок над одним из четырёх мест сортировки.", false);
                return;
            }

            if (targetIndex != giftIndex)
            {
                SetSortingEventStatus("Не подходит. Попробуй другое место.", false);
                StartCoroutine(Pop(sortingEventTargets[targetIndex]));
                return;
            }

            sortingEventPlaced[giftIndex] = true;
            sortingEventPlacedCount++;

            if (sortingEventCards[giftIndex] != null)
                sortingEventCards[giftIndex].SetActive(false);

            Image placedIcon = ImageAt(sortingEventTargets[targetIndex],
                "Рассортированный подарок " + giftName,
                new Vector2(0.22f, 0.04f), new Vector2(0.78f, 0.69f),
                GetGiftSprite(giftName), Color.white);
            if (placedIcon != null)
                placedIcon.raycastTarget = false;

            SetSortingEventStatus(
                sortingEventPlacedCount == SortingEventGiftNames.Length
                    ? "Все подарки на местах! Сейчас проверим работу эльфов…"
                    : "Верно! Осталось подарков: " + (SortingEventGiftNames.Length - sortingEventPlacedCount) + ".",
                true);

            if (sortingEventPlacedCount == SortingEventGiftNames.Length)
                StartCoroutine(FinishMixedGiftSorting());
        }

        private IEnumerator SortingEventCountdown()
        {
            while (sortingEventSecondsRemaining > 0 && !sortingEventSolved)
            {
                if (sortingEventTimer != null)
                    sortingEventTimer.text = "ОСТАЛОСЬ: " + sortingEventSecondsRemaining + " С";

                yield return new WaitForSecondsRealtime(1f);
                sortingEventSecondsRemaining--;
            }

            if (!sortingEventSolved && sortingEventTimer != null)
            {
                sortingEventTimer.text = "МОЖНО ПРОДОЛЖАТЬ — НЕ СПЕШИ!";
                SetSortingEventStatus("Время вышло, но прогресс сохранён. Закончи сортировку в своём темпе.", false);
            }

            sortingEventTimerRoutine = null;
        }

        private IEnumerator FinishMixedGiftSorting()
        {
            sortingEventSolved = true;
            if (sortingEventTimerRoutine != null)
                StopCoroutine(sortingEventTimerRoutine);
            sortingEventTimerRoutine = null;

            if (sortingEventTimer != null)
                sortingEventTimer.text = "ГОТОВО!";
            SetSortingEventStatus("✦ УРА! ВСЕ ПОДАРКИ РАССОРТИРОВАНЫ! ✦", true);

            yield return new WaitForSecondsRealtime(0.75f);

            Action completed = sortingEventCompleted;
            sortingEventCompleted = null;
            completed?.Invoke();
        }

        private void SetSortingEventStatus(string message, bool success)
        {
            if (sortingEventStatus == null)
                return;

            sortingEventStatus.text = message;
            sortingEventStatus.color = success ? green : red;
            if (success)
                StartCoroutine(Pop(sortingEventStatus.transform));
        }

        /// <summary>Shows the reward screen after a sorting event has been completed.</summary>
        public void ShowChristmasEventSuccess(int rewardCoins, Action continueToShop)
        {
            if (sortingEventTimerRoutine != null)
                StopCoroutine(sortingEventTimerRoutine);
            sortingEventTimerRoutine = null;
            sortingEventStatus = null;
            sortingEventTimer = null;
            sortingEventCompleted = null;

            Build("Происшествие завершено");

            // Keep the winter illustration visible around the card and use a
            // layered evergreen surface instead of the large plain paper panel.
            Color evergreen = new Color32(22, 65, 47, 255);
            Color deepEvergreen = new Color32(13, 43, 33, 210);
            Color ribbonRed = new Color32(151, 43, 43, 255);
            Color warmCream = new Color32(255, 246, 224, 255);
            Color softCream = new Color32(249, 235, 203, 255);

            Panel(root.transform, "Мягкая тень праздничной карточки",
                new Vector2(0.065f, 0.058f), new Vector2(0.935f, 0.942f),
                new Vector2(16f, -18f), new Vector2(8f, -10f), deepEvergreen);

            Panel(root.transform, "Внешняя золотая окантовка",
                new Vector2(0.075f, 0.075f), new Vector2(0.925f, 0.925f), gold);
            RectTransform successCard = Panel(root.transform, "Праздничная зелёная карточка",
                new Vector2(0.087f, 0.087f), new Vector2(0.913f, 0.913f), evergreen);

            // A second fine gold line creates a double frame around the dark-green surface.
            Panel(successCard, "Внутренняя золотая окантовка",
                new Vector2(0.022f, 0.022f), new Vector2(0.978f, 0.978f), gold);
            Panel(successCard, "Внутренняя хвойная поверхность",
                new Vector2(0.032f, 0.032f), new Vector2(0.968f, 0.968f), evergreen);

            // Small festive corner details keep the frame warm without crowding the content.
            ChildLabel(successCard, "✦", 22, gold,
                new Vector2(0.045f, 0.91f), new Vector2(0.12f, 0.98f), TextAnchor.MiddleCenter);
            ChildLabel(successCard, "✦", 22, gold,
                new Vector2(0.88f, 0.91f), new Vector2(0.955f, 0.98f), TextAnchor.MiddleCenter);
            ChildLabel(successCard, "❄", 17, warmCream,
                new Vector2(0.045f, 0.045f), new Vector2(0.12f, 0.11f), TextAnchor.MiddleCenter);
            ChildLabel(successCard, "❄", 17, warmCream,
                new Vector2(0.88f, 0.045f), new Vector2(0.955f, 0.11f), TextAnchor.MiddleCenter);

            // Red title ribbon with a gold backing, slim edge and star accents.
            Panel(successCard, "Золотая основа праздничной ленты",
                new Vector2(0.115f, 0.765f), new Vector2(0.885f, 0.925f), gold);
            Panel(successCard, "Красная праздничная лента",
                new Vector2(0.128f, 0.782f), new Vector2(0.872f, 0.908f), ribbonRed);
            Panel(successCard, "Золотая линия на ленте",
                new Vector2(0.145f, 0.792f), new Vector2(0.855f, 0.799f), gold);
            Panel(successCard, "Нижняя золотая линия ленты",
                new Vector2(0.145f, 0.891f), new Vector2(0.855f, 0.898f), gold);
            ChildLabel(successCard, "✦", 22, gold,
                new Vector2(0.145f, 0.815f), new Vector2(0.21f, 0.88f), TextAnchor.MiddleCenter);
            ChildLabel(successCard, "✦", 22, gold,
                new Vector2(0.79f, 0.815f), new Vector2(0.855f, 0.88f), TextAnchor.MiddleCenter);
            CreateText(successCard, "Эльфам снова можно доверять!", 34, warmCream,
                new Vector2(0.20f, 0.805f), new Vector2(0.80f, 0.885f),
                TextAnchor.MiddleCenter, Vector2.zero);

            // A compact cream inset keeps the result message legible against green.
            Panel(successCard, "Золотая рамка основного текста",
                new Vector2(0.17f, 0.605f), new Vector2(0.83f, 0.745f), gold);
            Panel(successCard, "Светлая область основного текста",
                new Vector2(0.179f, 0.615f), new Vector2(0.821f, 0.735f), warmCream);
            CreateText(successCard,
                "Все подарки вернулись на свои места. Лавка снова готова принимать покупателей!",
                24, brown, new Vector2(0.205f, 0.622f), new Vector2(0.795f, 0.728f),
                TextAnchor.MiddleCenter, Vector2.zero);

            // Four small existing in-game gift sprites form a decorative reward row.
            string[] giftNames =
            {
                "МИШКА", "КОРОБКА", "ЛЕДЕНЕЦ", "СНЕЖНЫЙ ШАР"
            };
            Sprite[] giftSprites =
            {
                teddySprite, giftBoxSprite, candyCaneSprite, snowGlobeSprite
            };

            const float tileWidth = 0.132f;
            const float tileGap = 0.035f;
            float tileStart = (1f - (tileWidth * 4f + tileGap * 3f)) * 0.5f;

            for (int i = 0; i < giftSprites.Length; i++)
            {
                float left = tileStart + i * (tileWidth + tileGap);
                RectTransform tile = Panel(successCard, "Подарочная миниатюра " + giftNames[i],
                    new Vector2(left - 0.008f, 0.385f),
                    new Vector2(left + tileWidth + 0.008f, 0.565f), gold);
                Panel(tile, "Светлая подложка подарка",
                    new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f), softCream);

                ImageAt(tile, "Спрайт " + giftNames[i],
                    new Vector2(0.16f, 0.24f), new Vector2(0.84f, 0.94f),
                    giftSprites[i], Color.white);
                ChildLabel(tile, giftNames[i], 12, brown,
                    new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.23f),
                    TextAnchor.MiddleCenter);
            }

            // The burgundy reward plaque has its own gold border and clear spacing.
            Panel(successCard, "Золотая рамка награды",
                new Vector2(0.22f, 0.205f), new Vector2(0.78f, 0.345f), gold);
            RectTransform rewardPlaque = Panel(successCard, "Бордовая плашка награды",
                new Vector2(0.232f, 0.218f), new Vector2(0.768f, 0.332f), ribbonRed);
            ChildLabel(rewardPlaque,
                rewardCoins > 0 ? "+" + rewardCoins + " МОНЕТ ✦" : "БОНУС ЗА ЭТО ПРОИСШЕСТВИЕ УЖЕ ПОЛУЧЕН",
                rewardCoins > 0 ? 34 : 16, warmCream,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleCenter);

            // The button is layered over a gold backplate but keeps the existing callback.
            Panel(root.transform, "Тень кнопки продолжения",
                new Vector2(0.325f, 0.112f), new Vector2(0.675f, 0.208f),
                new Vector2(5f, -5f), new Vector2(3f, -3f), deepEvergreen);
            Panel(root.transform, "Золотая окантовка кнопки продолжения",
                new Vector2(0.325f, 0.12f), new Vector2(0.675f, 0.205f), gold);
            ButtonAt("ПРОДОЛЖИТЬ РАБОТУ", green, () => continueToShop?.Invoke(),
                new Vector2(0.337f, 0.132f), new Vector2(0.663f, 0.193f));

            StartCoroutine(Pop(successCard));
        }

        /// <summary>
        /// Advances the campaign, then redraws the next order only when the progression
        /// manager has actually supplied one. Day summaries and the final screen stay visible
        /// until their own flow advances the campaign.
        /// </summary>
        private void ContinueProgressionAndRefresh()
        {
            if (Continue == null)
            {
                Gameplay();
                return;
            }

            Continue.Invoke();

            if (currentOrder != null)
                Gameplay();
        }

        public void OrderSuccess(int earned)
        {
            OrderSuccess(earned, 0, 0);
        }

        public void OrderSuccess(int earned, int wrappingBonus, int reputationBonus, bool allowWorkshop = true)
        {
            coins += earned;
            order++;
            currentOrder = null;
            lastResultEarned = earned;
            lastResultWrappingBonus = wrappingBonus;
            lastResultReputationBonus = reputationBonus;
            lastResultCanOpenWorkshop = allowWorkshop;
            RenderOrderSuccessScreen();
        }

        private void RenderOrderSuccessScreen()
        {
            Build("Заказ выполнен");
            string resultMessage = "Покупатель доволен!\nТы получаешь монеты и помогаешь сделать чей-то праздник теплее.";
            if (lastResultWrappingBonus > 0)
                resultMessage += "\nБонус за удачное оформление: +" + lastResultWrappingBonus + " монет.";
            if (lastResultReputationBonus > 0)
                resultMessage += "\nИдеальное сочетание! Репутация +" + lastResultReputationBonus + ".";
            RectTransform successCard = Card("Заказ выполнен!", resultMessage);
            ChildLabel(successCard, "+ " + lastResultEarned + " МОНЕТ ✦", 44, gold,
                new Vector2(0.10f, 0.21f), new Vector2(0.90f, 0.41f), TextAnchor.MiddleCenter);
            Button("СЛЕДУЮЩИЙ ПОКУПАТЕЛЬ", green, ContinueProgressionAndRefresh, 0, -245, 430, 66);
            if (lastResultCanOpenWorkshop)
                Button("УЛУЧШИТЬ ЛАВКУ", wood, () => OpenWorkshop?.Invoke(), 0, -330, 330, 58);
        }

        public void DayEnd(int orders, int earnings)
        {
            day++;
            Build("Конец дня");
            Card("Рабочий день окончен!", "Выполнено заказов: " + orders + "\nЗаработано монет: " + earnings + "\n\nОтличная работа! Завтра лавка снова откроется.");
            Button("НАЧАТЬ НОВЫЙ ДЕНЬ", green, ContinueProgressionAndRefresh, 0, -250, 380, 70);
        }

        public void FinalScreen(GameSummaryData summary)
        {
            Build("Финал");
            string body = summary == null
                ? "Спасибо за тепло и заботу!\nПусть в каждом подарке будет немного рождественского чуда."
                : "Рождественский вечер завершён!\nВыполнено заказов: " + summary.OrdersCompleted
                    + "\nЗаработано монет: " + summary.TotalCoinsEarned
                    + "\nИтоговый баланс: " + summary.FinalCoins
                    + "\nРепутация: " + summary.ReputationEarned
                    + "\n\nСпасибо за рождественское чудо в лавке!";
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

        private RectTransform FestivePanel(
            Transform parent,
            string objectName,
            Vector2 min,
            Vector2 max,
            Color surface)
        {
            Color shadow = new Color32(43, 29, 23, 105);
            Vector2 shadowOffset = new Vector2(8f, -7f);
            Panel(parent, objectName + " — мягкая тень",
                min, max, shadowOffset, shadowOffset, shadow);
            Panel(parent, objectName + " — деревянная основа", min, max, wood);

            Vector2 goldInset = new Vector2(0.005f, 0.005f);
            Panel(parent, objectName + " — золотая окантовка",
                min + goldInset, max - goldInset, gold);

            Vector2 surfaceInset = new Vector2(0.012f, 0.012f);
            return Panel(parent, objectName + " — внутренняя поверхность",
                min + surfaceInset, max - surfaceInset, surface);
        }

        private RectTransform FestiveHeader(
            Transform parent,
            string objectName,
            string title,
            Vector2 min,
            Vector2 max,
            Color ribbonColor,
            int fontSize,
            bool showStars = true)
        {
            Panel(parent, objectName + " — золотая основа", min, max, gold);

            Vector2 inset = new Vector2(0.006f, 0.006f);
            RectTransform ribbon = Panel(parent, objectName + " — цветная лента",
                min + inset, max - inset, ribbonColor);

            Panel(parent, objectName + " — верхний золотой кант",
                new Vector2(min.x + 0.018f, max.y - 0.014f),
                new Vector2(max.x - 0.018f, max.y - 0.008f), gold);
            Panel(parent, objectName + " — нижний золотой кант",
                new Vector2(min.x + 0.018f, min.y + 0.008f),
                new Vector2(max.x - 0.018f, min.y + 0.014f), gold);

            float sideInset = showStars ? 0.075f : 0.018f;
            if (showStars)
            {
                ChildLabel(parent, "✦", Mathf.Max(12, fontSize - 3), gold,
                    new Vector2(min.x + 0.012f, min.y + 0.012f),
                    new Vector2(min.x + 0.072f, max.y - 0.012f), TextAnchor.MiddleCenter);
                ChildLabel(parent, "✦", Mathf.Max(12, fontSize - 3), gold,
                    new Vector2(max.x - 0.072f, min.y + 0.012f),
                    new Vector2(max.x - 0.012f, max.y - 0.012f), TextAnchor.MiddleCenter);
            }

            ChildLabel(parent, title, fontSize, paper,
                new Vector2(min.x + sideInset, min.y + 0.015f),
                new Vector2(max.x - sideInset, max.y - 0.015f),
                TextAnchor.MiddleCenter);
            return ribbon;
        }

        private void AddGoldOutline(GameObject target, float distance)
        {
            if (target == null) return;

            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.AddComponent<Outline>();

            outline.effectColor = gold;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
        }

        private RectTransform Card(string title, string body, bool isMainMenu = false)
        {
            bool isTutorial = title == "Как играть";
            bool isOrderSuccess = title == "Заказ выполнен!";
            Vector2 cardMin = isTutorial
                ? new Vector2(0.10f, 0.28f)
                : isOrderSuccess ? new Vector2(0.12f, 0.26f) : new Vector2(0.12f, 0.36f);
            Vector2 cardMax = isTutorial
                ? new Vector2(0.90f, 0.90f)
                : isOrderSuccess ? new Vector2(0.88f, 0.92f) : new Vector2(0.88f, 0.88f);

            if (isMainMenu)
            {
                RectTransform festiveCard = FestivePanel(
                    root.transform, "Праздничная карточка главного меню",
                    cardMin, cardMax, paper);

                Panel(festiveCard, "Золотая основа заголовка",
                    new Vector2(0.035f, 0.785f), new Vector2(0.965f, 0.975f), gold);
                Panel(festiveCard, "Бордовая праздничная лента",
                    new Vector2(0.045f, 0.800f), new Vector2(0.955f, 0.960f), red);
                Panel(festiveCard, "Золотая линия ленты сверху",
                    new Vector2(0.045f, 0.948f), new Vector2(0.955f, 0.960f), gold);
                Panel(festiveCard, "Золотая линия ленты снизу",
                    new Vector2(0.045f, 0.800f), new Vector2(0.955f, 0.812f), gold);

                ChildLabel(festiveCard, "✦", 23, gold,
                    new Vector2(0.055f, 0.825f), new Vector2(0.155f, 0.935f), TextAnchor.MiddleCenter);
                ChildLabel(festiveCard, "✦", 23, gold,
                    new Vector2(0.845f, 0.825f), new Vector2(0.945f, 0.935f), TextAnchor.MiddleCenter);
                CreateText(festiveCard, title, 34, paper,
                    new Vector2(0.15f, 0.815f), new Vector2(0.85f, 0.95f),
                    TextAnchor.MiddleCenter, Vector2.zero);

                Panel(festiveCard, "Золотая рамка приветственного текста",
                    new Vector2(0.065f, 0.105f), new Vector2(0.935f, 0.755f), gold);
                Panel(festiveCard, "Кремовая подложка приветственного текста",
                    new Vector2(0.075f, 0.120f), new Vector2(0.925f, 0.740f), cream);
                CreateText(festiveCard, body, 25, brown,
                    new Vector2(0.105f, 0.16f), new Vector2(0.895f, 0.70f),
                    TextAnchor.MiddleCenter, Vector2.zero);

                return festiveCard;
            }

            RectTransform card = FestivePanel(root.transform, "Праздничная карточка — " + title,
                cardMin, cardMax, paper);

            FestiveHeader(card, "Заголовок карточки — " + title, title,
                new Vector2(0.04f, 0.83f), new Vector2(0.96f, 0.975f),
                isOrderSuccess ? red : green, isTutorial ? 32 : 30);

            Vector2 bodyMin = isOrderSuccess ? new Vector2(0.075f, 0.44f) : new Vector2(0.075f, 0.10f);
            Vector2 bodyMax = isOrderSuccess
                ? new Vector2(0.925f, 0.79f)
                : new Vector2(0.925f, isTutorial ? 0.81f : 0.70f);
            Vector2 insetMin = bodyMin - new Vector2(0.018f, 0.015f);
            Vector2 insetMax = bodyMax + new Vector2(0.018f, 0.012f);
            FestivePanel(card, "Светлая область текста — " + title,
                insetMin, insetMax, cream);

            CreateText(card, body, isTutorial ? 25 : 27, brown,
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
            AddGoldOutline(handleObject, 1.5f);

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
            AddGoldOutline(buttonObject, 2f);

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
            AddGoldOutline(buttonObject, 2f);

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