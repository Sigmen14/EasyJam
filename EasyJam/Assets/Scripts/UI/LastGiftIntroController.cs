using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TheLastGift.Audio;

namespace TheLastGift.UI
{
    /// <summary>
    /// A self-contained, lightweight Christmas Eve intro. It is installed at runtime
    /// so the existing gameplay scene, UI prefab and progression scripts remain untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastGiftIntroController : MonoBehaviour
    {
        private const float IntroDuration = 25.5f;
        private const float FadeOutDuration = 1.3f;

        // Each story beat pauses the cinematic until the player presses Space.
        private static readonly float[] StoryBeatTimes =
        {
            0.7f, 5.2f, 10.8f, 13.4f, 17.2f, 21.2f
        };

        private static readonly string[] StoryBeats =
        {
            "24 декабря. В городе тихо падает снег.",
            "В маленькой рождественской лавке хозяин готовится закрыться.",
            "К двери подходит мальчик. Он пришёл не просто за подарком.",
            "Его мама — медсестра. Этой ночью она снова будет на смене.",
            "Мальчик мечтает подарить ей тёплый шарф — такой, каким мама укрывает его.",
            "Но ему не хватает денег. Хозяин решает помочь."
        };

        private static LastGiftIntroController activeIntro;
        private Action completionCallback;
        private bool hasCompleted;

        private Canvas introCanvas;
        private CanvasGroup canvasGroup;
        private CanvasGroup titleGroup;
        private CanvasGroup boyGroup;
        private RectTransform worldRoot;
        private RectTransform boyRoot;
        private RectTransform shopkeeperArm;
        private RectTransform doorLeaf;
        private Image windowGlow;
        private Image envelope;
        private Image fadeCurtain;
        private Sprite whiteSprite;
        private Sprite softCircleSprite;
        private Sprite pineTriangleSprite;
        private Texture2D whiteTexture;
        private Texture2D softCircleTexture;
        private Texture2D pineTriangleTexture;
        private Font introFont;
        private bool ownsIntroFont;
        private readonly List<SnowParticle> snowParticles = new List<SnowParticle>();

        private float elapsed;
        private bool exiting;
        private bool awaitingStoryAdvance;
        private int storyBeatIndex;
        private RectTransform storyPanel;
        private Text storyText;
        private RectTransform continuePromptRoot;

        private sealed class SnowParticle
        {
            public RectTransform Rect;
            public float StartX;
            public float StartY;
            public float Speed;
            public float Drift;
            public float Phase;
        }

        public static bool IsPlaying => activeIntro != null;

        /// <summary>
        /// Starts the intro only when the menu explicitly requests it.
        /// The callback resumes the existing tutorial/gameplay flow after the fade.
        /// </summary>
        public static bool TryPlay(Action onCompleted)
        {
            if (activeIntro != null)
                return false;

            GameObject introObject = new GameObject("The Last Gift — Christmas Eve Intro");
            LastGiftIntroController intro = introObject.AddComponent<LastGiftIntroController>();
            intro.completionCallback = onCompleted;
            activeIntro = intro;
            return true;
        }

        private void Awake()
        {
            CreateSprites();
            CreateCanvas();
            BuildTownAndShop();
            BuildSnowfall();
            BuildTitleCard();
            BuildStoryCaptions();
            BuildFadeCurtain();
            BuildSkipPrompt();
            BuildSkipButton();
        }

        private void Update()
        {
            if (exiting)
                return;

            Keyboard keyboard = Keyboard.current;
            if (awaitingStoryAdvance)
            {
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    ContinueStory();

                // Keep the current shot and its line on screen until the player is ready.
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            UpdateCamera();
            UpdateSnowfall();
            UpdateShopkeeper();
            UpdateBoyAndDoor();
            UpdateTitle();

            float fadeIn = Smooth01(elapsed / 1.15f);
            float fadeOut = 1f - Smooth01((elapsed - (IntroDuration - FadeOutDuration)) / FadeOutDuration);
            canvasGroup.alpha = fadeOut;
            if (fadeCurtain != null)
            {
                Color curtainColor = fadeCurtain.color;
                curtainColor.a = 1f - fadeIn;
                fadeCurtain.color = curtainColor;
            }

            if (storyBeatIndex < StoryBeatTimes.Length &&
                elapsed >= StoryBeatTimes[storyBeatIndex])
            {
                ShowStoryBeat(storyBeatIndex);
                awaitingStoryAdvance = true;
                return;
            }

            if (elapsed >= IntroDuration)
                CompleteAndClose();
        }

        private void OnDestroy()
        {
            if (whiteSprite != null)
                Destroy(whiteSprite);
            if (softCircleSprite != null)
                Destroy(softCircleSprite);
            if (pineTriangleSprite != null)
                Destroy(pineTriangleSprite);
            if (whiteTexture != null)
                Destroy(whiteTexture);
            if (softCircleTexture != null)
                Destroy(softCircleTexture);
            if (pineTriangleTexture != null)
                Destroy(pineTriangleTexture);
            if (ownsIntroFont && introFont != null)
                Destroy(introFont);
        }

        private void CreateSprites()
        {
            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.name = "Last Gift Intro — white pixel";
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply(false, true);
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);

            const int diameter = 64;
            softCircleTexture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            softCircleTexture.name = "Last Gift Intro — soft snow";
            Color32[] pixels = new Color32[diameter * diameter];
            Vector2 center = new Vector2((diameter - 1) * 0.5f, (diameter - 1) * 0.5f);
            float radius = diameter * 0.5f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01((1f - distance) * 3.2f);
                    alpha *= alpha;
                    pixels[y * diameter + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            softCircleTexture.SetPixels32(pixels);
            softCircleTexture.filterMode = FilterMode.Bilinear;
            softCircleTexture.wrapMode = TextureWrapMode.Clamp;
            softCircleTexture.Apply(false, true);
            softCircleSprite = Sprite.Create(
                softCircleTexture,
                new Rect(0f, 0f, diameter, diameter),
                new Vector2(0.5f, 0.5f),
                100f);

            // Standard UI sprite keeps the pine silhouettes fully opaque and stable
            // across render pipelines, unlike the custom polygon graphic.
            const int triangleSize = 128;
            pineTriangleTexture = new Texture2D(triangleSize, triangleSize, TextureFormat.RGBA32, false);
            pineTriangleTexture.name = "Last Gift Intro — pine silhouette";
            pineTriangleTexture.filterMode = FilterMode.Bilinear;
            pineTriangleTexture.wrapMode = TextureWrapMode.Clamp;
            Color32[] trianglePixels = new Color32[triangleSize * triangleSize];
            float triangleCenter = (triangleSize - 1) * 0.5f;
            for (int y = 0; y < triangleSize; y++)
            {
                float vertical = (float)y / (triangleSize - 1);
                float halfWidth = (1f - vertical) * triangleSize * 0.49f;
                for (int x = 0; x < triangleSize; x++)
                {
                    float distance = Mathf.Abs((x + 0.5f) - triangleCenter);
                    float coverage = Mathf.Clamp01(halfWidth + 0.5f - distance);
                    trianglePixels[y * triangleSize + x] =
                        new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            pineTriangleTexture.SetPixels32(trianglePixels);
            pineTriangleTexture.Apply(false, true);
            pineTriangleSprite = Sprite.Create(
                pineTriangleTexture,
                new Rect(0f, 0f, triangleSize, triangleSize),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void CreateCanvas()
        {
            GameObject canvasObject = new GameObject(
                "Intro Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            introCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // This transparent graphic prevents clicks from reaching the game menu below the cinematic.
            Image inputBlocker = AddImage(canvasObject.transform, "Intro input blocker",
                Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0f));
            inputBlocker.raycastTarget = true;
            inputBlocker.transform.SetAsFirstSibling();
        }

        private void BuildTownAndShop()
        {
            worldRoot = CreateRect(transform.Find("Intro Canvas"), "Town Camera", Vector2.zero, new Vector2(1920f, 1080f));
            AddImage(worldRoot, "Twilight sky", Vector2.zero, new Vector2(1920f, 1080f), new Color32(25, 39, 61, 255));

            // Soft vertical bands create a subtle illustrated evening gradient without a video or external art.
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                Color band = Color.Lerp(new Color32(29, 48, 72, 255), new Color32(81, 64, 64, 255), t);
                AddImage(worldRoot, "Sky gradient " + i, new Vector2(0f, 506.25f - i * 67.5f),
                    new Vector2(1920f, 68f), band);
            }

            AddImage(worldRoot, "Moon halo", new Vector2(700f, 335f), new Vector2(330f, 330f),
                new Color(1f, 0.82f, 0.57f, 0.12f), softCircleSprite);
            AddImage(worldRoot, "Winter moon", new Vector2(700f, 335f), new Vector2(100f, 100f),
                new Color32(255, 232, 188, 255), softCircleSprite);

            System.Random stars = new System.Random(1224);
            for (int i = 0; i < 54; i++)
            {
                float x = stars.Next(-940, 940);
                float y = stars.Next(170, 520);
                float size = i % 8 == 0 ? 8f : 3.5f;
                AddImage(worldRoot, "Star " + i, new Vector2(x, y), new Vector2(size, size),
                    new Color(1f, 0.9f, 0.72f, 0.68f), softCircleSprite);
            }

            AddPolygon(worldRoot, "Far snowy hill", new Vector2(-390f, -90f), new Vector2(1120f, 490f),
                new[] { new Vector2(-0.5f, -0.5f), new Vector2(-0.16f, 0.22f), new Vector2(0.12f, -0.05f), new Vector2(0.5f, -0.5f) },
                new Color32(57, 82, 105, 255));
            AddPolygon(worldRoot, "Right snowy hill", new Vector2(560f, -80f), new Vector2(1120f, 420f),
                new[] { new Vector2(-0.5f, -0.5f), new Vector2(-0.12f, 0.12f), new Vector2(0.15f, -0.12f), new Vector2(0.5f, -0.5f) },
                new Color32(68, 91, 111, 255));

            BuildBackgroundBuildings();
            AddImage(worldRoot, "Snowy street", new Vector2(0f, -455f), new Vector2(1920f, 260f), new Color32(222, 231, 231, 255));
            AddImage(worldRoot, "Quiet road", new Vector2(0f, -520f), new Vector2(1920f, 100f), new Color32(90, 103, 113, 255));
            AddImage(worldRoot, "Road snow edge", new Vector2(0f, -462f), new Vector2(1920f, 20f), new Color32(247, 246, 232, 255));

            BuildPineTree(worldRoot, -790f, -315f, 1.25f);
            BuildPineTree(worldRoot, 805f, -320f, 1.05f);
            BuildPineTree(worldRoot, -610f, -365f, 0.76f);
            BuildStreetLamp(worldRoot, -570f, -290f);
            BuildShop();

            AddText(worldRoot, "Opening date", "24 ДЕКАБРЯ  ·  РОЖДЕСТВЕНСКИЙ ВЕЧЕР", 20,
                new Vector2(0f, 490f), new Vector2(650f, 45f), new Color32(250, 225, 185, 225),
                TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildBackgroundBuildings()
        {
            int[] positions = { -900, -690, -465, 500, 720, 940 };
            int[] widths = { 210, 230, 250, 230, 210, 190 };
            int[] heights = { 390, 470, 340, 365, 450, 330 };
            Color32[] facades =
            {
                new Color32(57, 63, 76, 255),
                new Color32(76, 65, 67, 255),
                new Color32(59, 76, 83, 255),
                new Color32(71, 66, 75, 255),
                new Color32(55, 74, 79, 255),
                new Color32(78, 64, 65, 255)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                float x = positions[i];
                float height = heights[i];
                AddImage(worldRoot, "Townhouse " + i, new Vector2(x, -125f + height * 0.5f),
                    new Vector2(widths[i], height), facades[i]);
                AddPolygon(worldRoot, "Townhouse roof " + i, new Vector2(x, -125f + height),
                    new Vector2(widths[i] + 34f, 90f),
                    new[] { new Vector2(-0.5f, -0.45f), new Vector2(0f, 0.5f), new Vector2(0.5f, -0.45f) },
                    new Color32(48, 49, 61, 255));
                AddImage(worldRoot, "Snow on roof " + i, new Vector2(x, -125f + height + 5f),
                    new Vector2(widths[i] + 22f, 11f), new Color32(235, 239, 230, 230));

                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 2; column++)
                    {
                        Vector2 windowPosition = new Vector2(
                            x + (column == 0 ? -widths[i] * 0.22f : widths[i] * 0.22f),
                            -125f + height * (0.34f + row * 0.32f));
                        AddImage(worldRoot, "Distant lit window", windowPosition, new Vector2(27f, 44f),
                            (i + row + column) % 3 == 0
                                ? new Color32(248, 190, 104, 170)
                                : new Color32(30, 48, 66, 255));
                    }
                }
            }
        }

        private void BuildPineTree(Transform parent, float x, float baseY, float scale)
        {
            AddImage(parent, "Pine trunk", new Vector2(x, baseY + 50f * scale),
                new Vector2(36f * scale, 120f * scale), new Color32(83, 53, 39, 255));
            AddImage(parent, "Pine lower bough", new Vector2(x, baseY + 150f * scale),
                new Vector2(250f * scale, 250f * scale),
                new Color32(20, 72, 59, 255), pineTriangleSprite);
            AddImage(parent, "Pine middle bough", new Vector2(x, baseY + 225f * scale),
                new Vector2(195f * scale, 210f * scale),
                new Color32(27, 91, 68, 255), pineTriangleSprite);
            AddImage(parent, "Pine upper bough", new Vector2(x, baseY + 285f * scale),
                new Vector2(135f * scale, 155f * scale),
                new Color32(33, 107, 75, 255), pineTriangleSprite);
            AddImage(parent, "Snow cap lower", new Vector2(x, baseY + 78f * scale),
                new Vector2(175f * scale, 12f * scale), new Color32(233, 239, 228, 230));
            AddImage(parent, "Snow cap middle", new Vector2(x, baseY + 188f * scale),
                new Vector2(124f * scale, 10f * scale), new Color32(233, 239, 228, 230));

            Color32[] bulbs =
            {
                new Color32(247, 194, 94, 255),
                new Color32(190, 65, 57, 255),
                new Color32(239, 227, 192, 255)
            };
            for (int i = 0; i < 8; i++)
            {
                float y = baseY + (95f + i * 25f) * scale;
                float offset = (i % 2 == 0 ? -1f : 1f) * (18f + (i % 3) * 15f) * scale;
                AddImage(parent, "Pine light " + i, new Vector2(x + offset, y),
                    new Vector2(11f * scale, 11f * scale), bulbs[i % bulbs.Length], softCircleSprite);
            }
        }

        private void BuildStreetLamp(Transform parent, float x, float baseY)
        {
            AddImage(parent, "Street lamp post", new Vector2(x, baseY + 235f), new Vector2(13f, 470f),
                new Color32(48, 50, 58, 255));
            AddImage(parent, "Street lamp crossbar", new Vector2(x + 25f, baseY + 455f), new Vector2(70f, 11f),
                new Color32(48, 50, 58, 255));
            AddImage(parent, "Street lamp shade", new Vector2(x + 55f, baseY + 430f), new Vector2(73f, 24f),
                new Color32(54, 49, 51, 255));
            AddImage(parent, "Street lamp glow", new Vector2(x + 55f, baseY + 395f), new Vector2(170f, 170f),
                new Color(1f, 0.72f, 0.36f, 0.26f), softCircleSprite);
            AddImage(parent, "Street lamp bulb", new Vector2(x + 55f, baseY + 415f), new Vector2(22f, 30f),
                new Color32(255, 220, 151, 255), softCircleSprite);
        }

        private void BuildShop()
        {
            AddImage(worldRoot, "Shop cast shadow", new Vector2(0f, 15f), new Vector2(1195f, 710f),
                new Color(0.035f, 0.025f, 0.03f, 0.38f));
            AddPolygon(worldRoot, "Shop roof", new Vector2(0f, 385f), new Vector2(1190f, 340f),
                new[] { new Vector2(-0.5f, -0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, -0.5f) },
                new Color32(75, 45, 37, 255));
            AddPolygon(worldRoot, "Roof snow", new Vector2(0f, 510f), new Vector2(1120f, 65f),
                new[] { new Vector2(-0.5f, -0.25f), new Vector2(0f, 0.48f), new Vector2(0.5f, -0.25f) },
                new Color32(238, 241, 229, 245));
            AddImage(worldRoot, "Shop timber facade", new Vector2(0f, 15f), new Vector2(1080f, 580f),
                new Color32(125, 79, 53, 255));
            AddImage(worldRoot, "Shop left timber", new Vector2(-520f, 15f), new Vector2(24f, 600f),
                new Color32(86, 54, 40, 255));
            AddImage(worldRoot, "Shop right timber", new Vector2(520f, 15f), new Vector2(24f, 600f),
                new Color32(86, 54, 40, 255));

            // Close the visible gap between the timber facade and the snowy street.
            AddImage(worldRoot, "Shop foundation shadow", new Vector2(0f, -303f),
                new Vector2(1120f, 70f), new Color32(55, 43, 37, 255));
            AddImage(worldRoot, "Shop stone foundation", new Vector2(0f, -303f),
                new Vector2(1096f, 58f), new Color32(126, 103, 82, 255));
            for (int joint = 0; joint < 8; joint++)
            {
                float jointX = -480f + joint * 137f;
                AddImage(worldRoot, "Foundation stone joint " + joint,
                    new Vector2(jointX, -303f), new Vector2(3f, 48f),
                    new Color32(91, 72, 59, 255));
            }
            AddImage(worldRoot, "Shop foundation upper beam", new Vector2(0f, -273f),
                new Vector2(1110f, 18f), new Color32(78, 48, 37, 255));
            AddImage(worldRoot, "Shop foundation snow edge", new Vector2(0f, -333f),
                new Vector2(1128f, 10f), new Color32(235, 240, 230, 255));

            AddImage(worldRoot, "Shop sign frame", new Vector2(0f, 300f), new Vector2(590f, 88f),
                new Color32(60, 42, 35, 255));
            AddImage(worldRoot, "Shop sign", new Vector2(0f, 300f), new Vector2(570f, 72f),
                new Color32(36, 76, 59, 255));
            AddText(worldRoot, "Shop sign text", "РОЖДЕСТВЕНСКАЯ ЛАВКА", 32,
                new Vector2(0f, 300f), new Vector2(550f, 60f), new Color32(250, 224, 179, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);

            windowGlow = AddImage(worldRoot, "Window warm glow", new Vector2(-255f, 28f),
                new Vector2(670f, 550f), new Color(1f, 0.65f, 0.28f, 0.25f), softCircleSprite);
            AddImage(worldRoot, "Window outer frame", new Vector2(-255f, 35f), new Vector2(548f, 400f),
                new Color32(64, 42, 34, 255));
            AddImage(worldRoot, "Window golden reveal", new Vector2(-255f, 35f), new Vector2(520f, 372f),
                new Color32(230, 173, 102, 255));
            AddImage(worldRoot, "Window interior", new Vector2(-255f, 35f), new Vector2(494f, 346f),
                new Color32(255, 204, 132, 255));
            AddImage(worldRoot, "Interior back wall", new Vector2(-255f, 90f), new Vector2(480f, 235f),
                new Color32(174, 106, 65, 255));
            AddImage(worldRoot, "Window night reflection", new Vector2(-255f, 35f), new Vector2(494f, 346f),
                new Color(0.18f, 0.32f, 0.44f, 0.12f));
            AddImage(worldRoot, "Window vertical mullion", new Vector2(-255f, 35f), new Vector2(13f, 346f),
                new Color32(119, 74, 49, 255));
            AddImage(worldRoot, "Window horizontal mullion", new Vector2(-255f, 20f), new Vector2(494f, 13f),
                new Color32(119, 74, 49, 255));

            for (int shelf = 0; shelf < 2; shelf++)
            {
                float y = 65f + shelf * 100f;
                AddImage(worldRoot, "Shop shelf " + shelf, new Vector2(-85f, y), new Vector2(205f, 15f),
                    new Color32(103, 62, 42, 255));
                for (int gift = 0; gift < 3; gift++)
                {
                    Color32 giftColor = gift == 0 ? new Color32(164, 54, 49, 255)
                        : gift == 1 ? new Color32(36, 104, 72, 255)
                        : new Color32(212, 166, 83, 255);
                    Vector2 giftPosition = new Vector2(-150f + gift * 65f, y + 26f);
                    AddImage(worldRoot, "Gift on shelf", giftPosition, new Vector2(43f, 41f), giftColor);
                    AddImage(worldRoot, "Gift ribbon", giftPosition, new Vector2(7f, 41f),
                        new Color32(245, 220, 167, 255));
                }
            }

            AddImage(worldRoot, "Counter front", new Vector2(-245f, -103f), new Vector2(455f, 56f),
                new Color32(108, 65, 44, 255));
            AddImage(worldRoot, "Counter top", new Vector2(-245f, -69f), new Vector2(485f, 17f),
                new Color32(195, 139, 84, 255));
            BuildShopkeeper();

            // Doorway and hinged door. The door opens toward the viewer when the boy arrives.
            AddImage(worldRoot, "Door frame", new Vector2(350f, 5f), new Vector2(208f, 472f),
                new Color32(65, 42, 34, 255));
            AddImage(worldRoot, "Doorway warm interior", new Vector2(350f, 5f), new Vector2(174f, 438f),
                new Color32(255, 194, 117, 255));
            AddImage(worldRoot, "Door glass", new Vector2(350f, 82f), new Vector2(138f, 250f),
                new Color32(115, 145, 143, 180));
            doorLeaf = CreateRect(worldRoot, "Opening shop door", new Vector2(263f, 5f), new Vector2(174f, 438f));
            doorLeaf.pivot = new Vector2(0f, 0.5f);
            AddImage(doorLeaf, "Door wood panel", Vector2.zero, new Vector2(174f, 438f),
                new Color32(119, 72, 48, 255));
            AddImage(doorLeaf, "Door glass insert", new Vector2(0f, 89f), new Vector2(132f, 230f),
                new Color32(183, 166, 131, 175));
            AddImage(doorLeaf, "Door lower panel", new Vector2(0f, -151f), new Vector2(132f, 95f),
                new Color32(97, 59, 42, 255));
            AddImage(doorLeaf, "Door handle", new Vector2(59f, -5f), new Vector2(10f, 10f),
                new Color32(235, 190, 101, 255), softCircleSprite);

            AddImage(worldRoot, "Shop door step", new Vector2(350f, -225f), new Vector2(240f, 28f),
                new Color32(91, 60, 43, 255));
            AddImage(worldRoot, "Shop snowy awning", new Vector2(0f, 205f), new Vector2(1070f, 22f),
                new Color32(227, 234, 224, 245));

            for (int i = 0; i < 13; i++)
            {
                float x = -490f + i * 82f;
                float y = 218f - Mathf.Abs(x) * 0.045f;
                AddImage(worldRoot, "Garland cable bulb", new Vector2(x, y), new Vector2(14f, 14f),
                    i % 3 == 0 ? new Color32(245, 190, 89, 255)
                    : i % 3 == 1 ? new Color32(177, 52, 48, 255)
                    : new Color32(236, 226, 190, 255), softCircleSprite);
            }

            for (int i = 0; i < 5; i++)
            {
                float x = -350f + i * 175f;
                AddImage(worldRoot, "Facade garland leaf", new Vector2(x, 245f), new Vector2(70f, 13f),
                    new Color32(31, 82, 57, 255));
            }

            BuildBoy();
        }

        private void BuildShopkeeper()
        {
            Color32 skin = new Color32(225, 168, 126, 255);
            AddImage(worldRoot, "Shopkeeper body", new Vector2(-330f, -5f), new Vector2(82f, 135f),
                new Color32(45, 82, 65, 255));
            AddImage(worldRoot, "Shopkeeper apron", new Vector2(-330f, -28f), new Vector2(55f, 84f),
                new Color32(238, 220, 181, 255));
            AddImage(worldRoot, "Shopkeeper neck", new Vector2(-330f, 70f), new Vector2(30f, 34f), skin, softCircleSprite);
            AddImage(worldRoot, "Shopkeeper head", new Vector2(-330f, 118f), new Vector2(84f, 84f), skin, softCircleSprite);
            AddImage(worldRoot, "Shopkeeper hair", new Vector2(-330f, 154f), new Vector2(91f, 44f),
                new Color32(72, 50, 41, 255), softCircleSprite);
            AddImage(worldRoot, "Shopkeeper face", new Vector2(-330f, 106f), new Vector2(68f, 56f), skin, softCircleSprite);
            AddImage(worldRoot, "Shopkeeper left eye", new Vector2(-347f, 112f), new Vector2(6f, 6f),
                new Color32(61, 46, 39, 255), softCircleSprite);
            AddImage(worldRoot, "Shopkeeper right eye", new Vector2(-318f, 112f), new Vector2(6f, 6f),
                new Color32(61, 46, 39, 255), softCircleSprite);
            // The arm's top pivot is embedded in the shopkeeper's right shoulder.
            shopkeeperArm = CreateRect(worldRoot, "Shopkeeper moving arm",
                new Vector2(-291f, 42f), new Vector2(32f, 108f));
            shopkeeperArm.pivot = new Vector2(0.5f, 0.9f);
            AddImage(shopkeeperArm, "Shopkeeper sleeve", Vector2.zero, new Vector2(27f, 112f),
                new Color32(45, 82, 65, 255));
            AddImage(shopkeeperArm, "Shopkeeper hand", new Vector2(0f, -56f), new Vector2(25f, 24f), skin, softCircleSprite);
        }

        private void BuildBoy()
        {
            boyRoot = CreateRect(worldRoot, "Boy with a letter", new Vector2(-860f, -248f), new Vector2(160f, 250f));
            boyGroup = boyRoot.gameObject.AddComponent<CanvasGroup>();
            boyGroup.alpha = 0f;

            Color32 skin = new Color32(236, 184, 145, 255);
            AddImage(boyRoot, "Boy coat", new Vector2(0f, -12f), new Vector2(75f, 105f),
                new Color32(54, 91, 76, 255));
            AddImage(boyRoot, "Boy scarf", new Vector2(0f, 39f), new Vector2(84f, 22f),
                new Color32(169, 54, 49, 255));
            AddImage(boyRoot, "Boy scarf tail", new Vector2(23f, 8f), new Vector2(20f, 58f),
                new Color32(169, 54, 49, 255));
            AddImage(boyRoot, "Boy neck", new Vector2(0f, 64f), new Vector2(27f, 27f), skin, softCircleSprite);
            AddImage(boyRoot, "Boy face", new Vector2(0f, 102f), new Vector2(69f, 75f), skin, softCircleSprite);
            AddImage(boyRoot, "Boy hair", new Vector2(0f, 132f), new Vector2(77f, 35f),
                new Color32(91, 62, 47, 255), softCircleSprite);
            AddPolygon(boyRoot, "Boy winter hat", new Vector2(0f, 153f), new Vector2(88f, 65f),
                new[] { new Vector2(-0.5f, -0.45f), new Vector2(-0.3f, 0.45f), new Vector2(0.28f, 0.5f), new Vector2(0.5f, -0.45f) },
                new Color32(164, 54, 49, 255));
            AddImage(boyRoot, "Boy hat pompom", new Vector2(28f, 180f), new Vector2(18f, 18f),
                new Color32(245, 236, 218, 255), softCircleSprite);

            // Clear, high-contrast facial features remain readable at the final camera scale.
            AddImage(boyRoot, "Boy left cheek", new Vector2(-23f, 87f), new Vector2(12f, 8f),
                new Color32(218, 127, 112, 150), softCircleSprite);
            AddImage(boyRoot, "Boy right cheek", new Vector2(23f, 87f), new Vector2(12f, 8f),
                new Color32(218, 127, 112, 150), softCircleSprite);
            AddImage(boyRoot, "Boy left eye white", new Vector2(-15f, 101f), new Vector2(11f, 14f),
                new Color32(255, 250, 239, 255), softCircleSprite);
            AddImage(boyRoot, "Boy right eye white", new Vector2(15f, 101f), new Vector2(11f, 14f),
                new Color32(255, 250, 239, 255), softCircleSprite);
            AddImage(boyRoot, "Boy left pupil", new Vector2(-14f, 101f), new Vector2(5f, 8f),
                new Color32(55, 43, 39, 255), softCircleSprite);
            AddImage(boyRoot, "Boy right pupil", new Vector2(16f, 101f), new Vector2(5f, 8f),
                new Color32(55, 43, 39, 255), softCircleSprite);
            AddImage(boyRoot, "Boy left eyebrow", new Vector2(-15f, 113f), new Vector2(12f, 3f),
                new Color32(91, 62, 47, 255));
            AddImage(boyRoot, "Boy right eyebrow", new Vector2(15f, 113f), new Vector2(12f, 3f),
                new Color32(91, 62, 47, 255));
            AddImage(boyRoot, "Boy nose", new Vector2(0f, 88f), new Vector2(6f, 7f),
                new Color32(204, 139, 111, 255), softCircleSprite);
            AddImage(boyRoot, "Boy smile", new Vector2(0f, 76f), new Vector2(13f, 5f),
                new Color32(111, 59, 54, 255), softCircleSprite);

            AddImage(boyRoot, "Boy left boot", new Vector2(-22f, -77f), new Vector2(34f, 20f),
                new Color32(70, 53, 47, 255));
            AddImage(boyRoot, "Boy right boot", new Vector2(25f, -77f), new Vector2(34f, 20f),
                new Color32(70, 53, 47, 255));
            envelope = AddImage(boyRoot, "Boy letter", new Vector2(54f, 0f), new Vector2(48f, 57f),
                new Color32(255, 243, 211, 255));
            AddPolygon(boyRoot, "Fold on boy letter", new Vector2(54f, 10f), new Vector2(42f, 27f),
                new[] { new Vector2(-0.5f, 0.5f), new Vector2(0f, -0.36f), new Vector2(0.5f, 0.5f) },
                new Color32(206, 177, 133, 255));
            AddImage(boyRoot, "Letter wax seal", new Vector2(54f, -13f), new Vector2(11f, 11f),
                new Color32(165, 48, 45, 255), softCircleSprite);
        }

        private void BuildSnowfall()
        {
            RectTransform snowRoot = CreateRect(transform.Find("Intro Canvas"), "Falling snow", Vector2.zero, new Vector2(1920f, 1080f));
            System.Random random = new System.Random(2412);
            for (int i = 0; i < 95; i++)
            {
                float size = i % 11 == 0 ? 12f : random.Next(3, 8);
                float x = random.Next(-960, 960);
                float y = random.Next(-540, 540);
                Image flake = AddImage(snowRoot, "Snowflake " + i, new Vector2(x, y),
                    new Vector2(size, size), new Color(1f, 0.99f, 0.94f, 0.58f + (float)random.NextDouble() * 0.35f),
                    softCircleSprite);

                snowParticles.Add(new SnowParticle
                {
                    Rect = flake.rectTransform,
                    StartX = x,
                    StartY = y,
                    Speed = 24f + (float)random.NextDouble() * 74f,
                    Drift = 0.25f + (float)random.NextDouble() * 0.9f,
                    Phase = (float)random.NextDouble() * 6.28f
                });
            }
        }

        private void BuildTitleCard()
        {
            RectTransform canvasRect = transform.Find("Intro Canvas") as RectTransform;
            RectTransform titleRoot = CreateRect(canvasRect, "Title reveal", Vector2.zero, new Vector2(1920f, 1080f));
            titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleGroup.interactable = false;
            titleGroup.blocksRaycasts = false;

            AddImage(titleRoot, "Title soft shade", Vector2.zero, new Vector2(1920f, 1080f),
                new Color(0.035f, 0.055f, 0.08f, 0.38f));
            AddImage(titleRoot, "Title gold outer frame", new Vector2(0f, 5f), new Vector2(1190f, 390f),
                new Color32(202, 158, 85, 235));
            AddImage(titleRoot, "Title dark wooden plaque", new Vector2(0f, 5f), new Vector2(1174f, 374f),
                new Color32(48, 38, 35, 245));
            AddText(titleRoot, "Title date", "24 ДЕКАБРЯ  ·  РОЖДЕСТВЕНСКИЙ ВЕЧЕР", 22,
                new Vector2(0f, 137f), new Vector2(950f, 42f), new Color32(238, 204, 145, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddText(titleRoot, "Game title", "ПОСЛЕДНИЙ ПОДАРОК", 88,
                new Vector2(0f, 42f), new Vector2(1060f, 118f), new Color32(250, 225, 179, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddImage(titleRoot, "Title gold divider", new Vector2(0f, -33f), new Vector2(490f, 3f),
                new Color32(205, 157, 83, 255));
            AddText(titleRoot, "Story subtitle", "ОДНА ПРОСТАЯ ПРОСЬБА МОЖЕТ ИЗМЕНИТЬ ЭТОТ ВЕЧЕР.", 25,
                new Vector2(0f, -110f), new Vector2(980f, 90f), new Color32(244, 235, 216, 255),
                TextAnchor.MiddleCenter, FontStyle.Normal);
            AddText(titleRoot, "Title ornament", "✦     ❄     ✦", 25,
                new Vector2(0f, -184f), new Vector2(380f, 38f), new Color32(193, 72, 60, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildFadeCurtain()
        {
            fadeCurtain = AddImage(transform.Find("Intro Canvas"), "Opening fade from black",
                Vector2.zero, new Vector2(1920f, 1080f), new Color(0.025f, 0.035f, 0.055f, 1f));
            fadeCurtain.raycastTarget = false;
        }

        private void BuildStoryCaptions()
        {
            RectTransform canvasRect = transform.Find("Intro Canvas") as RectTransform;
            storyPanel = CreateRect(canvasRect, "Story dialogue panel",
                new Vector2(0f, -322f), new Vector2(1320f, 142f));
            AddImage(storyPanel, "Story panel gold frame", Vector2.zero,
                new Vector2(1320f, 142f), new Color32(208, 164, 91, 242));
            AddImage(storyPanel, "Story panel wooden surface", Vector2.zero,
                new Vector2(1308f, 130f), new Color32(48, 39, 36, 245));
            AddImage(storyPanel, "Story panel inner surface", Vector2.zero,
                new Vector2(1288f, 110f), new Color32(73, 58, 49, 248));

            storyText = AddText(storyPanel, "Story dialogue text", string.Empty, 29,
                Vector2.zero, new Vector2(1210f, 86f), new Color32(250, 239, 218, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            storyPanel.gameObject.SetActive(false);
        }

        private void BuildSkipPrompt()
        {
            continuePromptRoot = CreateRect(transform.Find("Intro Canvas"),
                "Space continue prompt", new Vector2(0f, -468f), new Vector2(500f, 54f));
            AddImage(continuePromptRoot, "Continue prompt gold frame", Vector2.zero,
                new Vector2(500f, 54f), new Color32(208, 164, 91, 238));
            AddImage(continuePromptRoot, "Continue prompt wooden plaque", Vector2.zero,
                new Vector2(492f, 46f), new Color32(55, 45, 41, 245));
            AddText(continuePromptRoot, "Continue prompt label", "ПРОБЕЛ — продолжить", 20,
                Vector2.zero, new Vector2(470f, 40f), new Color32(250, 232, 199, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            continuePromptRoot.gameObject.SetActive(false);
        }

        private void BuildSkipButton()
        {
            RectTransform canvasRect = transform.Find("Intro Canvas") as RectTransform;
            RectTransform buttonRoot = CreateRect(canvasRect, "Skip intro button",
                new Vector2(820f, 470f), new Vector2(230f, 68f));

            Image background = AddImage(buttonRoot, "Skip button background",
                Vector2.zero, new Vector2(230f, 68f), new Color32(153, 45, 43, 250));
            background.raycastTarget = true;

            Button button = buttonRoot.gameObject.AddComponent<Button>();
            LastGiftButtonHover.Attach(button);
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color32(153, 45, 43, 250);
            colors.highlightedColor = new Color32(184, 62, 54, 255);
            colors.pressedColor = new Color32(112, 34, 34, 255);
            colors.selectedColor = new Color32(184, 62, 54, 255);
            button.colors = colors;
            button.onClick.AddListener(() => LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.UiClick));
            button.onClick.AddListener(SkipIntro);

            AddText(buttonRoot, "Skip button label", "ПРОПУСТИТЬ", 20,
                Vector2.zero, new Vector2(210f, 52f), new Color32(255, 242, 220, 255),
                TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void UpdateCamera()
        {
            float pushIn = Smooth01((elapsed - 0.6f) / 8.6f);
            float scale = Mathf.Lerp(0.88f, 1.045f, pushIn);
            float settle = Mathf.Sin(elapsed * 0.48f) * 3.5f;
            worldRoot.localScale = Vector3.one * scale;
            worldRoot.anchoredPosition = new Vector2(0f, -18f + pushIn * 23f + settle);
        }

        private void UpdateSnowfall()
        {
            for (int i = 0; i < snowParticles.Count; i++)
            {
                SnowParticle particle = snowParticles[i];
                float y = particle.StartY - Mathf.Repeat(elapsed * particle.Speed + particle.Phase * 120f, 1120f);
                if (y < -560f)
                    y += 1120f;
                float x = particle.StartX + Mathf.Sin(elapsed * particle.Drift + particle.Phase) * 32f;
                particle.Rect.anchoredPosition = new Vector2(x, y);
            }
        }

        private void UpdateShopkeeper()
        {
            if (shopkeeperArm == null)
                return;

            float closingMotion = Mathf.Sin(elapsed * 1.7f) * 10f;
            float littleWave = Smooth01((elapsed - 9.5f) / 1.4f) * 13f;
            shopkeeperArm.localRotation = Quaternion.Euler(0f, 0f, -16f + closingMotion + littleWave);
            if (windowGlow != null)
            {
                Color glow = windowGlow.color;
                glow.a = 0.22f + (Mathf.Sin(elapsed * 1.4f) + 1f) * 0.035f;
                windowGlow.color = glow;
            }
        }

        private void UpdateBoyAndDoor()
        {
            float arrival = Smooth01((elapsed - 12.2f) / 4.7f);
            boyRoot.anchoredPosition = new Vector2(
                Mathf.Lerp(-860f, 354f, arrival),
                -248f + Mathf.Abs(Mathf.Sin(elapsed * 8.5f)) * 4f * (1f - arrival));

            float visible = Smooth01((elapsed - 11.9f) / 0.65f);
            boyGroup.alpha = visible * (1f - Smooth01((elapsed - 17.0f) / 0.9f));
            float openDoor = Smooth01((elapsed - 15.8f) / 1.1f);
            doorLeaf.localRotation = Quaternion.Euler(0f, 0f, -58f * openDoor);

            if (envelope != null)
            {
                float pulse = 1f + Mathf.Sin(elapsed * 3.4f) * 0.04f;
                envelope.rectTransform.localScale = Vector3.one * pulse;
            }
        }

        private void UpdateTitle()
        {
            if (titleGroup != null)
                titleGroup.alpha = Smooth01((elapsed - 17.4f) / 1.55f);
        }

        private void ShowStoryBeat(int index)
        {
            if (storyText == null || index < 0 || index >= StoryBeats.Length)
                return;

            LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.DialogueAdvance);
            storyText.text = StoryBeats[index];
            if (storyPanel != null)
                storyPanel.gameObject.SetActive(true);
            if (continuePromptRoot != null)
                continuePromptRoot.gameObject.SetActive(true);
        }

        private void ContinueStory()
        {
            if (!awaitingStoryAdvance || exiting)
                return;

            awaitingStoryAdvance = false;
            storyBeatIndex++;

            if (storyPanel != null)
                storyPanel.gameObject.SetActive(false);
            if (continuePromptRoot != null)
                continuePromptRoot.gameObject.SetActive(false);
        }

        private void SkipIntro()
        {
            if (!exiting)
                StartCoroutine(FadeAndClose(0.28f));
        }

        private IEnumerator FadeAndClose(float duration)
        {
            exiting = true;
            float startAlpha = canvasGroup.alpha;
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Smooth01(timer / duration));
                yield return null;
            }

            CompleteAndClose();
        }

        private void CompleteAndClose()
        {
            if (hasCompleted)
                return;

            hasCompleted = true;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (activeIntro == this)
                activeIntro = null;

            Action callback = completionCallback;
            completionCallback = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private static float Smooth01(float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
        }

        private RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Image AddImage(Transform parent, string objectName, Vector2 position, Vector2 size,
            Color color, Sprite sprite = null)
        {
            RectTransform rect = CreateRect(parent, objectName, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : whiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Font GetIntroFont()
        {
            if (introFont != null)
                return introFont;

            introFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (introFont == null || !introFont.HasCharacter('Ё') || !introFont.HasCharacter('ё'))
            {
                introFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans" },
                    28);
                ownsIntroFont = introFont != null;
            }

            if (introFont == null)
            {
                Debug.LogError("Не удалось создать шрифт для русской заставки.", this);
                return null;
            }

            if (!introFont.HasCharacter('Ё') || !introFont.HasCharacter('ё'))
                Debug.LogError("Выбранный шрифт не содержит букву Ё/ё. Проверьте установленные системные шрифты.", this);

            return introFont;
        }

        private Text AddText(Transform parent, string objectName, string value, int fontSize,
            Vector2 position, Vector2 size, Color color, TextAnchor alignment, FontStyle style)
        {
            RectTransform rect = CreateRect(parent, objectName, position, size);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = GetIntroFont();
            CanvasScaler scaler = introCanvas != null ? introCanvas.GetComponent<CanvasScaler>() : null;
            float canvasScaleFactor = GetIntroCanvasScaleFactor(introCanvas, scaler);
            int minimumCanvasSize = Mathf.CeilToInt(25f / canvasScaleFactor);
            int preferredFontSize = fontSize < 40
                ? Mathf.Max(25, fontSize + 3)
                : fontSize;
            int readableFontSize = Mathf.Max(minimumCanvasSize, preferredFontSize);
            // Request the complete Russian alphabet, including Ё/ё, before setting the label text.
            if (label.font != null)
                label.font.RequestCharactersInTexture(
                    "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" + value,
                    readableFontSize,
                    style);
            label.text = value;
            label.fontSize = readableFontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.lineSpacing = 1.12f;
            return label;
        }

        private float GetIntroCanvasScaleFactor(Canvas rootCanvas, CanvasScaler scaler)
        {
            if (rootCanvas == null || scaler == null
                || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                return rootCanvas != null ? Mathf.Max(0.01f, rootCanvas.scaleFactor) : 1f;

            float widthScale = Mathf.Max(0.01f, Screen.width / Mathf.Max(1f, scaler.referenceResolution.x));
            float heightScale = Mathf.Max(0.01f, Screen.height / Mathf.Max(1f, scaler.referenceResolution.y));

            if (scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand)
                return Mathf.Min(widthScale, heightScale);
            if (scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink)
                return Mathf.Max(widthScale, heightScale);

            float match = Mathf.Clamp01(scaler.matchWidthOrHeight);
            return Mathf.Max(0.01f, Mathf.Pow(widthScale, 1f - match) * Mathf.Pow(heightScale, match));
        }

        private void AddPolygon(Transform parent, string objectName, Vector2 position, Vector2 size,
            Vector2[] vertices, Color color)
        {
            RectTransform rect = CreateRect(parent, objectName, position, size);
            LastGiftIntroPolygon graphic = rect.gameObject.AddComponent<LastGiftIntroPolygon>();
            graphic.color = color;
            graphic.raycastTarget = false;
            graphic.SetVertices(vertices);
        }
    }

    /// <summary>Small procedural UI polygon used for roofs, trees and paper folds.</summary>
    public sealed class LastGiftIntroPolygon : MaskableGraphic
    {
        private Vector2[] vertices = Array.Empty<Vector2>();

        public void SetVertices(Vector2[] points)
        {
            vertices = points ?? Array.Empty<Vector2>();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (vertices == null || vertices.Length < 3)
                return;

            Rect rect = GetPixelAdjustedRect();
            for (int i = 0; i < vertices.Length; i++)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = new Vector3(
                    rect.xMin + (vertices[i].x + 0.5f) * rect.width,
                    rect.yMin + (vertices[i].y + 0.5f) * rect.height,
                    0f);
                vertexHelper.AddVert(vertex);
            }

            for (int i = 1; i < vertices.Length - 1; i++)
                vertexHelper.AddTriangle(0, i, i + 1);
        }
    }
}
