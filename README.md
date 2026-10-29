# ItemRecommender

Client-side item recommendations for **Risk of Rain 2**: markers in the **Artifact of Command** menu and a **build path** in the HUD.
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
- **Pick order** above the legend: the best 5 of exactly the items this menu offers, ranked like the build path (rating + survivor + synergies).
- When the menu opens, the cursor sits on an item and the game would show its description right away. The mod hides it until you move the mouse.

## Build analysis
The mod reads what your survivor and your items actually do – from the skill and item descriptions in the game, so it also works for modded items and survivors – and rates every offered item against that:

| Your build | Gets pushed up |
|---|---|
| Several debuff types (bleed, burn, poison, slow, weaken, armor shred, launch, …) | Death Mark, Noxious Thorn – and items that add a debuff type you don't have yet |
| A fire source (Gasoline, Kjaro's Band, Artificer, CHEF, …) | Ignition Tank |
| Bleed / stun sources | Sanguine Chalice / Vile Censer |
| Crit chance | Laser Scope, Predatory Instincts, Harvester's Scythe (and no more crit once you're at 100%) |
| High attack speed / many on-hit items | On-hit items / attack speed |
| A primary that hits one target (Huntress, Commando, Bandit, …) | Area damage: Gasoline, Will-o'-the-wisp, Ukulele, Behemoth, … |
| Area damage | On-kill items (Topaz Brooch, Infusion, Bandolier, …) |

Items that don't fit yet say why, e.g. "You have no burn source yet". The reasons show in green in the tooltip; the score feeds the pick order and the build path.

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
- Only **white and green** items: red and boss items rarely drop, so they are left out.
- **F6** switches to the **rarity view**: one row of white and one row of green items (5 each), best first – what to take from the next white or green Command menu. Each row always has a defense/health item and a healing item, at most 2 damage items.
- **F7** swaps the highlighted item: it is gone for the rest of the run, another item takes its slot. **F8** skips the whole page.
- **F5** shows the previous page (press again to go further back), **F8** goes forward again.
- Skips and pages are remembered for the whole run, also after a game restart (`BepInEx/config/ItemRecommender.state.txt`).
- Once you hold a void item, its normal item no longer shows up; the path shows the void item and counts both stacks together (e.g. Tougher Times + Safer Spaces).

## Edit the builds
After the first launch, edit `BepInEx/config/ItemRecommender.builds.txt`. It is re-read while the game runs; the format is explained at the top. Each `Path:` line in a survivor section is a page, e.g.
```
Path: Syringe x10 > Bear x6 > Hoof x6 > CritGlasses x10
```

## Config
`BepInEx/config/Waso.ItemRecommender.cfg` – or in-game under **Settings > Mod Options** if [Risk of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) is installed (optional, changes apply right away, hotkeys can be rebound there).

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
| `SwitchViewKey` | F6 | Build path ↔ rarity view |
| `RarityRowSize` | 5 | Items per row in the rarity view (3–8) |
| `ShowMenuOrder` | true | Pick order above the legend in the Command menu |
| `SkipItemKey` / `SkipPageKey` | F7 / F8 | Swap item / skip page |
| `BackPageKey` | F5 | Previous page |
| `ShowSkipHint` | true | Hotkey hint under the tile |
| `PathSeparator` | `>` | Symbol between steps |

Source: https://github.com/Waso2101/ItemRecommender
