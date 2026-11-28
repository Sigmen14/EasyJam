using System;
using UnityEngine;
using TheLastGift.UI;

/// <summary>Story beats for the short Christmas Eve run; presentation remains in the existing UI.</summary>
[DisallowMultipleComponent]
public sealed class LastGiftStoryController : MonoBehaviour
{
    private LastGiftUIController ui;
    private bool isPlaying;

    /// <summary>Optional hook for a separately-authored ending animation.</summary>
    public Func<Action, bool> FinalAnimationPlayer { get; set; }
    public bool IsPlaying => isPlaying;

    private struct DialogueLine
    {
        public readonly string Speaker;
        public readonly string Text;
        public DialogueLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    public void Configure(LastGiftUIController uiController) => ui = uiController;

    public void BeginOpening(Action onFinished)
    {
        Play(new[]
        {
            new DialogueLine("Хозяин лавки", "Вечер 24 декабря. Спасибо, что помогаешь мне успеть до закрытия!"),
            new DialogueLine("Миша", "Здравствуйте. Моя мама — медсестра. Сегодня она снова будет на ночной смене."),
            new DialogueLine("Миша", "Когда на улице холодно, мама всегда отдаёт мне свой шарф. Я хочу подарить ей новый."),
            new DialogueLine("Хозяин лавки", "Поможем покупателям, а потом вместе подумаем, как исполнить твоё желание.")
        }, onFinished);
    }

    public void PlayDuringProgression(int completedOrders, Action onFinished)
    {
        if (completedOrders == 1)
            Play(new[] { new DialogueLine("Хозяин лавки", "Первый заказ готов. Отличная работа — монеты уже в кассе!") }, onFinished);
        else if (completedOrders == 2)
            Play(new[]
            {
                new DialogueLine("Миша", "Я тоже помогал с поручениями и немного накопил. Но до шарфа пока не хватает."),
                new DialogueLine("Хозяин лавки", "Не переживай. У нас ещё есть время до закрытия.")
            }, onFinished);
        else
            onFinished?.Invoke();
    }

    public void PlayBoyReturnAndDiscount(Action onFinished)
    {
        Play(new[]
        {
            new DialogueLine("Миша", "Я снова здесь. Я накопил немного, но красивый шарф всё ещё мне не по карману."),
            new DialogueLine("Хозяин лавки", "Сегодня особенный вечер. Для тебя будет рождественская скидка."),
            new DialogueLine("Хозяин лавки", "Это наш последний заказ. Давай красиво упакуем шарф для твоей мамы.")
        }, onFinished);
    }

    public void PlayEnding(Action onFinished)
    {
        Play(new[]
        {
            new DialogueLine("Миша", "Спасибо! Теперь я успею встретить маму после смены с подарком."),
            new DialogueLine("Хозяин лавки", "Иногда самый дорогой подарок — не тот, который стоит больше всего. А тот, в который вложили любовь.")
        }, () =>
        {
            if (FinalAnimationPlayer == null || !FinalAnimationPlayer.Invoke(onFinished))
                onFinished?.Invoke();
        });
    }

    private void Play(DialogueLine[] lines, Action onFinished)
    {
        if (isPlaying) return;
        if (ui == null || lines == null || lines.Length == 0) { onFinished?.Invoke(); return; }
        isPlaying = true;
        ShowLine(lines, 0, () => { isPlaying = false; onFinished?.Invoke(); });
    }

    private void ShowLine(DialogueLine[] lines, int index, Action onFinished)
    {
        DialogueLine line = lines[index];
        ui.ShowStoryDialogue(line.Speaker, line.Text, () =>
        {
            int next = index + 1;
            if (next < lines.Length) ShowLine(lines, next, onFinished);
            else onFinished?.Invoke();
        });
    }
}
