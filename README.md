# ItemRecommender

Client-side item recommendations for the **Artifact of Command** menu – like a LoL build overlay.
Other players do **not** need this mod.

| Marker | Meaning |
|---|---|
| Gold frame + star | Core item for your survivor |
| Pulsing star | Best pick in this menu |
| Light frame | Good pick |
| Turquoise dot | Synergy with an item you already have (proc chain) |
| Orange frame + X | Rather not |
| Red frame + X | Don't build – e.g. a void item that would convert your good items |

A legend is shown next to the Command menu. Covers all DLC, void and lunar items; void conversions are detected automatically (also for modded void items).

Hover an item to see *why* it is recommended (German/English, follows the game language).

## Edit the builds
After the first launch, edit `BepInEx/config/ItemRecommender.builds.txt`.
The file is re-read while the game is running. Format is explained at the top of the file.

## Build path
A tile under the objectives shows your survivor's build path with item icons and how many you already have (e.g. `3/10`). The current step has a gold frame, finished steps are dimmed.

Every survivor has three pages (early, mid, late run). When every item on a page is done, the next page appears ("Build path 2/3"). Each `Path:` line in the build file is one page; long lines are split every 8 items:
```
Path: Syringe x5 > Bear x3 > Hoof x2
Path: Syringe x10 > CritGlasses x10 > Missile x2
```
Counts are totals, so a later page can raise an earlier target.

When every page is done, **bonus pages** are generated from the ratings and your current synergies (toggle: `BonusPages`).

## Other details
- When the Command menu opens, the cursor sits on an item and the game shows its description right away. The mod hides it until you move the mouse.
- Holding the void version of an item (e.g. Safer Spaces) makes the normal item a good pick, since it becomes another void stack.
- Ratings follow the community consensus tier lists (S = core, A = good, rest neutral; only items that actively hurt you get an X), with survivor-specific overrides.

## Config
`BepInEx/config/clxru.ItemRecommender.cfg`: `Enabled`, `ShowGoodTier`, `ShowLegend`, `Language` (auto/de/en), `ShowBuildPath`, `BonusPages`, `PathSeparator`, `HideTooltipUntilMouseMoves` (hides the item description that pops up because the cursor starts on an item, until you move the mouse).
