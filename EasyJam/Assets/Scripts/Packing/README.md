# The Last Gift — Packing prototype

## Quick setup
The **SampleScene** already contains a **Packing Board** object with `PackingBoardController`. Open the scene and press Play. Leave **Run Demo On Start** enabled to launch the built-in puzzle, or disable it and call `SetupBoard(width, height, gifts)` from your own code. Assign the Main Camera to **Target Camera** only if the scene camera is not tagged MainCamera.

The prototype draws its own grid and gift cell visuals using generated runtime sprites, so it requires no new packages or art assets. Demo gift definitions are temporary ScriptableObjects created at runtime.

## Demo puzzle
The default board is 4×4. It has four tetrominoes (two T pieces, one L piece, and one S piece), for exactly 16 cells total. Every cell must be covered by exactly one fragment. The initial arrangement is deliberately not solved; rotate and position the pieces to fill the whole box.

## Controls
- **Left mouse:** pick up, drag, and release a gift. Release on the board to place it.
- **R / Space / right mouse while dragging:** rotate a rotatable gift clockwise by 90 degrees. The same hint is displayed in the game HUD.
- **V:** manually validate the current arrangement.
- To remove a placed gift, drag it beyond the edge of the box; it returns to its tray position.
- Gifts can be picked up from the tray or from the board and moved again. An invalid move of an already placed piece restores its previous position; a piece dragged from the tray returns to the tray if the drop is invalid.

The HUD shows the number of covered cells and placed fragments, plus the rotation and removal controls. The puzzle is validated automatically after each drop. When all fragments are placed without overlaps and all 16 cells are covered, a completion panel appears and offers a **Play Again** button.

## Integration API
- `SetupBoard(int width, int height, List<GiftDefinition> gifts)`: replaces the board and current gifts.
- `TryValidate(out PackingResult result)`: returns true only when every gift is placed inside the board without overlaps and every board cell is covered.
- `PackingResult`: `IsValid`, `AllItemsPlaced`, `PlacedCount`, `TotalCount`, `Message`.

## Create reusable gift assets
In Unity, use **The Last Gift → Packing → Create Example Gift Assets** to generate sample `GiftDefinition` assets in `Assets/Gifts`. Or right-click in the Project window and choose **Create → The Last Gift → Packing → Gift Definition**. Edit name, shape cells, sprite, rotation permission and tint in the Inspector.

Shape coordinates are integer cell offsets. A shape should use unique cells; negative or non-normalized coordinates are accepted and normalized for display and rotation.
