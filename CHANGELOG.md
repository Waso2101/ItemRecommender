# Changelog

## 0.9.0
- Build analysis: reads your survivor's skills and every item description (also modded ones) and rates items against your build
  - Death Mark / Noxious Thorn rise with the number of debuff types you apply; new debuff types are pushed while you hold them
  - Ignition Tank, Sanguine Chalice, Vile Censer only once you have a burn / bleed / stun source
  - Crit payoff items scale with your actual crit chance; no more crit items at 100%
  - Single-target survivors (Huntress, Commando, Bandit, …) get area damage pushed; area damage pushes on-kill items
- Tooltip shows these reasons in green, and what's missing in grey
- Huntress: area damage (Gasoline, Will-o'-the-wisp, Ukulele, Behemoth) in her plan

## 0.8.1
- Risk of Options support (optional): change all settings and hotkeys in-game under Settings > Mod Options
- F5: back to the previous build path page (F8 goes forward again)
- Skipped items and pages are kept for the whole run and saved, so they survive a game restart
- Void items: once you hold one, its normal item leaves the path; the void item shows instead and both stacks count together

## 0.8.0
- Rarity view (F6): one row white, one row green, best first, each row with defense + healing
- Pick order for the open Command menu, shown above the legend
- Build path pages only use white and green items

## 0.7.0
- Pages after page 1 are generated once and stay until done (no more constant re-ranking, no "live" label)
- Balanced pages: offense, defense, max health/shield, sustain and utility slots; later pages favour defense + sustain (`LatePageFocus`)
- Picks use community ratings + survivor ratings + synergies; recent pages are pushed back, void pairs never share a page
- Red/boss items are optional (don't block a page), max 2 per page
- F7 now swaps the item: its slot gets refilled
- README rewritten

## 0.6.0
- Skip hotkeys for the build path: F7 = skip item until next stage, F8 = skip page
- Removed filler items (Medkit, Monster Tooth) from the start pages

## 0.5.1
- Plugin ID is now Waso.ItemRecommender (config file: Waso.ItemRecommender.cfg). Old settings are taken over automatically.

## 0.5.0
- Smaller build path icons, more items per page, higher counts
- Live pages: after page 1, the build path is re-ranked live from ratings + current synergies

## 0.4.0
- Build path tile under the objectives, with pages per survivor (early / mid / late run)
- Tile copies the look of the game's objective panel; icons load via the game's own async image
- Item description no longer pops up when the Command menu opens under the cursor
- Ratings reworked from community tier lists, longer explanations (DE/EN)
- Normal items are rated good if you already hold their void version
- Seeker section

## 0.2.0
- First version: Command menu markers, synergies, void conversion checks, legend
