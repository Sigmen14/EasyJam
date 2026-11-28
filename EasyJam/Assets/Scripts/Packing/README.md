# The Last Gift — 4×4 packing puzzle

## Campaign
The packing scene contains three authored 4×4 orders. Each card is shown before the board becomes interactive and lists every special rule with an icon and a Russian description.

1. **Бережная доставка** — one rule: fragile gifts may not touch heavy gifts (including diagonally).
2. **Подарочный комплект** — two rules: fragile gifts stay away from heavy gifts, and the toy/accessory pair must touch.
3. **Рождественский финал** — two rules: the main gift belongs entirely inside the central 2×2 cells, and the two companion shapes must touch.

All orders use four tetrominoes (16 cells in total). The order definitions are ScriptableObjects in `Assets/Data/PackingOrders`; reusable item shapes and weights live in `Assets/Gifts`. `PackingPuzzleOrderDefinition.HasFeasibleSolution` exhaustively searches rotations and integer grid anchors before an order is loaded. The Editor smoke test repeats that check across every authored order. The sequence is fixed, not random, and the main story campaign assets and their ~10-minute pacing are not changed.

## Controls
- **Left mouse:** pick up, drag, and release a gift.
- **R / Space / right mouse while dragging:** rotate a rotatable gift clockwise by 90 degrees.
- **V:** manually re-check the full board and every active condition.
- To remove a placed gift, drag it beyond the edge of the box; it returns to its tray position.
- An invalid drop of a placed gift restores its previous position. A gift from the tray returns to the tray if its drop is invalid.

The grid still requires every 4×4 cell to be covered exactly once. Special conditions are checked separately after the regular overlap, bounds, and full-coverage checks. A failed rule blocks completion but never disables dragging: pick up the affected gifts and correct their positions. The HUD shows a green tick for a fulfilled rule, a red cross and a specific correction hint for a violated rule, and a neutral circle while the relevant pieces have not yet been placed.

## Extension
- Add handling metadata to a `GiftDefinition` with `PackingWeightCategory` (`Normal`, `Fragile`, `Heavy`).
- Add a `PackingConstraintDefinition` to an order for `FragileAwayFromHeavy`, `KeepTogether`, or `MustBeInCenter`.
- Use `PackingPuzzleOrderDefinition` assets to author a fixed sequence. The configuration validator checks item references, unique shapes, exact 16-cell area, rule targets and supported rule count. The feasibility search checks that at least one full, non-overlapping 4×4 layout satisfies every rule.

## Existing API
- `SetupBoard(int width, int height, List<GiftDefinition> gifts)` remains available for demos and external callers.
- `TryValidate(out PackingResult result)` preserves the original full-board validation and includes active order constraints.
- Gift dragging, rotations, removal to tray, and invalid-drop restoration are unchanged.


## Verification handoff

The project declares Unity **6000.0.83f1** in `ProjectSettings/ProjectVersion.txt`.

Before considering the change fully verified in the Editor:

1. Open `Assets/Scenes/SampleScene.unity` in that Unity version and wait for asset import and script compilation to finish. Check the Console for compile/import errors.
2. Run **Tools → The Last Gift → Run Packing Constraint Smoke Test**. The expected result is `PASSED` for all three orders.
3. Enter Play Mode. The first order card should appear before the board accepts input. Start it and confirm the tray shows **ТЯЖЁЛЫЙ ПОЕЗД** and **ХРУПКИЙ ШАР**, with recognizable illustrations and readable labels.
4. Test the fragile/heavy rule by placing them adjacent, including diagonally: the HUD should show a red cross and a correction hint. Move them apart: it should turn green. Confirm that a failed rule never prevents dragging.
5. Complete order 1, then order 2 (fragile/heavy separation plus toy/accessory adjacency), then order 3 (main gift fully in the center 2×2 plus the two L-shaped parts touching). Each order must also fill all 16 cells without overlap.
6. Confirm the final completion button loops to order 1 and **ВЕРНУТЬСЯ В ЛАВКУ** returns to the shop normally.

Automated geometry search previously found valid solutions for all orders (8, 8, and 4 layouts respectively). The Editor smoke test and Play Mode have **not** yet been run in this environment; the checklist above is the remaining manual sign-off.
