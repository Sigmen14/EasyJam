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
        private RectTransform qualityTaskTarget;
        private RectTransform qualityTaskItem;
        private LastGiftQualityDragHandle qualityTaskHandle;
        private Image qualityTaskTargetImage;
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
            BuildQualityTaskSequence();

            qualityCheckRoot = new GameObject("Проверка качества подарка", typeof(RectTransform));
            qualityCheckRoot.transform.SetParent(canvas.transform, false);
            qualityCheckRoot.transform.SetAsLastSibling();

            RectTransform rootRect = qualityCheckRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Panel(qualityCheckRoot.transform, "Затемнение проверки качества",
                Vector2.zero, Vector2.one, new Color(0.025f, 0.055f, 0.045f, 0.94f));
            FestivePanel(qualityCheckRoot.transform, "Праздничная панель проверки качества",
                new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f), paper);
            FestiveHeader(qualityCheckRoot.transform, "Заголовок проверки качества",
                "ПОСЛЕДНИЙ ШТРИХ", new Vector2(0.10f, 0.865f), new Vector2(0.90f, 0.94f), red, 27);

            qualityTaskHint = ChildLabel(qualityCheckRoot.transform, "", 23, brown,
                new Vector2(0.085f, 0.735f), new Vector2(0.59f, 0.835f), TextAnchor.MiddleCenter);
            qualityTaskDescription = ChildLabel(qualityCheckRoot.transform, "", 17, brown,
                new Vector2(0.085f, 0.66f), new Vector2(0.59f, 0.735f), TextAnchor.MiddleCenter);
            qualityTaskProgress = ChildLabel(qualityCheckRoot.transform, "", 15, green,
                new Vector2(0.10f, 0.585f), new Vector2(0.36f, 0.635f), TextAnchor.MiddleCenter);
            qualityTaskStatus = ChildLabel(qualityCheckRoot.transform,
                "Подарок почти готов — осталось немного!", 16, red,
                new Vector2(0.08f, 0.19f), new Vector2(0.38f, 0.29f), TextAnchor.MiddleCenter);

            qualityTimerLabel = ChildLabel(qualityCheckRoot.transform, "", 20, red,
                new Vector2(0.76f, 0.765f), new Vector2(0.90f, 0.835f), TextAnchor.MiddleCenter);
            qualityTimerFill = Panel(qualityCheckRoot.transform, "Полоса таймера",
                new Vector2(0.12f, 0.115f), new Vector2(0.88f, 0.135f), gold).GetComponent<Image>();
            qualityTimerFill.type = Image.Type.Filled;
            qualityTimerFill.fillMethod = Image.FillMethod.Horizontal;
            qualityTimerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            qualityTimerFill.fillAmount = 1f;

            BuildQualityGiftPreview();
            ButtonAtOnParent(qualityCheckRoot.transform, "ПРОПУСТИТЬ",
                new Color32(126, 53, 49, 255), BeginQualityCelebration,
                new Vector2(0.70f, 0.055f), new Vector2(0.90f, 0.115f));

            ShowCurrentQualityTask();
            RefreshQualityTimer();
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
            Color giftPaper = GetWrappingPaperColor(selectedPaper);
            Color ribbon = GetWrappingRibbonColor(selectedRibbon);

            FestivePanel(qualityCheckRoot.transform, "Подложка подарка",
                new Vector2(0.395f, 0.205f), new Vector2(0.705f, 0.735f), cream);
            Panel(qualityCheckRoot.transform, "Коробка готового подарка",
                new Vector2(0.445f, 0.285f), new Vector2(0.655f, 0.645f), giftPaper);
            Panel(qualityCheckRoot.transform, "Крышка подарка",
                new Vector2(0.425f, 0.615f), new Vector2(0.675f, 0.685f), gold);
            Panel(qualityCheckRoot.transform, "Лента поперёк коробки",
                new Vector2(0.445f, 0.435f), new Vector2(0.655f, 0.475f),
                new Color(ribbon.r, ribbon.g, ribbon.b, 0.70f));
            Panel(qualityCheckRoot.transform, "Бант слева",
                new Vector2(0.495f, 0.665f), new Vector2(0.525f, 0.735f), ribbon);
            Panel(qualityCheckRoot.transform, "Бант справа",
                new Vector2(0.535f, 0.665f), new Vector2(0.565f, 0.735f), ribbon);
            Panel(qualityCheckRoot.transform, "Узел банта",
                new Vector2(0.522f, 0.675f), new Vector2(0.539f, 0.708f), gold);
            ChildLabel(qualityCheckRoot.transform, "РОЖДЕСТВЕНСКИЙ ПОДАРОК", 15, paper,
                new Vector2(0.455f, 0.295f), new Vector2(0.645f, 0.34f), TextAnchor.MiddleCenter);
        }

        private void ShowCurrentQualityTask()
        {
            if (qualityCheckRoot == null || qualityTaskIndex >= qualityTaskSequence.Count)
                return;

            if (qualityTaskItem != null)
                Destroy(qualityTaskItem.gameObject);
            if (qualityTaskTarget != null)
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
                    qualityTaskHint.text = "ПОПРАВЬ ЛЕНТУ";
                    qualityTaskDescription.text = "Перетащи ленту в золотую отметку на подарке.";
                    qualityTaskTarget = Panel(qualityCheckRoot.transform, "Место для ленты",
                        new Vector2(0.515f, 0.285f), new Vector2(0.558f, 0.645f),
                        new Color(1f, 0.82f, 0.35f, 0.38f));
                    qualityTaskTargetImage = qualityTaskTarget.GetComponent<Image>();
                    qualityTaskHandle = CreateQualityDragItem("Лента для подарка", "ЛЕНТА",
                        GiftQualityTask.Ribbon, new Vector2(0.19f, 0.46f),
                        new Vector2(62f, 300f), GetWrappingRibbonColor(selectedRibbon));
                    qualityTaskItem = qualityTaskHandle.transform as RectTransform;
                    break;

                case GiftQualityTask.Postcard:
                    qualityTaskHint.text = "ПРИКРЕПИ ОТКРЫТКУ";
                    qualityTaskDescription.text = "Перетащи открытку на отмеченное место у крышки.";
                    qualityTaskTarget = Panel(qualityCheckRoot.transform, "Место для открытки",
                        new Vector2(0.565f, 0.575f), new Vector2(0.685f, 0.685f),
                        new Color(1f, 0.91f, 0.70f, 0.55f));
                    qualityTaskTargetImage = qualityTaskTarget.GetComponent<Image>();
                    qualityTaskHandle = CreateQualityDragItem("Открытка для подарка", "С ТЕПЛОМ",
                        GiftQualityTask.Postcard, new Vector2(0.19f, 0.37f),
                        new Vector2(176f, 104f), new Color32(236, 218, 180, 255));
                    qualityTaskItem = qualityTaskHandle.transform as RectTransform;
                    break;

                case GiftQualityTask.Seal:
                    qualityTaskHint.text = "ЗАКЛЕЙ УПАКОВКУ";
                    qualityTaskDescription.text = "Нажми на золотую отметку или проведи по ней.";
                    qualityTaskTarget = Panel(qualityCheckRoot.transform, "Отмеченный участок для скотча",
                        new Vector2(0.485f, 0.34f), new Vector2(0.565f, 0.425f),
                        new Color(1f, 0.78f, 0.28f, 0.62f));
                    qualityTaskTargetImage = qualityTaskTarget.GetComponent<Image>();
                    qualityTaskTargetImage.raycastTarget = true;
                    qualityTaskHandle = qualityTaskTarget.gameObject.AddComponent<LastGiftQualityDragHandle>();
                    qualityTaskHandle.Initialize(this, GiftQualityTask.Ribbon, true);
                    qualityTaskItem = qualityTaskTarget;
                    ChildLabel(qualityTaskTarget, "СКОТЧ", 15, brown,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter).raycastTarget = false;
                    break;
            }

            qualityTaskStatus.text = "Сделай последний штрих — промах можно исправить.";
        }

        private LastGiftQualityDragHandle CreateQualityDragItem(
            string objectName, string caption, GiftQualityTask task, Vector2 anchor, Vector2 size, Color color)
        {
            GameObject item = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(LastGiftQualityDragHandle));
            item.transform.SetParent(qualityCheckRoot.transform, false);

            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            ChildLabel(item.transform, caption, 15, paper,
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

            if (qualityTaskItem != null)
                qualityTaskItem.gameObject.SetActive(false);
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
            if (qualityTaskTarget != null && qualityTaskTargetImage != null)
            {
                Color finalColor = qualityTaskTargetImage.color;
                finalColor.a = 1f;
                qualityTaskTargetImage.color = finalColor;
            }

            LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.OrderComplete);
            qualityCelebrationGlow = Panel(qualityCheckRoot.transform, "Золотое сияние готового подарка",
                new Vector2(0.18f, 0.365f), new Vector2(0.82f, 0.675f),
                new Color(1f, 0.82f, 0.34f, 0.26f)).GetComponent<Image>();

            string title = perfect ? "ОТЛИЧНАЯ РАБОТА!" : "ПОДАРОК ГОТОВ!";
            ChildLabel(qualityCheckRoot.transform, title, 30, red,
                new Vector2(0.20f, 0.49f), new Vector2(0.80f, 0.62f), TextAnchor.MiddleCenter);
            string reward = qualityBonusForSession > 0
                ? "Аккуратность: +" + qualityBonusForSession + " монеты ✦"
                : "Основная награда за заказ сохранена.";
            ChildLabel(qualityCheckRoot.transform, reward, 18, brown,
                new Vector2(0.20f, 0.405f), new Vector2(0.80f, 0.49f), TextAnchor.MiddleCenter);

            ButtonAtOnParent(qualityCheckRoot.transform, "ПРОДОЛЖИТЬ", green,
                FinishQualityCheckPresentation, new Vector2(0.35f, 0.245f), new Vector2(0.65f, 0.325f));
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
