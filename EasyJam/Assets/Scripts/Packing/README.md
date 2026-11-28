# The Last Gift — Packing prototype

## Quick setup
1. Create an empty GameObject in a scene, name it **Packing Board**.
2. Add **PackingBoardController**.
3. Leave **Run Demo On Start** enabled for a temporary runtime sample, or disable it and call `SetupBoard(width, height, gifts)` from your own code.
4. Assign the Main Camera to **Target Camera** if the scene camera is not tagged MainCamera.

The prototype draws its own grid and gift cell visuals using generated runtime sprites, so it requires no new packages or art assets. Demo gifts are shown in two tray columns outside the left and right sides of the board; four items are arranged around the board for visibility. Demo gift definitions are temporary ScriptableObjects created at runtime.

## Controls
- **Left mouse:** pick up, drag, and release a gift. Release on the board to place; release outside/invalid area returns it to its tray position.
- **R / Space / right mouse while dragging:** rotate a rotatable gift clockwise by 90 degrees.
- Gifts can be picked up from the tray or from the board and moved again.

## Integration API
- `SetupBoard(int width, int height, List<GiftDefinition> gifts)`: replaces the board and current gifts.
- `TryValidate(out PackingResult result)`: returns true only when every gift is placed inside the board without overlaps.
- `PackingResult`: `IsValid`, `AllItemsPlaced`, `PlacedCount`, `TotalCount`, `Message`.

## Create reusable gift assets
In Unity, use **The Last Gift → Packing → Create Example Gift Assets** to generate sample `GiftDefinition` assets in `Assets/Gifts`. Or right-click in the Project window and choose **Create → The Last Gift → Packing → Gift Definition**. Edit name, shape cells, sprite, rotation permission and tint in the Inspector.

Shape coordinates are integer cell offsets. A shape should use unique cells; negative or non-normalized coordinates are accepted and normalized for display and rotation.

## Notes
The demo is deliberately a separate component-level prototype and does not edit SampleScene. To play it, add the controller to any scene object and press Play. A runtime setup API call can replace the demo at any time.
