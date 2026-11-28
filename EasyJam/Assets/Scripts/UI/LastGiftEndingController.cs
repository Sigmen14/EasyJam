using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastGift.UI
{
    /// <summary>
    /// Plays the story ending as a lightweight, procedural uGUI sequence.
    /// The controller is attached by OrderProgressionUIAdapter only when the campaign ends.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastGiftEndingController : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private static readonly Color32 WarmCream = new Color32(248, 235, 205, 255);
        private static readonly Color32 Wood = new Color32(105, 64, 43, 255);
        private static readonly Color32 Gold = new Color32(225, 180, 91, 255);
        private static readonly Color32 Evergreen = new Color32(38, 83, 61, 255);
        private static readonly Color32 Burgundy = new Color32(151, 48, 47, 255);
        private static readonly Color32 Night = new Color32(20, 35, 57, 255);

        private sealed class SnowParticle
        {
            public RectTransform Rect;
            public float FallSpeed;
            public float Drift;
            public float Phase;
        }

        private readonly List<SnowParticle> snowParticles = new List<SnowParticle>();
        private readonly System.Random random = new System.Random(2412);

        private LastGiftUIController ui;
        private Canvas canvas;
        private GameObject overlayRoot;
        private CanvasGroup overlayGroup;
        private RectTransform interiorGroup;
        private RectTransform streetGroup;
        private RectTransform streetBackground;
        private RectTransform streetBoy;
        private RectTransform gift;
        private RectTransform captionPanel;
        private Text captionText;
        private GameObject skipButton;
        private CanvasGroup finalCardGroup;
        private CanvasGroup quoteGroup;
        private CanvasGroup endGroup;
        private Sprite whiteSprite;
        private Sprite circleSprite;
        private Texture2D whiteTexture;
        private Texture2D circleTexture;
        private Texture2D interiorTexture;
        private Texture2D streetTexture;
        private Sprite interiorSprite;
        private Sprite streetSprite;
        private Coroutine sequence;
        private bool skipRequested;
        private bool finalCardShown;
        private bool restarting;

        /// <summary>Starts the ending. Safe to call once after GameCompleted.</summary>
        public void Play(LastGiftUIController uiController)
        {
            if (overlayRoot != null || uiController == null)
                return;

            ui = uiController;
            canvas = ui.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogError("LastGiftEndingController: UI Canvas was not found.", this);
                return;
            }

            CreateRuntimeSprites();
            BuildEndingScene();
            sequence = StartCoroutine(PlaySequence());
        }

        private void Update()
        {
            if (restarting || snowParticles.Count == 0 || overlayRoot == null)
                return;

            RectTransform rootRect = overlayRoot.GetComponent<RectTransform>();
            float halfWidth = rootRect.rect.width * 0.5f;
            float halfHeight = rootRect.rect.height * 0.5f;
            float delta = Time.unscaledDeltaTime;

            for (int i = 0; i < snowParticles.Count; i++)
            {
                SnowParticle particle = snowParticles[i];
                Vector2 position = particle.Rect.anchoredPosition;
                position.y -= particle.FallSpeed * delta;
                position.x += Mathf.Sin(Time.unscaledTime * 0.65f + particle.Phase)
                    * particle.Drift * delta;

                if (position.y < -halfHeight - 18f)
                {
                    position.y = halfHeight + 18f;
                    position.x = (float)(random.NextDouble() * 2.0 - 1.0) * halfWidth;
                }

                particle.Rect.anchoredPosition = position;
            }
        }

        private IEnumerator PlaySequence()
        {
            yield return Fade(overlayGroup, 0f, 1f, 1.0f);
            if (!skipRequested)
            {
                SetCaption("Хозяин лавки бережно протягивает мальчику его подарок.");
                yield return Hold(1.0f);
            }

            if (!skipRequested)
            {
                yield return HandOverGift();
                SetCaption("«Спасибо! Мама будет очень рада», — тихо говорит мальчик.");
                yield return Hold(2.2f);
            }

            if (!skipRequested)
            {
                SetCaption("С подарком в руках он выходит в рождественскую ночь.");
                yield return CrossToStreet();
            }

            if (!skipRequested)
            {
                SetCaption("Он спешит встретить маму после её рождественской смены.");
                yield return WalkIntoNight();
                yield return Hold(1.0f);
            }

            yield return ShowFinalCard();
            sequence = null;
        }

        private void BuildEndingScene()
        {
            overlayRoot = new GameObject("The Last Gift — финальная заставка", typeof(RectTransform), typeof(CanvasGroup));
            overlayRoot.transform.SetParent(canvas.transform, false);
            overlayRoot.transform.SetAsLastSibling();

            RectTransform rootRect = overlayRoot.GetComponent<RectTransform>();
            Stretch(rootRect);
            overlayGroup = overlayRoot.GetComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = true;
            overlayGroup.blocksRaycasts = true;

            interiorGroup = CreateLayer(overlayRoot.transform, "Тёплая рождественская лавка");
            interiorTexture = LastGiftChristmasBackground.CreateTexture();
            interiorSprite = Sprite.Create(
                interiorTexture,
                new Rect(0f, 0f, interiorTexture.width, interiorTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            AddImage(interiorGroup, "Знакомый интерьер лавки", interiorSprite, Color.white, true);

            AddShape(interiorGroup, "Стойка из тёплого дерева",
                new Vector2(0.5f, 0.155f), new Vector2(1120f, 78f), Wood);
            AddShape(interiorGroup, "Золотой край стойки",
                new Vector2(0.5f, 0.193f), new Vector2(1120f, 7f), Gold);

            CreateCharacter(interiorGroup, "Мальчик", new Vector2(0.355f, 0.235f), 0.92f, false);
            CreateCharacter(interiorGroup, "Хозяин лавки", new Vector2(0.69f, 0.235f), 0.92f, true);
            gift = CreateGift(interiorGroup, new Vector2(0.505f, 0.315f), 1f, "Подарок для мамы");

            streetGroup = CreateLayer(overlayRoot.transform, "Снежная улица");
            streetGroup.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            streetTexture = CreateStreetTexture();
            streetSprite = Sprite.Create(
                streetTexture,
                new Rect(0f, 0f, streetTexture.width, streetTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            Image streetImage = AddImage(streetGroup, "Заснеженная рождественская улица", streetSprite, Color.white, true);
            streetBackground = streetImage.rectTransform;
            streetBackground.localScale = Vector3.one * 1.12f;

            CreateText(streetGroup, "Вывеска лавки", "РОЖДЕСТВЕНСКАЯ ЛАВКА",
                34, Gold, new Vector2(0.34f, 0.755f), new Vector2(0.66f, 0.825f),
                TextAnchor.MiddleCenter);
            CreateText(streetGroup, "Дата рождественского вечера", "24 ДЕКАБРЯ  •  РОЖДЕСТВЕНСКИЙ ВЕЧЕР",
                20, WarmCream, new Vector2(0.24f, 0.91f), new Vector2(0.76f, 0.965f),
                TextAnchor.MiddleCenter);

            streetBoy = CreateCharacter(streetGroup, "Мальчик с подарком", new Vector2(0.56f, 0.20f), 0.62f, false);
            CreateGift(streetBoy, new Vector2(0.34f, 0.40f), 0.52f, "Упакованный шарф");

            CreateSnow();
            BuildCaption();
            skipButton = CreateButton(overlayRoot.transform, "ПРОПУСТИТЬ  ›",
                new Vector2(0.865f, 0.94f), new Vector2(0.985f, 0.99f),
                new Color32(35, 66, 54, 235), Skip);
        }

        private void BuildCaption()
        {
            captionPanel = CreateCenteredRect(overlayRoot.transform, "Подпись к сцене",
                new Vector2(0.5f, 0.075f), new Vector2(1380f, 100f));
            Image backing = captionPanel.gameObject.AddComponent<Image>();
            backing.sprite = whiteSprite;
            backing.color = new Color32(29, 42, 42, 226);
            backing.raycastTarget = false;

            AddShape(captionPanel, "Золотая линия подписи",
                new Vector2(0.5f, 0.08f), new Vector2(1380f, 4f), Gold);
            captionText = CreateText(captionPanel, "Текст подписи", string.Empty,
                27, WarmCream, new Vector2(0.035f, 0.05f), new Vector2(0.965f, 0.95f),
                TextAnchor.MiddleCenter);
        }

        private void CreateSnow()
        {
            RectTransform layer = CreateLayer(overlayRoot.transform, "Мягкий снегопад");
            RectTransform rootRect = overlayRoot.GetComponent<RectTransform>();
            float width = Mathf.Max(1f, rootRect.rect.width);
            float height = Mathf.Max(1f, rootRect.rect.height);

            for (int i = 0; i < 76; i++)
            {
                float x = (float)(random.NextDouble() - 0.5) * width;
                float y = (float)(random.NextDouble() - 0.5) * height;
                float size = 3f + (float)random.NextDouble() * 7f;
                Image flake = AddImage(layer, "Снежинка " + i, circleSprite,
                    new Color(1f, 0.97f, 0.88f, 0.28f + (float)random.NextDouble() * 0.58f), false);
                flake.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                flake.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                flake.rectTransform.sizeDelta = new Vector2(size, size);
                flake.rectTransform.anchoredPosition = new Vector2(x, y);

                snowParticles.Add(new SnowParticle
                {
                    Rect = flake.rectTransform,
                    FallSpeed = 24f + (float)random.NextDouble() * 62f,
                    Drift = 8f + (float)random.NextDouble() * 24f,
                    Phase = (float)random.NextDouble() * Mathf.PI * 2f
                });
            }
        }

        private IEnumerator HandOverGift()
        {
            if (gift == null)
                yield break;

            Vector2 start = new Vector2(0.505f, 0.315f);
            Vector2 end = new Vector2(0.405f, 0.315f);
            float elapsed = 0f;
            const float duration = 1.8f;

            while (elapsed < duration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                gift.anchorMin = Vector2.Lerp(start, end, t);
                gift.anchorMax = gift.anchorMin;
                gift.anchoredPosition = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 55f);
                gift.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * -7f);
                yield return null;
            }

            gift.anchorMin = end;
            gift.anchorMax = end;
            gift.anchoredPosition = Vector2.zero;
            gift.localRotation = Quaternion.identity;
        }

        private IEnumerator CrossToStreet()
        {
            CanvasGroup interiorCanvas = interiorGroup.GetComponent<CanvasGroup>();
            CanvasGroup streetCanvas = streetGroup.GetComponent<CanvasGroup>();
            if (interiorCanvas == null)
                interiorCanvas = interiorGroup.gameObject.AddComponent<CanvasGroup>();

            yield return CrossFade(interiorCanvas, streetCanvas, 1.35f);
        }

        private IEnumerator WalkIntoNight()
        {
            float elapsed = 0f;
            const float duration = 6.4f;
            while (elapsed < duration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                Vector2 position = new Vector2(Mathf.Lerp(0.56f, 0.79f, t), 0.20f + Mathf.Sin(t * Mathf.PI * 5f) * 0.008f);
                streetBoy.anchorMin = position;
                streetBoy.anchorMax = position;
                streetBoy.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 10f) * -2.2f);
                streetBackground.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t);
                yield return null;
            }

            streetBackground.localScale = Vector3.one;
            if (streetBoy != null)
                streetBoy.localRotation = Quaternion.identity;
        }

        private IEnumerator ShowFinalCard()
        {
            if (finalCardShown)
                yield break;

            finalCardShown = true;
            if (skipButton != null)
                skipButton.SetActive(false);

            if (captionPanel != null)
            {
                CanvasGroup captionCanvas = captionPanel.GetComponent<CanvasGroup>();
                if (captionCanvas == null)
                    captionCanvas = captionPanel.gameObject.AddComponent<CanvasGroup>();
                yield return Fade(captionCanvas, captionCanvas.alpha, 0f, 0.45f);
                captionPanel.gameObject.SetActive(false);
            }

            finalCardGroup = CreateFinalCard();
            yield return Fade(finalCardGroup, 0f, 1f, skipRequested ? 0.15f : 1.4f);

            if (quoteGroup != null)
                yield return Fade(quoteGroup, 0f, 1f, skipRequested ? 0.1f : 1.1f);

            if (!skipRequested)
                yield return Hold(3.2f);

            if (endGroup != null)
                yield return Fade(endGroup, 0f, 1f, skipRequested ? 0.1f : 0.9f);
        }

        private CanvasGroup CreateFinalCard()
        {
            RectTransform card = CreateLayer(overlayRoot.transform, "Финальная карточка");
            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image shade = AddImage(card, "Затемнение ночного города", whiteSprite,
                new Color(8f / 255f, 17f / 255f, 30f / 255f, 0.68f), false);
            Stretch(shade.rectTransform);

            AddShape(card, "Деревянная рамка финала",
                new Vector2(0.5f, 0.515f), new Vector2(1450f, 710f), Wood);
            AddShape(card, "Золотая окантовка",
                new Vector2(0.5f, 0.515f), new Vector2(1418f, 678f), Gold);
            AddShape(card, "Хвойно-зелёная подложка",
                new Vector2(0.5f, 0.515f), new Vector2(1398f, 658f), Evergreen);
            AddShape(card, "Кремовая карточка",
                new Vector2(0.5f, 0.515f), new Vector2(1332f, 592f), WarmCream);

            CreateText(card, "Рождественский орнамент",
                "✦     ❄     ✧     ❄     ✦",
                25, Burgundy, new Vector2(0.28f, 0.735f), new Vector2(0.72f, 0.805f),
                TextAnchor.MiddleCenter);

            RectTransform quoteRoot = CreateGroup(card, "Главная мысль", new Vector2(0.5f, 0.56f));
            quoteRoot.sizeDelta = new Vector2(1180f, 260f);
            quoteGroup = quoteRoot.gameObject.AddComponent<CanvasGroup>();
            quoteGroup.alpha = 0f;
            CreateText(quoteRoot.transform, "Финальная цитата",
                "«Иногда самый дорогой подарок — не тот, который стоит больше всего.\nА тот, в который вложили любовь».",
                37, new Color32(74, 49, 37, 255),
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f),
                TextAnchor.MiddleCenter);

            RectTransform endRoot = CreateGroup(card, "Завершение и повторная игра", new Vector2(0.5f, 0.265f));
            endRoot.sizeDelta = new Vector2(760f, 190f);
            endGroup = endRoot.gameObject.AddComponent<CanvasGroup>();
            endGroup.alpha = 0f;
            CreateText(endRoot.transform, "THE END",
                "THE END", 48, Burgundy,
                new Vector2(0.20f, 0.48f), new Vector2(0.80f, 1f),
                TextAnchor.MiddleCenter);
            CreateButton(endRoot.transform, "ПРОЙТИ СНОВА",
                new Vector2(0.27f, 0.0f), new Vector2(0.73f, 0.45f),
                Evergreen, RestartCampaign);
            return group;
        }

        private void SetCaption(string message)
        {
            if (captionText != null)
                captionText.text = message;
        }

        private void Skip()
        {
            if (finalCardShown || skipRequested)
                return;

            skipRequested = true;
            if (skipButton != null)
                skipButton.SetActive(false);
        }

        private void RestartCampaign()
        {
            if (restarting)
                return;

            restarting = true;
            LastGiftUIController controller = ui;

            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            if (overlayRoot != null)
                Destroy(overlayRoot);

            ReleaseRuntimeSprites();
            snowParticles.Clear();

            if (controller != null)
            {
                // Match the existing final-screen flow: reset the campaign first,
                // then return to the title menu so the player can start from day one.
                controller.StartGame?.Invoke();
                controller.MainMenu();
            }
        }

        private IEnumerator Hold(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Fade(CanvasGroup target, float from, float to, float duration)
        {
            if (target == null)
                yield break;

            if (duration <= 0f || skipRequested)
            {
                target.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            target.alpha = from;
            while (elapsed < duration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                target.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            target.alpha = to;
        }

        private IEnumerator CrossFade(CanvasGroup from, CanvasGroup to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                from.alpha = 1f - t;
                to.alpha = t;
                yield return null;
            }

            from.alpha = 0f;
            to.alpha = 1f;
        }

        private RectTransform CreateCharacter(
            Transform parent,
            string name,
            Vector2 anchor,
            float scale,
            bool shopkeeper)
        {
            RectTransform actor = CreateGroup(parent, name, anchor);
            actor.localScale = Vector3.one * scale;

            Color32 coat = shopkeeper
                ? new Color32(43, 91, 65, 255)
                : new Color32(47, 64, 87, 255);
            Color32 scarf = shopkeeper ? Gold : Burgundy;
            Color32 skin = new Color32(237, 190, 151, 255);
            Color32 hair = shopkeeper
                ? new Color32(79, 50, 37, 255)
                : new Color32(64, 43, 34, 255);

            AddActorPart(actor, "Пальто", new Vector2(0f, -23f), new Vector2(122f, 164f), coat);
            AddActorPart(actor, "Левый рукав", new Vector2(-72f, 9f), new Vector2(47f, 126f), coat, true);
            AddActorPart(actor, "Правый рукав", new Vector2(72f, 9f), new Vector2(47f, 126f), coat, true);
            AddActorPart(actor, "Левая рука", new Vector2(-74f, -49f), new Vector2(29f, 55f), skin, true);
            AddActorPart(actor, "Правая рука", new Vector2(74f, -49f), new Vector2(29f, 55f), skin, true);
            AddActorPart(actor, "Шарф", new Vector2(0f, 54f), new Vector2(104f, 22f), scarf);
            AddActorPart(actor, "Свисающий конец шарфа", new Vector2(30f, 20f), new Vector2(23f, 64f), scarf);
            AddActorPart(actor, "Голова", new Vector2(0f, 112f), new Vector2(91f, 91f), skin, true);
            AddActorPart(actor, "Волосы", new Vector2(0f, 147f), new Vector2(95f, 36f), hair);
            AddActorPart(actor, "Левая прядь", new Vector2(-38f, 128f), new Vector2(20f, 43f), hair, true);
            AddActorPart(actor, "Правая прядь", new Vector2(37f, 128f), new Vector2(20f, 43f), hair, true);
            AddActorPart(actor, "Левый ботинок", new Vector2(-34f, -111f), new Vector2(50f, 28f), new Color32(62, 43, 34, 255), true);
            AddActorPart(actor, "Правый ботинок", new Vector2(34f, -111f), new Vector2(50f, 28f), new Color32(62, 43, 34, 255), true);
            AddActorPart(actor, "Глаз", new Vector2(-17f, 112f), new Vector2(7f, 7f), new Color32(53, 43, 37, 255), true);
            AddActorPart(actor, "Глаз", new Vector2(17f, 112f), new Vector2(7f, 7f), new Color32(53, 43, 37, 255), true);

            if (shopkeeper)
            {
                AddActorPart(actor, "Фартук лавочника", new Vector2(0f, -26f), new Vector2(72f, 111f), new Color32(66, 111, 76, 255));
                AddActorPart(actor, "Пуговица", new Vector2(0f, -11f), new Vector2(8f, 8f), Gold, true);
            }

            return actor;
        }

        private void AddActorPart(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            bool round = false)
        {
            RectTransform rect = CreateCenteredRect(parent, name, new Vector2(0.5f, 0.5f), size);
            rect.anchoredPosition = position;
            AddImage(rect, name + " изображение", round ? circleSprite : whiteSprite, color, false);
        }

        private RectTransform CreateGift(Transform parent, Vector2 anchor, float scale, string name)
        {
            RectTransform giftRoot = CreateGroup(parent, name, anchor);
            giftRoot.sizeDelta = new Vector2(150f, 135f);
            giftRoot.localScale = Vector3.one * scale;
            AddActorPart(giftRoot, "Коробка", new Vector2(0f, -8f), new Vector2(104f, 78f), Burgundy);
            AddActorPart(giftRoot, "Золотая лента — вертикаль", new Vector2(0f, -8f), new Vector2(18f, 78f), Gold);
            AddActorPart(giftRoot, "Золотая лента — горизонталь", new Vector2(0f, -8f), new Vector2(104f, 16f), Gold);
            AddActorPart(giftRoot, "Крышка", new Vector2(0f, 36f), new Vector2(120f, 19f), new Color32(178, 59, 52, 255));
            AddActorPart(giftRoot, "Бант слева", new Vector2(-15f, 57f), new Vector2(34f, 20f), Gold, true);
            AddActorPart(giftRoot, "Бант справа", new Vector2(15f, 57f), new Vector2(34f, 20f), Gold, true);
            AddActorPart(giftRoot, "Узел банта", new Vector2(0f, 57f), new Vector2(19f, 19f), WarmCream, true);
            return giftRoot;
        }

        private void CreateRuntimeSprites()
        {
            whiteTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            whiteTexture.SetPixels32(new[]
            {
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255)
            });
            whiteTexture.Apply(false, true);
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);

            const int diameter = 64;
            circleTexture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[diameter * diameter];
            Vector2 center = new Vector2(diameter * 0.5f, diameter * 0.5f);
            float radius = diameter * 0.5f - 1f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    byte alpha = (byte)(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                    pixels[y * diameter + x] = new Color32(255, 255, 255, alpha);
                }
            }

            circleTexture.SetPixels32(pixels);
            circleTexture.Apply(false, true);
            circleSprite = Sprite.Create(
                circleTexture,
                new Rect(0f, 0f, diameter, diameter),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void ReleaseRuntimeSprites()
        {
            DestroySprite(interiorSprite);
            DestroySprite(streetSprite);
            DestroySprite(whiteSprite);
            DestroySprite(circleSprite);
            DestroyTexture(interiorTexture);
            DestroyTexture(streetTexture);
            DestroyTexture(whiteTexture);
            DestroyTexture(circleTexture);

            interiorSprite = null;
            streetSprite = null;
            whiteSprite = null;
            circleSprite = null;
            interiorTexture = null;
            streetTexture = null;
            whiteTexture = null;
            circleTexture = null;
        }

        private void DestroySprite(Sprite sprite)
        {
            if (sprite != null)
                Destroy(sprite);
        }

        private void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
                Destroy(texture);
        }

        private RectTransform CreateLayer(Transform parent, string name)
        {
            RectTransform rect = CreateCenteredRect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(rect);
            return rect;
        }

        private RectTransform CreateGroup(Transform parent, string name, Vector2 anchor)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(260f, 340f);
            return rect;
        }

        private RectTransform CreateCenteredRect(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform AddShape(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            Color color)
        {
            RectTransform rect = CreateCenteredRect(parent, name, anchor, size);
            AddImage(rect, name + " изображение", whiteSprite, color, false);
            return rect;
        }

        private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool stretch)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (stretch)
                Stretch(image.rectTransform);
            else
            {
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = Vector2.zero;
                image.rectTransform.offsetMax = Vector2.zero;
            }

            return image;
        }

        private Text CreateText(
            Transform parent,
            string objectName,
            string value,
            int fontSize,
            Color color,
            Vector2 min,
            Vector2 max,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, objectName, min, max);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(16, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            text.supportRichText = true;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.30f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            return text;
        }

        private GameObject CreateButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(parent, label, min, max);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text text = CreateText(rect, "Надпись кнопки", label, 22, WarmCream,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            text.resizeTextMinSize = 17;
            return rect.gameObject;
        }

        private RectTransform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Texture2D CreateStreetTexture()
        {
            const int width = 960;
            const int height = 540;
            Color32[] pixels = new Color32[width * height];

            Color top = new Color32(17, 30, 52, 255);
            Color bottom = new Color32(71, 86, 107, 255);
            for (int y = 0; y < height; y++)
            {
                Color32 row = Color.Lerp(bottom, top, y / (float)(height - 1));
                int offset = y * width;
                for (int x = 0; x < width; x++)
                    pixels[offset + x] = row;
            }

            FillCircle(pixels, width, height, 784, 427, 75, new Color32(235, 209, 157, 35));
            FillCircle(pixels, width, height, 784, 427, 37, new Color32(255, 230, 184, 255));
            FillCircle(pixels, width, height, 796, 435, 34, new Color32(24, 39, 61, 255));

            for (int i = 0; i < 90; i++)
            {
                int x = random.Next(0, width);
                int y = random.Next(200, 520);
                FillCircle(pixels, width, height, x, y, i % 9 == 0 ? 2 : 1,
                    new Color32(255, 240, 207, 220));
            }

            DrawDistantHouse(pixels, width, height, 0, 170, 210, 240);
            DrawDistantHouse(pixels, width, height, 765, 164, 195, 234);

            // The main shop: layered timber, a steep snowy roof and glowing windows.
            FillRect(pixels, width, height, 235, 128, 725, 418, new Color32(110, 67, 48, 255));
            FillTriangle(pixels, width, height, 480, 505, 195, 395, 765, 395,
                new Color32(39, 68, 57, 255));
            FillTriangle(pixels, width, height, 480, 518, 203, 402, 757, 402,
                new Color32(239, 239, 220, 255));
            FillRect(pixels, width, height, 245, 139, 715, 390, new Color32(135, 83, 56, 255));

            for (int y = 170; y < 388; y += 38)
                DrawLine(pixels, width, height, 248, y, 712, y, 4, new Color32(104, 61, 43, 180));

            // Window on the left of the door.
            FillRect(pixels, width, height, 286, 196, 450, 354, new Color32(74, 43, 34, 255));
            FillRect(pixels, width, height, 299, 209, 437, 341, new Color32(252, 202, 117, 255));
            FillRect(pixels, width, height, 314, 224, 422, 329, new Color32(255, 226, 164, 255));
            FillRect(pixels, width, height, 358, 209, 368, 341, new Color32(117, 76, 49, 255));
            FillRect(pixels, width, height, 299, 269, 437, 280, new Color32(117, 76, 49, 255));
            FillCircle(pixels, width, height, 328, 247, 25, new Color32(242, 180, 83, 60));

            // Door and brass details.
            FillRect(pixels, width, height, 523, 139, 654, 335, new Color32(67, 54, 43, 255));
            FillRect(pixels, width, height, 536, 153, 641, 324, new Color32(47, 83, 67, 255));
            FillRect(pixels, width, height, 551, 173, 626, 305, new Color32(255, 206, 119, 255));
            FillRect(pixels, width, height, 559, 181, 618, 296, new Color32(245, 224, 177, 255));
            FillRect(pixels, width, height, 584, 181, 593, 296, new Color32(111, 77, 52, 255));
            FillCircle(pixels, width, height, 620, 230, 5, new Color32(230, 182, 91, 255));

            // Shop awning, garland and snow cap.
            FillRect(pixels, width, height, 264, 365, 696, 392, new Color32(133, 45, 45, 255));
            for (int x = 270; x < 696; x += 54)
            {
                FillTriangle(pixels, width, height, x + 24, 365, x, 391, x + 48, 391,
                    new Color32(211, 170, 105, 255));
                FillCircle(pixels, width, height, x + 24, 370, 5, Gold);
            }
            DrawLine(pixels, width, height, 224, 400, 735, 400, 12, new Color32(245, 240, 221, 255));

            // Fir trees and the snow-covered street foreground.
            DrawFirTree(pixels, width, height, 90, 130, 212);
            DrawFirTree(pixels, width, height, 860, 119, 230);
            FillRect(pixels, width, height, 0, 0, width, 115, new Color32(224, 231, 229, 255));
            FillTriangle(pixels, width, height, 510, 155, 340, 0, 710, 0,
                new Color32(238, 241, 234, 255));
            DrawLine(pixels, width, height, 0, 116, width, 116, 5, new Color32(246, 245, 229, 255));

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Заснеженная рождественская улица";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawDistantHouse(Color32[] pixels, int width, int height, int x, int y, int w, int h)
        {
            FillRect(pixels, width, height, x, y, x + w, y + h, new Color32(54, 63, 76, 255));
            FillTriangle(pixels, width, height, x + w / 2, y + h + 62, x - 24, y + h - 8,
                x + w + 24, y + h - 8, new Color32(49, 64, 75, 255));
            FillRect(pixels, width, height, x + 22, y + 50, x + 74, y + 113, new Color32(248, 192, 106, 255));
            FillRect(pixels, width, height, x + 112, y + 50, x + 164, y + 113, new Color32(248, 192, 106, 255));
            FillRect(pixels, width, height, x + 41, y + 50, x + 52, y + 113, new Color32(126, 87, 58, 255));
            FillRect(pixels, width, height, x + 22, y + 78, x + 74, y + 88, new Color32(126, 87, 58, 255));
            FillRect(pixels, width, height, x + 132, y + 50, x + 143, y + 113, new Color32(126, 87, 58, 255));
            FillRect(pixels, width, height, x + 112, y + 78, x + 164, y + 88, new Color32(126, 87, 58, 255));
            DrawLine(pixels, width, height, x - 14, y + h - 6, x + w + 14, y + h - 6,
                8, new Color32(237, 239, 226, 255));
        }

        private static void DrawFirTree(Color32[] pixels, int width, int height, int centerX, int baseY, int treeHeight)
        {
            FillRect(pixels, width, height, centerX - 9, baseY - 28, centerX + 9, baseY + 10,
                new Color32(91, 60, 42, 255));
            FillTriangle(pixels, width, height, centerX, baseY + treeHeight,
                centerX - 76, baseY, centerX + 76, baseY, new Color32(23, 71, 57, 255));
            FillTriangle(pixels, width, height, centerX, baseY + treeHeight - 47,
                centerX - 58, baseY + 48, centerX + 58, baseY + 48, new Color32(34, 94, 68, 255));
            DrawLine(pixels, width, height, centerX - 60, baseY + 50, centerX - 8, baseY + 68,
                6, new Color32(237, 240, 226, 255));
            DrawLine(pixels, width, height, centerX + 8, baseY + 68, centerX + 60, baseY + 50,
                6, new Color32(237, 240, 226, 255));

            for (int i = 0; i < 8; i++)
            {
                int y = baseY + 28 + i * 19;
                int spread = 12 + i * 7;
                Color32 bulb = i % 3 == 0
                    ? new Color32(244, 191, 89, 255)
                    : i % 3 == 1
                        ? new Color32(174, 60, 54, 255)
                        : new Color32(240, 222, 174, 255);
                FillCircle(pixels, width, height, centerX + (i % 2 == 0 ? -spread : spread), y, 4, bulb);
            }
        }

        private static void FillRect(Color32[] pixels, int width, int height,
            int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            xMin = Mathf.Clamp(xMin, 0, width);
            xMax = Mathf.Clamp(xMax, 0, width);
            yMin = Mathf.Clamp(yMin, 0, height);
            yMax = Mathf.Clamp(yMax, 0, height);

            for (int y = yMin; y < yMax; y++)
            {
                int offset = y * width;
                for (int x = xMin; x < xMax; x++)
                    pixels[offset + x] = color;
            }
        }

        private static void FillCircle(Color32[] pixels, int width, int height,
            int centerX, int centerY, int radius, Color32 color)
        {
            int minX = Mathf.Max(0, centerX - radius);
            int maxX = Mathf.Min(width - 1, centerX + radius);
            int minY = Mathf.Max(0, centerY - radius);
            int maxY = Mathf.Min(height - 1, centerY + radius);
            int squaredRadius = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= squaredRadius)
                        pixels[y * width + x] = color;
                }
            }
        }

        private static void FillTriangle(Color32[] pixels, int width, int height,
            int x1, int y1, int x2, int y2, int x3, int y3, Color32 color)
        {
            int minX = Mathf.Clamp(Mathf.Min(x1, Mathf.Min(x2, x3)), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.Max(x1, Mathf.Max(x2, x3)), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.Min(y1, Mathf.Min(y2, y3)), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.Max(y1, Mathf.Max(y2, y3)), 0, height - 1);
            float area = Edge(x1, y1, x2, y2, x3, y3);
            if (Mathf.Abs(area) < 0.001f)
                return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float a = Edge(x2, y2, x3, y3, x, y) / area;
                    float b = Edge(x3, y3, x1, y1, x, y) / area;
                    float c = Edge(x1, y1, x2, y2, x, y) / area;
                    if (a >= 0f && b >= 0f && c >= 0f)
                        pixels[y * width + x] = color;
                }
            }
        }

        private static float Edge(int ax, int ay, int bx, int by, int px, int py)
        {
            return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }

        private static void DrawLine(Color32[] pixels, int width, int height,
            int x1, int y1, int x2, int y2, int thickness, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
            if (steps <= 0)
            {
                FillCircle(pixels, width, height, x1, y1, Mathf.Max(1, thickness / 2), color);
                return;
            }

            int radius = Mathf.Max(1, thickness / 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
                FillCircle(pixels, width, height, x, y, radius, color);
            }
        }
    }
}