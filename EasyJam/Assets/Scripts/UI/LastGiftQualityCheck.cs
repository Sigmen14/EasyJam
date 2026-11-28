using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TheLastGift.Audio;

namespace TheLastGift.UI
{
    /// <summary>
    /// Short, optional finishing check shown once per order. The base order reward is
    /// still awarded by OrderProgressionManager even when the player skips or times out.
    /// </summary>
    public sealed partial class LastGiftUIController
    {
        private const float QualityCheckDuration = 8.5f;

        private enum GiftQualityTask
        {
            Ribbon,
            Postcard,
            Seal
        }

        private readonly List<GiftQualityTask> qualityTaskSequence = new List<GiftQualityTask>();
        private GameObject qualityCheckRoot;
        private GameObject qualityTaskPromptPanel;
        private GameObject qualityTaskStatusPanel;
        private bool qualityCompactLayout;
        private bool qualitySmallScreen;
        private RectTransform qualityTaskTarget;
        private RectTransform qualityTaskItem;
        private LastGiftQualityDragHandle qualityTaskHandle;
        private Image qualityTaskTargetImage;
        private Image qualityTimerBackground;
        private Image qualityTimerFill;
        private Image qualityCelebrationGlow;
        private Text qualityTaskHint;
        private Text qualityTaskDescription;
        private Text qualityTaskStatus;
        private Text qualityTaskProgress;
        private Text qualityTimerLabel;
        private Action<int> qualityCheckCallback;
        private float qualityTimeRemaining;
        private float qualityCelebrationRemaining;
        private int qualityTaskIndex;
        private int qualityCompletedCount;
        private int qualityMistakeCount;
        private int qualityBonusForSession;
        private int lastQualityTask = -1;
        private bool qualityCheckCelebrating;
        private bool qualityCheckQueuedFromPackingMiniGame;
        private bool qualityCheckCompletedForOrder;
        private int qualityCheckBonusCoins;

        /// <summary>
        /// Called by the optional 4x4 packing scene only after its board is complete.
        /// The check is shown when that scene returns to the active order UI.
        /// </summary>
        public static void QueueQualityCheckFromPackingMiniGame()
        {
            if (activeInstance == null || !activeInstance.packingMiniGameOpen)
                return;

            if (activeInstance.IsQualityCheckContextValid()
                && !activeInstance.qualityCheckCompletedForOrder)
                activeInstance.qualityCheckQueuedFromPackingMiniGame = true;
        }

        public static void CancelQueuedQualityCheckFromPackingMiniGame()
        {
            if (activeInstance != null)
                activeInstance.qualityCheckQueuedFromPackingMiniGame = false;
        }

        /// <summary>Starts the quality check after the wrapping choices have been submitted.</summary>
        public void BeginQualityCheckFromWrapping(Action<int> onCompleted)
        {
            StartQualityCheck(onCompleted, false);
        }

        /// <summary>
        /// Returns a previously earned quality bonus from the 4x4 route, so the same order
        /// never asks the player to complete the check twice.
        /// </summary>
        public bool TryConsumeQualityCheckBonus(out int bonusCoins)
        {
            if (!qualityCheckCompletedForOrder)
            {
                bonusCoins = 0;
                return false;
            }

            bonusCoins = qualityCheckBonusCoins;
            qualityCheckCompletedForOrder = false;
            qualityCheckBonusCoins = 0;
            return true;
        }

        private bool IsQualityCheckContextValid()
        {
            return canvas != null
                && root != null
                && root.name == "Игровой экран"
                && currentOrder != null
                && !LastGiftIntroController.IsPlaying;
        }

        private void StartQueuedQualityCheckAfterPuzzle()
        {
            if (!qualityCheckQueuedFromPackingMiniGame)
                return;

            qualityCheckQueuedFromPackingMiniGame = false;
            if (qualityCheckCompletedForOrder || !IsQualityCheckContextValid())
                return;

            // Store the result until the order is submitted. The main reward remains
            // untouched and is still granted by the existing progression manager.
            StartQualityCheck(null, true);
        }

        private void StartQualityCheck(Action<int> onCompleted, bool storeResultForOrder)
        {
            if (!IsQualityCheckContextValid())
            {
                if (storeResultForOrder)
                {
                    qualityCheckCompletedForOrder = true;
                    qualityCheckBonusCoins = 0;
                }

                onCompleted?.Invoke(0);
                return;
            }

            if (qualityCheckRoot != null)
            {
                qualityCheckRoot.SetActive(false);
                Destroy(qualityCheckRoot);
            }

            if (wrappingRoot != null)
            {
                wrappingRoot.SetActive(false);
                Destroy(wrappingRoot);
                wrappingRoot = null;
            }

            qualityCheckCallback = onCompleted;
            qualityTimeRemaining = QualityCheckDuration;
            qualityCelebrationRemaining = 0f;
            qualityTaskIndex = 0;
            qualityCompletedCount = 0;
            qualityMistakeCount = 0;
            qualityBonusForSession = 0;
            qualityCheckCelebrating = false;
            qualityTaskSequence.Clear();
            qualityCompactLayout = Screen.width < Screen.height
                || (float)Screen.width / Mathf.Max(1f, Screen.height) < 1.35f;
            qualitySmallScreen = Screen.width < 700 || Screen.height < 500;
            BuildQualityTaskSequence();

            qualityCheckRoot = new GameObject("Проверка качества подарка", typeof(RectTransform));
            qualityCheckRoot.transform.SetParent(canvas.transform, false);
            qualityCheckRoot.transform.SetAsLastSibling();

            RectTransform rootRect = qualityCheckRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            BuildQualityScreenLayout();
            ShowCurrentQualityTask();
            RefreshQualityTimer();
        }

        /// <summary>Builds a responsive, separated layout for wide and portrait screens.</summary>
        private void BuildQualityScreenLayout()
        {
            Panel(qualityCheckRoot.transform, "Затемнение проверки качества",
                Vector2.zero, Vector2.one, new Color(0.025f, 0.055f, 0.045f, 0.94f));
            FestivePanel(qualityCheckRoot.transform, "Праздничная панель проверки качества",
                new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f), paper);

            Vector2 headerMin = qualityCompactLayout ? new Vector2(0.08f, 0.895f) : new Vector2(0.075f, 0.875f);
            Vector2 headerMax = qualityCompactLayout ? new Vector2(0.92f, 0.955f) : new Vector2(0.72f, 0.955f);
            FestiveHeader(qualityCheckRoot.transform, "Заголовок проверки качества",
                "ПОСЛЕДНИЙ ШТРИХ", headerMin, headerMax, red, qualityCompactLayout ? 23 : 27);

            Vector2 hintMin = qualityCompactLayout ? new Vector2(0.10f, 0.765f) : new Vector2(0.105f, 0.735f);
            Vector2 hintMax = qualityCompactLayout ? new Vector2(0.90f, 0.835f) : new Vector2(0.425f, 0.825f);
            Vector2 descriptionMin = qualityCompactLayout ? new Vector2(0.11f, 0.710f) : new Vector2(0.105f, 0.635f);
            Vector2 descriptionMax = qualityCompactLayout ? new Vector2(0.89f, 0.765f) : new Vector2(0.425f, 0.735f);
            Vector2 progressMin = qualityCompactLayout ? new Vector2(0.12f, 0.675f) : new Vector2(0.105f, 0.575f);
            Vector2 progressMax = qualityCompactLayout ? new Vector2(0.43f, 0.710f) : new Vector2(0.34f, 0.625f);

            qualityTaskPromptPanel = FestivePanel(qualityCheckRoot.transform, "Карточка задания",
                qualityCompactLayout ? new Vector2(0.075f, 0.665f) : new Vector2(0.075f, 0.555f),
                qualityCompactLayout ? new Vector2(0.925f, 0.865f) : new Vector2(0.455f, 0.855f),
                cream).gameObject;

            qualityTaskHint = ChildLabel(qualityCheckRoot.transform, "", qualityCompactLayout ? 20 : 23,
                red, hintMin, hintMax, TextAnchor.MiddleCenter);
            qualityTaskDescription = ChildLabel(qualityCheckRoot.transform, "", 20,
                brown, descriptionMin, descriptionMax, TextAnchor.MiddleCenter);
            qualityTaskProgress = ChildLabel(qualityCheckRoot.transform, "", 20,
                green, progressMin, progressMax, TextAnchor.MiddleCenter);

            Vector2 timerLabelMin = qualityCompactLayout ? new Vector2(0.61f, 0.675f) : new Vector2(0.765f, 0.875f);
            Vector2 timerLabelMax = qualityCompactLayout ? new Vector2(0.89f, 0.710f) : new Vector2(0.90f, 0.945f);
            qualityTimerLabel = ChildLabel(qualityCheckRoot.transform, "", 20, red,
                timerLabelMin, timerLabelMax, TextAnchor.MiddleCenter);

            qualityTaskStatusPanel = FestivePanel(qualityCheckRoot.transform, "Карточка отклика",
                qualityCompactLayout ? new Vector2(0.12f, 0.165f) : new Vector2(0.075f, 0.295f),
                qualityCompactLayout ? new Vector2(0.88f, 0.235f) : new Vector2(0.455f, 0.445f),
                cream).gameObject;
            qualityTaskStatus = ChildLabel(qualityCheckRoot.transform,
                "Подарок почти готов — осталось немного!", qualityCompactLayout ? 20 : 21, brown,
                qualityCompactLayout ? new Vector2(0.15f, 0.175f) : new Vector2(0.105f, 0.325f),
                qualityCompactLayout ? new Vector2(0.85f, 0.225f) : new Vector2(0.425f, 0.415f),
                TextAnchor.MiddleCenter);

            BuildQualityGiftPreview();

            Vector2 timerMin = qualityCompactLayout ? new Vector2(0.12f, 0.115f) : new Vector2(0.12f, 0.135f);
            Vector2 timerMax = qualityCompactLayout ? new Vector2(0.88f, 0.137f) : new Vector2(0.88f, 0.16f);
            qualityTimerBackground = Panel(qualityCheckRoot.transform, "Фон полосы таймера",
                timerMin, timerMax, new Color32(222, 209, 178, 255)).GetComponent<Image>();
            qualityTimerFill = Panel(qualityCheckRoot.transform, "Заполнение таймера",
                timerMin, timerMax, gold).GetComponent<Image>();
            qualityTimerFill.type = Image.Type.Filled;
            qualityTimerFill.fillMethod = Image.FillMethod.Horizontal;
            qualityTimerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            qualityTimerFill.fillAmount = 1f;

            Vector2 skipMin = qualityCompactLayout ? new Vector2(0.34f, 0.045f) : new Vector2(0.73f, 0.055f);
            Vector2 skipMax = qualityCompactLayout ? new Vector2(0.66f, 0.105f) : new Vector2(0.91f, 0.115f);
            ButtonAtOnParent(qualityCheckRoot.transform, "ПРОПУСТИТЬ",
                new Color32(126, 53, 49, 255), BeginQualityCelebration, skipMin, skipMax);
        }

        private void BuildQualityTaskSequence()
        {
            qualityTaskSequence.Clear();
            List<GiftQualityTask> available = new List<GiftQualityTask>
            {
                GiftQualityTask.Ribbon,
                GiftQualityTask.Postcard,
                GiftQualityTask.Seal
            };

            if (lastQualityTask >= 0)
                available.Remove((GiftQualityTask)lastQualityTask);

            // One or two tasks keep this interlude within the intended 5–10 seconds.
            int taskCount = UnityEngine.Random.Range(1, 3);
            if (available.Count < taskCount)
            {
                available.Clear();
                available.Add(GiftQualityTask.Ribbon);
                available.Add(GiftQualityTask.Postcard);
                available.Add(GiftQualityTask.Seal);
            }

            while (qualityTaskSequence.Count < taskCount && available.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, available.Count);
                qualityTaskSequence.Add(available[index]);
                available.RemoveAt(index);
            }

            if (qualityTaskSequence.Count > 0)
                lastQualityTask = (int)qualityTaskSequence[qualityTaskSequence.Count - 1];
        }

        private void BuildQualityGiftPreview()
        {
            FestivePanel(qualityCheckRoot.transform, "Сцена подарка",
                qualityCompactLayout ? new Vector2(0.075f, 0.245f) : new Vector2(0.49f, 0.235f),
                qualityCompactLayout ? new Vector2(0.925f, 0.635f) : new Vector2(0.925f, 0.835f),
                cream);

            Color giftPaper = GetWrappingPaperColor(selectedPaper);
            Color ribbon = GetWrappingRibbonColor(selectedRibbon);

            Vector2 boxMin = qualityCompactLayout ? new Vector2(0.35f, 0.315f) : new Vector2(0.60f, 0.305f);
            Vector2 boxMax = qualityCompactLayout ? new Vector2(0.65f, 0.545f) : new Vector2(0.81f, 0.615f);
            Vector2 lidMin = qualityCompactLayout ? new Vector2(0.32f, 0.535f) : new Vector2(0.58f, 0.595f);
            Vector2 lidMax = qualityCompactLayout ? new Vector2(0.68f, 0.605f) : new Vector2(0.83f, 0.685f);

            Panel(qualityCheckRoot.transform, "Коробка готового подарка", boxMin, boxMax, giftPaper);
            Panel(qualityCheckRoot.transform, "Крышка подарка", lidMin, lidMax, gold);

            Vector2 horizontalMin = qualityCompactLayout ? new Vector2(0.35f, 0.415f) : new Vector2(0.60f, 0.445f);
            Vector2 horizontalMax = qualityCompactLayout ? new Vector2(0.65f, 0.455f) : new Vector2(0.81f, 0.485f);
            Vector2 verticalMin = qualityCompactLayout ? new Vector2(0.485f, 0.315f) : new Vector2(0.685f, 0.305f);
            Vector2 verticalMax = qualityCompactLayout ? new Vector2(0.515f, 0.545f) : new Vector2(0.725f, 0.615f);

            Panel(qualityCheckRoot.transform, "Горизонтальная лента",
                horizontalMin, horizontalMax, ribbon);
            Panel(qualityCheckRoot.transform, "Вертикальная лента",
                verticalMin, verticalMax, ribbon);

            Vector2 bowLeftMin = qualityCompactLayout ? new Vector2(0.465f, 0.585f) : new Vector2(0.675f, 0.665f);
            Vector2 bowLeftMax = qualityCompactLayout ? new Vector2(0.495f, 0.645f) : new Vector2(0.705f, 0.735f);
            Vector2 bowRightMin = qualityCompactLayout ? new Vector2(0.505f, 0.585f) : new Vector2(0.715f, 0.665f);
            Vector2 bowRightMax = qualityCompactLayout ? new Vector2(0.535f, 0.645f) : new Vector2(0.745f, 0.735f);
            RectTransform bowLeft = Panel(qualityCheckRoot.transform, "Лепесток банта слева",
                bowLeftMin, bowLeftMax, ribbon);
            bowLeft.localRotation = Quaternion.Euler(0f, 0f, 35f);
            RectTransform bowRight = Panel(qualityCheckRoot.transform, "Лепесток банта справа",
                bowRightMin, bowRightMax, ribbon);
            bowRight.localRotation = Quaternion.Euler(0f, 0f, -35f);

            Vector2 knotMin = qualityCompactLayout ? new Vector2(0.49f, 0.60f) : new Vector2(0.705f, 0.685f);
            Vector2 knotMax = qualityCompactLayout ? new Vector2(0.51f, 0.63f) : new Vector2(0.725f, 0.715f);
            Panel(qualityCheckRoot.transform, "Узел банта", knotMin, knotMax, gold);

            Vector2 giftLabelMin = qualityCompactLayout ? new Vector2(0.31f, 0.255f) : new Vector2(0.57f, 0.255f);
            Vector2 giftLabelMax = qualityCompactLayout ? new Vector2(0.69f, 0.295f) : new Vector2(0.84f, 0.295f);
            ChildLabel(qualityCheckRoot.transform, "РОЖДЕСТВЕНСКИЙ ПОДАРОК", 20, brown,
                giftLabelMin, giftLabelMax, TextAnchor.MiddleCenter);
        }

        private void ShowCurrentQualityTask()
        {
            if (qualityCheckRoot == null || qualityTaskIndex >= qualityTaskSequence.Count)
                return;

            if (qualityTaskItem != null)
                Destroy(qualityTaskItem.gameObject);
            if (qualityTaskTarget != null
                && (qualityTaskItem == null || qualityTaskTarget.gameObject != qualityTaskItem.gameObject))
                Destroy(qualityTaskTarget.gameObject);

            qualityTaskItem = null;
            qualityTaskTarget = null;
            qualityTaskHandle = null;
            qualityTaskTargetImage = null;

            GiftQualityTask task = qualityTaskSequence[qualityTaskIndex];
            qualityTaskProgress.text = "ШАГ " + (qualityTaskIndex + 1) + " ИЗ " + qualityTaskSequence.Count;

            switch (task)
            {
                case GiftQualityTask.Ribbon:
                {
                    qualityTaskHint.text = "ПОПРАВЬ ЛЕНТУ";
                    qualityTaskDescription.text = "Перетащи ленту в золотую отметку на подарке.";
                    Vector2 targetMin = qualityCompactLayout
                        ? (qualitySmallScreen ? new Vector2(0.43f, 0.315f) : new Vector2(0.485f, 0.315f))
                        : (qualitySmallScreen ? new Vector2(0.65f, 0.305f) : new Vector2(0.685f, 0.305f));
                    Vector2 targetMax = qualityCompactLayout
                        ? (qualitySmallScreen ? new Vector2(0.57f, 0.545f) : new Vector2(0.515f, 0.545f))
                        : (qualitySmallScreen ? new Vector2(0.76f, 0.615f) : new Vector2(0.725f, 0.615f));
                    qualityTaskTarget = Panel(qualityCheckRoot.transform, "Цель для ленты",
                        targetMin, targetMax, new Color(1f, 0.82f, 0.35f, 0.40f));
                    qualityTaskTargetImage = qualityTaskTarget.GetComponent<Image>();

                    qualityTaskHandle = CreateQualityDragItem("Лента для подарка", "ЛЕНТА",
                        GiftQualityTask.Ribbon,
                        qualityCompactLayout ? new Vector2(0.22f, 0.43f) : new Vector2(0.525f, 0.46f),
                        qualityCompactLayout ? new Vector2(54f, 180f) : new Vector2(58f, 250f),
                        GetWrappingRibbonColor(selectedRibbon));
                    qualityTaskItem = qualityTaskHandle.transform as RectTransform;
                    break;
                }

                case GiftQualityTask.Postcard:
                {
                    qualityTaskHint.text = "ПРИКРЕПИ ОТКРЫТКУ";
                    qualityTaskDescription.text = "Перетащи открытку на золотую отметку у крышки.";
                    Vector2 targetMin = qualityCompactLayout
                        ? new Vector2(0.60f, 0.54f) : new Vector2(0.79f, 0.625f);
                    Vector2 targetMax = qualityCompactLayout
                        ? new Vector2(0.73f, 0.62f) : new Vector2(0.875f, 0.735f);
                    qualityTaskTarget = Panel(qualityCheckRoot.transform, "Место для открытки",
                        targetMin, targetMax, new Color(1f, 0.91f, 0.70f, 0.60f));
                    qualityTaskTargetImage = qualityTaskTarget.GetComponent<Image>();

                    qualityTaskHandle = CreateQualityDragItem("Открытка для подарка", "С ТЕПЛОМ",
                        GiftQualityTask.Postcard,
                        qualityCompactLayout ? new Vector2(0.22f, 0.32f) : new Vector2(0.535f, 0.34f),
                        qualityCompactLayout ? new Vector2(142f, 82f) : new Vector2(170f, 100f),
                        new Color32(236, 218, 180, 255));
                    qualityTaskItem = qualityTaskHandle.transform as RectTransform;
                    break;
                }

                case GiftQualityTask.Seal:
                {
                    qualityTaskHint.text = "ЗАКЛЕЙ УПАКОВКУ";
                    qualityTaskDescription.text = "Нажми на золотую отметку или проведи по ней.";
                    Vector2 targetMin = qualityCompactLayout
                        ? (qualitySmallScreen ? new Vector2(0.43f, 0.405f) : new Vector2(0.44f, 0.415f))
                        : new Vector2(0.65f, qualitySmallScreen ? 0.435f : 0.445f);
                    Vector2 targetMax = qualityCompactLayout
                        ? (qualitySmallScreen ? new Vector2(0.57f, 0.475f) : new Vector2(0.56f, 0.455f))
                        : new Vector2(0.76f, qualitySmallScreen ? 0.505f : 0.485f);
                    qualityTaskTarget = Panel(qualityCheckRoot.transform, "Отмеченный участок для скотча",
                        targetMin, targetMax, new Color(1f, 0.78f, 0.28f, 0.80f));
                    qualityTaskTargetImage = qualityTaskTarget.GetComponent<Image>();
                    qualityTaskTargetImage.raycastTarget = true;
                    qualityTaskHandle = qualityTaskTarget.gameObject.AddComponent<LastGiftQualityDragHandle>();
                    qualityTaskHandle.Initialize(this, GiftQualityTask.Ribbon, true);
                    qualityTaskItem = qualityTaskTarget;
                    ChildLabel(qualityTaskTarget, "СКОТЧ", 18, brown,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter).raycastTarget = false;
                    break;
                }
            }

            qualityTaskStatus.text = "Подарок почти готов. Промах можно исправить!";
        }

        private LastGiftQualityDragHandle CreateQualityDragItem(
            string objectName, string caption, GiftQualityTask task, Vector2 anchor, Vector2 size, Color color)
        {
            GameObject item = new GameObject(objectName,
                typeof(RectTransform), typeof(Image), typeof(LastGiftQualityDragHandle));
            item.transform.SetParent(qualityCheckRoot.transform, false);

            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            float canvasScale = canvas != null ? Mathf.Max(0.1f, canvas.scaleFactor) : 1f;
            float minimumTouchSize = 52f / canvasScale;
            size.x = Mathf.Max(size.x, minimumTouchSize);
            size.y = Mathf.Max(size.y, minimumTouchSize);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Color captionColor = task == GiftQualityTask.Postcard ? brown : paper;
            ChildLabel(item.transform, caption, 20, captionColor,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter).raycastTarget = false;

            LastGiftQualityDragHandle handle = item.GetComponent<LastGiftQualityDragHandle>();
            handle.Initialize(this, task, false);
            return handle;
        }

        private void LateUpdate()
        {
            if (qualityCheckRoot == null || !qualityCheckRoot.activeInHierarchy || Time.timeScale <= 0f)
                return;

            if (qualityCheckCelebrating)
            {
                qualityCelebrationRemaining -= Time.deltaTime;
                if (qualityCelebrationGlow != null)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.035f;
                    qualityCelebrationGlow.rectTransform.localScale = Vector3.one * pulse;
                    Color glowColor = qualityCelebrationGlow.color;
                    glowColor.a = 0.20f + (Mathf.Sin(Time.time * 5f) + 1f) * 0.08f;
                    qualityCelebrationGlow.color = glowColor;
                }

                if (qualityCelebrationRemaining <= 0f)
                    FinishQualityCheckPresentation();
                return;
            }

            qualityTimeRemaining -= Time.deltaTime;
            RefreshQualityTimer();
            if (qualityTimeRemaining <= 0f)
                BeginQualityCelebration();
        }

        private void RefreshQualityTimer()
        {
            if (qualityTimerLabel != null)
                qualityTimerLabel.text = Mathf.CeilToInt(Mathf.Max(0f, qualityTimeRemaining)).ToString("00") + " СЕК";
            if (qualityTimerFill != null)
                qualityTimerFill.fillAmount = Mathf.Clamp01(qualityTimeRemaining / QualityCheckDuration);
        }

        /// <summary>Called by the UI drag handle for mouse and touch input.</summary>
        public void HandleQualityDragEnd(LastGiftQualityDragHandle handle, PointerEventData eventData)
        {
            if (qualityCheckCelebrating || handle == null || handle != qualityTaskHandle
                || qualityTaskTarget == null || qualityCheckRoot == null)
                return;

            bool insideTarget = RectTransformUtility.RectangleContainsScreenPoint(
                qualityTaskTarget, eventData.position, eventData.pressEventCamera);
            if (!insideTarget)
            {
                RegisterQualityMistake(handle);
                return;
            }

            StartCoroutine(AnimateQualitySnap(handle.transform as RectTransform));
        }

        /// <summary>Called for a tap or swipe on the marked tape area.</summary>
        public void HandleQualitySealInteraction(LastGiftQualityDragHandle handle, PointerEventData eventData)
        {
            if (qualityCheckCelebrating || handle == null || handle != qualityTaskHandle
                || qualityTaskTarget == null || qualityCheckRoot == null)
                return;

            bool insideTarget = RectTransformUtility.RectangleContainsScreenPoint(
                qualityTaskTarget, eventData.position, eventData.pressEventCamera);
            if (insideTarget)
                CompleteCurrentQualityTask();
            else
                RegisterQualityMistake(handle);
        }

        private void RegisterQualityMistake(LastGiftQualityDragHandle handle)
        {
            qualityMistakeCount++;
            handle.ReturnToStartPosition();
            qualityTaskStatus.text = "ЧУТЬ МИМО — попробуй ещё раз!";
            LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.Error);
        }

        private IEnumerator AnimateQualitySnap(RectTransform draggedRect)
        {
            if (draggedRect == null || qualityTaskTarget == null)
                yield break;

            Vector3 start = draggedRect.position;
            Vector3 end = qualityTaskTarget.position;
            float elapsed = 0f;
            const float duration = 0.18f;

            while (elapsed < duration && draggedRect != null && qualityTaskTarget != null)
            {
                elapsed += Time.deltaTime;
                draggedRect.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (draggedRect != null)
                draggedRect.position = end;
            CompleteCurrentQualityTask();
        }

        private void CompleteCurrentQualityTask()
        {
            if (qualityCheckCelebrating || qualityCheckRoot == null)
                return;

            qualityCompletedCount++;
            qualityTaskStatus.text = "ОТЛИЧНО! Последний штрих на месте.";
            LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.GiftPacked);

            if (qualityTaskTargetImage != null)
            {
                Color completeColor = qualityTaskTargetImage.color;
                completeColor.a = 1f;
                qualityTaskTargetImage.color = completeColor;
            }

            GiftQualityTask completedTask = qualityTaskSequence[qualityTaskIndex];
            if (completedTask == GiftQualityTask.Postcard)
            {
                // Keep the postcard snapped to the gift as a permanent finishing detail.
                if (qualityTaskTarget != null)
                    Destroy(qualityTaskTarget.gameObject);
                qualityTaskTarget = null;
                qualityTaskItem = null;
            }
            else if (completedTask == GiftQualityTask.Seal)
            {
                // The highlighted tape area becomes part of the finished package.
                qualityTaskTarget = null;
                qualityTaskItem = null;
            }
            else if (qualityTaskItem != null)
            {
                qualityTaskItem.gameObject.SetActive(false);
            }

            qualityTaskHandle = null;

            qualityTaskIndex++;
            if (qualityTaskIndex >= qualityTaskSequence.Count)
            {
                BeginQualityCelebration();
                return;
            }

            StartCoroutine(ShowNextQualityTask());
        }

        private IEnumerator ShowNextQualityTask()
        {
            yield return new WaitForSeconds(0.22f);
            if (qualityCheckRoot != null && !qualityCheckCelebrating)
                ShowCurrentQualityTask();
        }

        private void BeginQualityCelebration()
        {
            if (qualityCheckRoot == null || qualityCheckCelebrating)
                return;

            qualityCheckCelebrating = true;
            qualityCelebrationRemaining = 1.15f;
            qualityBonusForSession = qualityCompletedCount > 0 ? 2 : 0;

            bool perfect = qualityCompletedCount == qualityTaskSequence.Count
                && qualityMistakeCount == 0
                && qualityTimeRemaining > 0f;
            if (perfect)
                qualityBonusForSession += 2;

            if (qualityTaskItem != null)
                qualityTaskItem.gameObject.SetActive(false);
            if (qualityTaskPromptPanel != null)
                qualityTaskPromptPanel.SetActive(false);
            if (qualityTaskStatusPanel != null)
                qualityTaskStatusPanel.SetActive(false);
            if (qualityTaskHint != null) qualityTaskHint.gameObject.SetActive(false);
            if (qualityTaskDescription != null) qualityTaskDescription.gameObject.SetActive(false);
            if (qualityTaskProgress != null) qualityTaskProgress.gameObject.SetActive(false);
            if (qualityTimerLabel != null) qualityTimerLabel.gameObject.SetActive(false);
            if (qualityTimerFill != null) qualityTimerFill.gameObject.SetActive(false);
            if (qualityTimerBackground != null) qualityTimerBackground.gameObject.SetActive(false);
            foreach (Button previousButton in qualityCheckRoot.GetComponentsInChildren<Button>(true))
                previousButton.gameObject.SetActive(false);
            if (qualityTaskTarget != null && qualityTaskTargetImage != null)
            {
                Color finalColor = qualityTaskTargetImage.color;
                finalColor.a = 1f;
                qualityTaskTargetImage.color = finalColor;
            }

            LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.OrderComplete);
            qualityCelebrationGlow = Panel(qualityCheckRoot.transform, "Золотое сияние готового подарка",
                qualityCompactLayout ? new Vector2(0.12f, 0.34f) : new Vector2(0.25f, 0.36f),
                qualityCompactLayout ? new Vector2(0.88f, 0.64f) : new Vector2(0.77f, 0.70f),
                new Color(1f, 0.82f, 0.34f, 0.30f)).GetComponent<Image>();

            string title = perfect ? "ОТЛИЧНАЯ РАБОТА!" : "ПОДАРОК ГОТОВ!";
            ChildLabel(qualityCheckRoot.transform, title, 30, red,
                new Vector2(0.20f, 0.49f), new Vector2(0.80f, 0.62f), TextAnchor.MiddleCenter);
            string reward = qualityBonusForSession > 0
                ? "Аккуратность: +" + qualityBonusForSession + " монеты ✦"
                : "Основная награда за заказ сохранена.";
            ChildLabel(qualityCheckRoot.transform, reward, 21, brown,
                new Vector2(0.20f, 0.405f), new Vector2(0.80f, 0.49f), TextAnchor.MiddleCenter);

            ButtonAtOnParent(qualityCheckRoot.transform, "ПРОДОЛЖИТЬ", green,
                FinishQualityCheckPresentation,
                qualityCompactLayout ? new Vector2(0.31f, 0.22f) : new Vector2(0.34f, 0.245f),
                qualityCompactLayout ? new Vector2(0.69f, 0.30f) : new Vector2(0.66f, 0.325f));
        }

        private void FinishQualityCheckPresentation()
        {
            if (qualityCheckRoot == null)
                return;

            int bonus = qualityBonusForSession;
            Action<int> callback = qualityCheckCallback;
            qualityCheckCallback = null;

            qualityCheckRoot.SetActive(false);
            Destroy(qualityCheckRoot);
            qualityCheckRoot = null;
            qualityTaskTarget = null;
            qualityTaskItem = null;
            qualityTaskHandle = null;
            qualityTimerFill = null;
            qualityCelebrationGlow = null;
            qualityCheckCelebrating = false;

            if (callback == null)
            {
                qualityCheckCompletedForOrder = true;
                qualityCheckBonusCoins = bonus;
            }
            else
            {
                qualityCheckCompletedForOrder = false;
                qualityCheckBonusCoins = 0;
                callback.Invoke(bonus);
            }
        }

        private void ResetQualityCheckForNewOrder()
        {
            qualityCheckQueuedFromPackingMiniGame = false;
            qualityCheckCompletedForOrder = false;
            qualityCheckBonusCoins = 0;
            qualityCheckCallback = null;
            qualityCheckCelebrating = false;

            if (qualityCheckRoot != null)
            {
                qualityCheckRoot.SetActive(false);
                Destroy(qualityCheckRoot);
                qualityCheckRoot = null;
            }
        }

        private void CancelQualityCheckForMainMenu()
        {
            ResetQualityCheckForNewOrder();
        }
    }
}
