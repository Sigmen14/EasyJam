# The Last Gift UI — Setup

## Project findings
- Unity Editor version: **6000.0.83f1** (Unity 6, 2D URP).
- Project is nested in `EasyJam/`; repository root is a wrapper.
- `EasyJam/AGENTS.md` was reviewed. At the time of implementation, the tracked project did not contain the external packing/order progression managers.
- The UI does not create a replacement order manager. Its callback API remains available for an adapter to the real game systems.

## Generate the prefab
1. Open the project using Unity **6000.0.83f1**.
2. Wait until script compilation and asset import complete.
3. In the Unity menu select **The Last Gift → UI → Create UI Root Prefab**.
4. The prefab is generated at `Assets/Prefabs/UI/LastGiftUI.prefab` and an instance is automatically added to the currently open scene.
5. If the scene has no active camera, the command creates a basic 2D Main Camera. The UI controller also creates a fallback camera at runtime if needed.
6. Save the open scene with **Ctrl+S**, then press Play. Do not drag a second UI prefab into the scene if the command has already added one.

## Current UI and interaction
- Russian main menu and a first-time tutorial with explicit mouse drag instructions.
- Gameplay HUD with separate customer, packing box and gift shelf areas.
- Four illustrated gift icons (teddy bear, present, candy cane and snow globe), loaded from `Assets/Resources/LastGift/GiftIconsBase64.txt`.
- Drag a gift from the shelf and release it over the box. It snaps into the next available cell in a 3×3 visual grid.
- The **В МЕНЮ** button is at the lower left; **ПРОВЕРИТЬ ЗАКАЗ** is centered below the box.
- Order success, day-end and final presentation screens remain available through public methods.

**Important prototype boundary:** the drag-and-drop visuals work independently, but current drop placement is a simple 3×3 demonstration. It does not yet test overlaps, rotations, or fit constraints. When `ConfirmPacking` is not connected, the check button uses a temporary demonstration order (teddy bear, present and candy cane) and shows a success screen; connect the real game managers before treating this as final gameplay.

## Integration API
UI class: `TheLastGift.UI.LastGiftUIController`.
- Public callback Actions: `ConfirmPacking`, `Continue`, `StartGame` (wire via an adapter; these are not Inspector-serializable).
- Methods: `PackingFeedback(string,bool)`, `OrderSuccess(int earnedCoins)`, `DayEnd(int orders,int earnings)`, `FinalScreen()`, `Gameplay()`, `MainMenu()`, `ShowTutorial()`.
- Assign `ui.ConfirmPacking = ValidatePacking;`, `ui.Continue = progression.ContinueAfterResult;` and `ui.StartGame = progression.StartGame;` after verifying the actual external APIs.
- On failed validation call `ui.PackingFeedback(reason,false)`; on success call `ui.OrderSuccess(reward)`; for end-of-day call `ui.DayEnd(orderCount,dayCoins)`; on game end call `ui.FinalScreen()`.

## Manual test checklist
- Generate the prefab and check Console for compile/import errors.
- Add the prefab to a test scene and press Play.
- Navigate main menu → tutorial → gameplay.
- Drag each of the four gift pictures from the right shelf into the central box; drop outside the box and confirm the feedback message.
- Fill all nine cells and confirm that the box-full message appears.
- Press **ПРОВЕРИТЬ ЗАКАЗ** with an empty box and with items in the box.
- Test widescreen and narrow aspect ratios.
- After the external game managers are available, wire the adapter and test the actual packing rules and order progression.

**Unity Editor compilation and play-mode testing have not been run by the implementation environment.**
