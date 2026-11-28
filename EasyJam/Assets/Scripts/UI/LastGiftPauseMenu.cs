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
    /// Pause and display-settings presentation for the existing LastGiftUIController.
    /// This is a partial file, not a second UI or pause manager.
    /// </summary>
    public sealed partial class LastGiftUIController
    {
        private const string FullscreenPreferenceKey = "LastGift.Display.Fullscreen";
        private const string ResolutionWidthPreferenceKey = "LastGift.Display.Width";
        private const string ResolutionHeightPreferenceKey = "LastGift.Display.Height";

        private enum PauseView
        {
            None,
            Menu,
            Settings,
            ConfirmExit
        }

        private PauseView pauseView;
        private GameObject pauseOverlayRoot;
        private GameObject pauseContentRoot;
        private float timeScaleBeforePause = 1f;
        private bool canvasWasEnabledBeforePause;
        private bool rootWasActiveBeforePause;
        private bool wrappingWasActiveBeforePause;

        private readonly List<Resolution> supportedDisplayResolutions = new List<Resolution>();
        private int selectedDisplayResolutionIndex;
        private bool fullscreenEnabled;
        private Text resolutionValueLabel;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            switch (pauseView)
            {
                case PauseView.Menu:
                    ResumeFromPause();
                    return;

                case PauseView.Settings:
                case PauseView.ConfirmExit:
                    ShowPauseMenu();
                    return;

                case PauseView.None:
                    if (CanOpenPauseMenu())
                        OpenPauseMenu();
                    return;
            }
        }

        private bool CanOpenPauseMenu()
        {
            if (root == null || canvas == null || Time.timeScale <= 0f || introTransitionPending)
                return false;

            if (LastGiftIntroController.IsPlaying)
                return false;

            // These are playable campaign screens. Main menu, tutorial, story dialogue
            // and the final ending are deliberately excluded.
            string screenName = root.name;
            return screenName == "Игровой экран"
                || screenName == "Тайное желание покупателя"
                || screenName == "Угаданное желание"
                || screenName == "Ответ на тайное желание"
                || screenName == "Перепутанные подарки"
                || screenName == "Происшествие завершено"
                || screenName == "Заказ выполнен"
                || screenName == "Конец дня"
                || screenName == "Улучшения мастерской";
        }

        private void OpenPauseMenu()
        {
            if (pauseView != PauseView.None || !CanOpenPauseMenu())
                return;

            timeScaleBeforePause = Time.timeScale;
            canvasWasEnabledBeforePause = canvas.enabled;
            rootWasActiveBeforePause = root != null && root.activeSelf;
            wrappingWasActiveBeforePause = wrappingRoot != null && wrappingRoot.activeSelf;

            // Keep the music source running; pausing Unity time is enough to stop
            // gameplay timers, physics, animations and scaled coroutines.
            Time.timeScale = 0f;

            // The shop canvas is normally disabled while the packing scene is open.
            // Re-enable it only for the modal pause overlay, hiding its previous UI.
            canvas.enabled = true;
            if (root != null)
                root.SetActive(false);
            if (wrappingRoot != null)
                wrappingRoot.SetActive(false);

            pauseOverlayRoot = new GameObject("Меню паузы The Last Gift",
                typeof(RectTransform), typeof(Image));
            pauseOverlayRoot.transform.SetParent(canvas.transform, false);
            pauseOverlayRoot.transform.SetAsLastSibling();

            RectTransform overlayRect = pauseOverlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image backdrop = pauseOverlayRoot.GetComponent<Image>();
            backdrop.color = new Color(0.055f, 0.075f, 0.065f, 0.78f);
            backdrop.raycastTarget = true;

            ShowPauseMenu();
        }

        private void ShowPauseMenu()
        {
            if (pauseOverlayRoot == null)
                return;

            pauseView = PauseView.Menu;
            CreatePauseContent();

            RectTransform card = FestivePanel(pauseContentRoot.transform,
                "Праздничная карточка паузы",
                new Vector2(0.22f, 0.15f), new Vector2(0.78f, 0.85f), paper);

            FestiveHeader(card, "Золотая лента паузы", "ПАУЗА",
                new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.96f), green, 34);

            // Equal button bounds and spacing. Settings is above "ВЫЙТИ В МЕНЮ".
            ButtonAtOnParent(card, "ПРОДОЛЖИТЬ", green, ResumeFromPause,
                new Vector2(0.16f, 0.58f), new Vector2(0.84f, 0.73f));
            ButtonAtOnParent(card, "НАСТРОЙКИ", wood, ShowPauseSettings,
                new Vector2(0.16f, 0.385f), new Vector2(0.84f, 0.535f));
            ButtonAtOnParent(card, "ВЫЙТИ В МЕНЮ", red, ShowPauseExitConfirmation,
                new Vector2(0.16f, 0.19f), new Vector2(0.84f, 0.34f));
        }

        private void ShowPauseExitConfirmation()
        {
            if (pauseOverlayRoot == null)
                return;

            pauseView = PauseView.ConfirmExit;
            CreatePauseContent();

            RectTransform card = FestivePanel(pauseContentRoot.transform,
                "Подтверждение выхода в меню",
                new Vector2(0.22f, 0.25f), new Vector2(0.78f, 0.75f), paper);

            FestiveHeader(card, "Лента подтверждения", "ВЫЙТИ В МЕНЮ?",
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.94f), red, 30);

            ChildLabel(card,
                "Текущая попытка будет завершена. Ты сможешь начать новую игру из главного меню.",
                18, brown, new Vector2(0.12f, 0.43f), new Vector2(0.88f, 0.69f),
                TextAnchor.MiddleCenter);

            ButtonAtOnParent(card, "ОСТАТЬСЯ", green, ShowPauseMenu,
                new Vector2(0.10f, 0.17f), new Vector2(0.47f, 0.34f));
            ButtonAtOnParent(card, "ВЫЙТИ В МЕНЮ", red, ConfirmReturnToMainMenu,
                new Vector2(0.53f, 0.17f), new Vector2(0.90f, 0.34f));
        }

        private void ShowPauseSettings()
        {
            if (pauseOverlayRoot == null)
                return;

            pauseView = PauseView.Settings;
            CreatePauseContent();
            BuildSettingsPanel(pauseContentRoot.transform, ShowPauseMenu, "НАЗАД");
        }

        private void CreatePauseContent()
        {
            if (pauseContentRoot != null)
                Destroy(pauseContentRoot);

            pauseContentRoot = new GameObject("Содержимое паузы", typeof(RectTransform));
            pauseContentRoot.transform.SetParent(pauseOverlayRoot.transform, false);

            RectTransform rect = pauseContentRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ResumeFromPause()
        {
            if (pauseView == PauseView.None)
                return;

            if (pauseOverlayRoot != null)
            {
                pauseOverlayRoot.SetActive(false);
                Destroy(pauseOverlayRoot);
            }

            pauseOverlayRoot = null;
            pauseContentRoot = null;
            pauseView = PauseView.None;

            if (canvas != null)
                canvas.enabled = canvasWasEnabledBeforePause;
            if (root != null)
                root.SetActive(rootWasActiveBeforePause);
            if (wrappingRoot != null)
                wrappingRoot.SetActive(wrappingWasActiveBeforePause);

            Time.timeScale = timeScaleBeforePause;
            timeScaleBeforePause = 1f;
        }

        private void ConfirmReturnToMainMenu()
        {
            if (pauseView == PauseView.None)
                return;

            if (pauseOverlayRoot != null)
            {
                pauseOverlayRoot.SetActive(false);
                Destroy(pauseOverlayRoot);
            }

            pauseOverlayRoot = null;
            pauseContentRoot = null;
            pauseView = PauseView.None;

            // A menu transition must never inherit a zero time scale.
            Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
            timeScaleBeforePause = 1f;

            if (canvas != null)
                canvas.enabled = true;
            if (root != null)
                root.SetActive(true);
            if (wrappingRoot != null)
                wrappingRoot.SetActive(true);

            LeaveGameplayToMenu();
        }

        private void LeaveGameplayToMenu()
        {
            if (packingMiniGameOpen)
            {
                if (canvas != null)
                    canvas.enabled = true;
                if (root != null)
                    root.SetActive(true);
                if (wrappingRoot != null)
                    wrappingRoot.SetActive(false);

                StartCoroutine(UnloadMiniGameBeforeReturningToMenu());
                return;
            }

            FinishReturningToMainMenu();
        }

        private IEnumerator UnloadMiniGameBeforeReturningToMenu()
        {
            yield return UnloadPackingMiniGame();
            FinishReturningToMainMenu();
        }

        private void FinishReturningToMainMenu()
        {
            if (canvas != null)
                canvas.enabled = true;
            if (root != null)
                root.SetActive(true);

            if (wrappingRoot != null)
            {
                Destroy(wrappingRoot);
                wrappingRoot = null;
            }

            if (Time.timeScale <= 0f)
                Time.timeScale = 1f;

            if (LeaveGameplay != null)
                LeaveGameplay.Invoke();
            else
            {
                ResetCampaignState();
                MainMenu();
            }
        }

        private void RestorePausedStateOnDestroy()
        {
            if (pauseView == PauseView.None)
                return;

            if (pauseOverlayRoot != null)
            {
                pauseOverlayRoot.SetActive(false);
                Destroy(pauseOverlayRoot);
            }

            pauseOverlayRoot = null;
            pauseContentRoot = null;
            pauseView = PauseView.None;

            if (canvas != null)
                canvas.enabled = canvasWasEnabledBeforePause;
            if (root != null)
                root.SetActive(rootWasActiveBeforePause);
            if (wrappingRoot != null)
                wrappingRoot.SetActive(wrappingWasActiveBeforePause);

            Time.timeScale = timeScaleBeforePause;
            timeScaleBeforePause = 1f;
        }

        /// <summary>Clears local run presentation when the campaign is returned to the menu.</summary>
        public void ResetCampaignState()
        {
            day = 1;
            coins = 0;
            reputation = 0;
            order = 1;
            currentOrder = null;
            placedGiftCount = 0;
            placedGiftNames.Clear();
            secretWishAnswered = false;
            introTransitionPending = false;
            workshopGarland = false;
            workshopExtraShelf = false;
            workshopPremiumWrapping = false;
            wrappingSubmitted = false;

            if (wrappingRoot != null)
            {
                Destroy(wrappingRoot);
                wrappingRoot = null;
            }

            if (sortingEventTimerRoutine != null)
            {
                StopCoroutine(sortingEventTimerRoutine);
                sortingEventTimerRoutine = null;
            }

            sortingEventCompleted = null;
            sortingEventPlacedCount = 0;
            sortingEventSecondsRemaining = 0;
            sortingEventSolved = false;
            for (int i = 0; i < sortingEventPlaced.Length; i++)
            {
                sortingEventPlaced[i] = false;
                sortingEventTargets[i] = null;
                sortingEventCards[i] = null;
            }

            lastResultEarned = 0;
            lastResultWrappingBonus = 0;
            lastResultReputationBonus = 0;
            lastResultCanOpenWorkshop = false;
        }

        private void BuildSettingsPanel(Transform parent, Action backAction, string backCaption)
        {
            LastGiftAudioManager.EnsureInstance();

            RectTransform card = FestivePanel(parent, "Праздничная панель настроек",
                new Vector2(0.13f, 0.075f), new Vector2(0.87f, 0.935f), paper);

            FestiveHeader(card, "Лента настроек", "НАСТРОЙКИ",
                new Vector2(0.08f, 0.855f), new Vector2(0.92f, 0.98f), red, 30);

            CreateVolumeControl(card, "ОБЩАЯ ГРОМКОСТЬ",
                LastGiftAudioManager.MasterVolume, 0.72f, LastGiftAudioManager.SetMasterVolume);
            CreateVolumeControl(card, "МУЗЫКА",
                LastGiftAudioManager.MusicVolume, 0.57f, LastGiftAudioManager.SetMusicVolume);
            CreateVolumeControl(card, "ЗВУКОВЫЕ ЭФФЕКТЫ",
                LastGiftAudioManager.SfxVolume, 0.42f, LastGiftAudioManager.SetSfxVolume);

            CreateFullscreenControl(card);
            CreateResolutionControl(card);

            ButtonAtOnParent(card, backCaption, wood, backAction,
                new Vector2(0.36f, 0.015f), new Vector2(0.64f, 0.095f));
        }

        private void InitializeDisplaySettings()
        {
            supportedDisplayResolutions.Clear();
            Resolution[] available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < supportedDisplayResolutions.Count; j++)
                {
                    if (supportedDisplayResolutions[j].width == available[i].width
                        && supportedDisplayResolutions[j].height == available[i].height)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    supportedDisplayResolutions.Add(available[i]);
            }

            if (supportedDisplayResolutions.Count == 0)
                supportedDisplayResolutions.Add(Screen.currentResolution);

            int savedWidth = PlayerPrefs.GetInt(ResolutionWidthPreferenceKey, Screen.width);
            int savedHeight = PlayerPrefs.GetInt(ResolutionHeightPreferenceKey, Screen.height);
            selectedDisplayResolutionIndex = FindDisplayResolution(savedWidth, savedHeight);
            fullscreenEnabled = PlayerPrefs.GetInt(
                FullscreenPreferenceKey, Screen.fullScreen ? 1 : 0) == 1;

            if (PlayerPrefs.HasKey(ResolutionWidthPreferenceKey)
                || PlayerPrefs.HasKey(ResolutionHeightPreferenceKey)
                || PlayerPrefs.HasKey(FullscreenPreferenceKey))
            {
                ApplyDisplaySettings();
            }
        }

        private int FindDisplayResolution(int width, int height)
        {
            for (int i = 0; i < supportedDisplayResolutions.Count; i++)
            {
                if (supportedDisplayResolutions[i].width == width
                    && supportedDisplayResolutions[i].height == height)
                    return i;
            }

            return 0;
        }

        private void CreateFullscreenControl(Transform parent)
        {
            ChildLabel(parent, "ПОЛНОЭКРАННЫЙ РЕЖИМ", 18, brown,
                new Vector2(0.14f, 0.225f), new Vector2(0.70f, 0.315f),
                TextAnchor.MiddleLeft);

            GameObject toggleObject = new GameObject("Переключатель полноэкранного режима",
                typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);

            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            // Put the toggle directly to the left of its label.
            toggleRect.anchorMin = new Vector2(0.055f, 0.235f);
            toggleRect.anchorMax = new Vector2(0.135f, 0.32f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;

            GameObject backgroundObject = new GameObject("Фон переключателя",
                typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(toggleObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image background = backgroundObject.GetComponent<Image>();
            background.color = cream;

            GameObject checkObject = new GameObject("Отметка",
                typeof(RectTransform), typeof(Image));
            checkObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.18f, 0.18f);
            checkRect.anchorMax = new Vector2(0.82f, 0.82f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image check = checkObject.GetComponent<Image>();
            check.color = green;
            check.raycastTarget = false;

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = fullscreenEnabled;
            toggle.onValueChanged.AddListener(value =>
            {
                fullscreenEnabled = value;
                ApplyDisplaySettings();
            });
            AddGoldOutline(backgroundObject, 1.5f);
        }

        private void CreateResolutionControl(Transform parent)
        {
            ChildLabel(parent, "РАЗРЕШЕНИЕ ЭКРАНА", 18, brown,
                new Vector2(0.14f, 0.115f), new Vector2(0.46f, 0.205f),
                TextAnchor.MiddleLeft);

            resolutionValueLabel = ChildLabel(parent, string.Empty, 17, brown,
                new Vector2(0.56f, 0.115f), new Vector2(0.82f, 0.205f),
                TextAnchor.MiddleCenter);

            if (supportedDisplayResolutions.Count > 1)
            {
                ButtonAtOnParent(parent, "‹", gold, () => ChangeDisplayResolution(-1),
                    new Vector2(0.47f, 0.12f), new Vector2(0.55f, 0.20f));
                ButtonAtOnParent(parent, "›", gold, () => ChangeDisplayResolution(1),
                    new Vector2(0.83f, 0.12f), new Vector2(0.91f, 0.20f));
            }

            UpdateResolutionLabel();
        }

        private void ChangeDisplayResolution(int direction)
        {
            if (supportedDisplayResolutions.Count <= 1)
                return;

            selectedDisplayResolutionIndex += direction;
            if (selectedDisplayResolutionIndex < 0)
                selectedDisplayResolutionIndex = supportedDisplayResolutions.Count - 1;
            else if (selectedDisplayResolutionIndex >= supportedDisplayResolutions.Count)
                selectedDisplayResolutionIndex = 0;

            ApplyDisplaySettings();
            UpdateResolutionLabel();
        }

        private void ApplyDisplaySettings()
        {
            if (supportedDisplayResolutions.Count == 0)
                return;

            selectedDisplayResolutionIndex = Mathf.Clamp(
                selectedDisplayResolutionIndex, 0, supportedDisplayResolutions.Count - 1);

            Resolution selected = supportedDisplayResolutions[selectedDisplayResolutionIndex];
            FullScreenMode mode = fullscreenEnabled
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            Screen.SetResolution(selected.width, selected.height, mode);

            PlayerPrefs.SetInt(FullscreenPreferenceKey, fullscreenEnabled ? 1 : 0);
            PlayerPrefs.SetInt(ResolutionWidthPreferenceKey, selected.width);
            PlayerPrefs.SetInt(ResolutionHeightPreferenceKey, selected.height);
            PlayerPrefs.Save();
        }

        private void UpdateResolutionLabel()
        {
            if (resolutionValueLabel == null || supportedDisplayResolutions.Count == 0)
                return;

            Resolution selected = supportedDisplayResolutions[
                Mathf.Clamp(selectedDisplayResolutionIndex, 0, supportedDisplayResolutions.Count - 1)];
            resolutionValueLabel.text = selected.width + " × " + selected.height;
        }
    }
}
