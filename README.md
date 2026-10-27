# ItemRecommender

Client-side item recommendations for **Risk of Rain 2**: markers in the **Artifact of Command** menu and a **build path** in the HUD – like a build overlay in other games.
Other players do **not** need this mod. German and English (follows the game language).

## Command menu

| Marker | Meaning |
|---|---|
| Gold frame + star | Core item for your survivor |
| Pulsing star | Best pick in this menu |
| Light frame | Good pick |
| Turquoise dot | Synergy with an item you already have |
| Orange frame + X | Rather not |
| Red frame + X | Don't build – e.g. a void item that would convert your good items |

- Hover an item to see **why**: what it does, why it is rated like this and what it pairs with.
- A legend is shown next to the menu.
- Void items are checked automatically (also modded ones): a void item that would eat your good items is marked red. If you already hold the void version (e.g. Safer Spaces), the normal item becomes a good pick, because it turns into another void stack.
- When the menu opens, the cursor sits on an item and the game would show its description right away. The mod hides it until you move the mouse.

**Ratings** follow the community consensus (six tier lists combined): S = core, A = good, the rest neutral. Only items that actively hurt you (Egocentrism, Purity, …) get an X. Every survivor has its own overrides, e.g. Seeker: Rose Buckler and Little Disciple work during Sojourn.

## Build path

A tile under the objectives shows which items to collect next, with icons and your progress (`3/10`). The next step has a gold frame, finished steps are dimmed.

- **Page 1** is a fixed start per survivor from the build file.
- **Page 2 onwards** is generated once per page and stays until it is done. Each page is a mix instead of eight damage items:

  | Slot | Page 2 | Page 3+ (default) |
  |---|---|---|
  | Offense (damage, procs) | 4 | 3 |
  | Defense (block, armor, barrier) | 1 | 1 |
  | Max health / shield (against one-shots) | 1 | 1 |
  | Sustain (healing) | 1 | 2 |
  | Utility / mobility | 1 | 1 |

  Items are picked by community rating, your survivor's rating, synergies with what you hold and the survivor's own plan. Items from the last two pages are pushed back so pages don't repeat, and a void item never shares a page with the item it converts.
- **Red and boss items** (blue counter) are optional: they rarely drop, so they don't block the page. Max 2 per page.
- **F7** swaps the highlighted item: it is gone until the next stage, another item takes its slot. **F8** skips the whole page.

## Edit the builds
After the first launch, edit `BepInEx/config/ItemRecommender.builds.txt`. It is re-read while the game runs; the format is explained at the top. Each `Path:` line in a survivor section is a page, e.g.
```
Path: Syringe x10 > Bear x6 > Hoof x6 > CritGlasses x10
```

## Config
`BepInEx/config/Waso.ItemRecommender.cfg`

| Setting | Default | |
|---|---|---|
| `Enabled` | true | Whole mod on/off |
| `ShowGoodTier` | true | Also frame "good" items, not only core items |
| `ShowLegend` | true | Legend next to the Command menu |
| `Language` | auto | auto / de / en |
| `HideTooltipUntilMouseMoves` | true | Hide the description under the cursor when the menu opens |
| `ShowBuildPath` | true | Build path tile |
| `LivePages` | true | Generate pages after page 1 (off: only the pages from the file) |
| `LatePageFocus` | sustain | Pages 3+: sustain / balanced / damage |
| `SkipItemKey` / `SkipPageKey` | F7 / F8 | Swap item / skip page |
| `ShowSkipHint` | true | Hotkey hint under the tile |
| `PathSeparator` | `>` | Symbol between steps |

Source: https://github.com/Waso2101/ItemRecommender
