# Changelog

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
