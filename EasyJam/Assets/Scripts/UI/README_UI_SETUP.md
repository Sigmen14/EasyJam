# The Last Gift UI — Setup

## Project findings
- Unity Editor version: **6000.0.83f1** (Unity 6, 2D URP).
- Project is nested in `EasyJam/`; repository root is only a wrapper.
- Read `EasyJam/AGENTS.md`. No `Instructions/` directory or gameplay scripts were found in the tracked tree.
- Packing/progression systems are not present. This UI therefore exposes public `Action` hooks and presentation methods; it does not duplicate game managers or invent their event types.

## Generate the prefab
1. Open project using Unity **6000.0.83f1**.
2. Wait until compile/import completes.
3. Menu: **The Last Gift → UI → Create UI Root Prefab**.
4. Generated prefab: `Assets/Prefabs/UI/LastGiftUI.prefab`.
5. Drag prefab into the gameplay scene (SampleScene was not edited). Make sure the scene has an EventSystem.
6. Controller creates a screen-space Canvas automatically at runtime.

## Included screens
- Main menu, first-order tutorial, gameplay HUD, order-success, day-end, final.
- HUD: day/coin/order labels; customer portrait and wish placeholders; gift shelf; packing-board placeholder; confirmation button.
- Style: paper cream, wood brown, Christmas red/green and gold, snow/window motifs.
- Hover/pressed Button ColorBlock and a small scale pop for feedback/reward.

## Integration API
UI class: `TheLastGift.UI.LastGiftUIController`.
- Public callback Actions: `ConfirmPacking`, `Continue`, `StartGame` (wire via adapter code; not Inspector-serializable).
- Methods: `PackingFeedback(string,bool)`, `OrderSuccess(int earnedCoins)`, `DayEnd(int orders,int earnings)`, `FinalScreen()`, `Gameplay()`, `MainMenu()`, `ShowTutorial()`.
- Confirm button calls `ConfirmPacking` only. Adapter should invoke board validation, pass `PackingFeedback` on failure and `OrderSuccess` on success; progression calls `DayEnd` or `FinalScreen` at appropriate milestones. Once actual classes/events land, add adapter without modifying packing/progression systems.

## Inspector / hookup
1. Place prefab in gameplay scene.
2. Create a separate adapter GameObject; set a serialized reference to `LastGiftUIController` in the adapter.
3. Adapter code subscribes to the real progression/order events, and assigns callback Actions:
   `ui.ConfirmPacking = ValidatePacking;`, `ui.Continue = progression.ContinueAfterResult;`, `ui.StartGame = progression.StartGame;` (adapt signatures after checking the real APIs).
4. On failed validation call `ui.PackingFeedback(reason,false)`; on success call `ui.OrderSuccess(reward)`; for end-of-day call `ui.DayEnd(orderCount,dayCoins)`; on game end call `ui.FinalScreen()`.
5. Update the display labels and customer text with real order data after the model classes are merged. Current content is placeholder data.

## Manual test checklist
- Generate prefab; inspect Console for compile errors.
- Add prefab to an empty test scene and press Play.
- Test menu -> tutorial -> gameplay, button states, and resolution scaling.
- Temporarily call success/day-end/final presentation methods from an adapter/test script.
- Confirm packing callback is invoked; it is intentionally unconnected until the external systems are merged.
- Test widescreen and narrow aspect ratios.

**Unity Editor was not available to run this project here.**
## Limitations
No custom sprite portraits, gift icons, snow particles, garland assets, or audio clips were included. Emojis/text are temporary visual placeholders. No audio is wired. Packing grid/order manager integrations remain blocked on the other programmers' classes/events.
