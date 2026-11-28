# The Last Gift audio

All original game audio is stored under Assets/Resources/Audio and loaded by Resources path.

## Music
- Audio/Music/MenuTheme — calm, bell-led Christmas theme for the main menu.
- Audio/Music/WorkshopTheme — warm, gently plucked workshop loop.
- Audio/Music/EndingTheme — soft, emotional ending loop.

LastGiftAudioManager.PlayMusic(LastGiftMusicTheme, fadeDuration) crossfades between themes. Re-requesting the active clip does not restart it. The manager persists between scenes and owns two music sources.

## Sound effects
LastGiftAudioManager.PlaySfx(LastGiftSoundEffect) plays one-shots from Audio/Sfx: UI click, gift drag, packed gift, error, new order, dialogue advance and order complete.

## Volume and pause-menu reuse
The existing settings screen exposes master, music and SFX sliders. Values use PlayerPrefs and are flushed on application pause/quit. LastGiftUIController.ShowAudioSettings() opens this panel and returns to the main menu. A future pause menu can call ShowAudioSettings(onBack) to restore its own state without duplicating slider logic.

Example API calls: LastGiftAudioManager.PlaySfx(LastGiftSoundEffect.GiftPacked); LastGiftAudioManager.PlayMusic(LastGiftMusicTheme.Workshop); LastGiftAudioManager.SetMusicVolume(0.5f).
