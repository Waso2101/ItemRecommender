using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemRecommender
{
    public sealed class PathStep
    {
        public string Item;
        public int Target;
        public bool Optional; // red/boss items on generated pages: shown, but don't block the page
    }

    /// <summary>
    /// HUD tile under the objective panel: the survivor's build path from builds.txt
    /// ("Path: CritGlasses x10 > Syringe x10 > ..."), with icons and how many you already have.
    /// Background and heading are copied from the objective panel so the tile looks like the game's own.
    /// </summary>
    public sealed class PathTile : MonoBehaviour
    {
        const float Icon = 20f, Sep = 8f, Gap = 2f, Pad = 6f, CountH = 11f, DefaultWidth = 260f;
        const int PageSize = 10; // longer Path lines are split into pages of this size
        const string TileName = "ItemRecommenderBuildPath";
        static bool loggedStyle;

        HUD hud;
        RectTransform objective;
        LayoutElement layout;
        bool inLayoutGroup;
        Image bg;
        TMP_Text headerStyle; // the objective panel's "Objective:" heading
        float titleH = 22f;
        RectTransform content;
        string signature;
        float nextRefresh;
        // Icons whose load the game skipped (it does that while a stage is loading); retried on every refresh.
        readonly List<AsyncImage> pendingIcons = new List<AsyncImage>();
        // Generated pages (after page 1): built once from roles + ratings, kept until done.
        const int GenSize = 8;
        List<PathStep> genPage;
        int genNumber = 1;
        string genBody;
        readonly HashSet<string> lastPageItems = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> pageBeforeItems = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        // Skipping (hotkeys): items are skipped until the next stage, the start page for the whole run.
        readonly HashSet<string> skipped = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        bool skipStart, onStartPage;
        List<PathStep> shownSteps;
        string currentItem;
        Run lastRun;
        // Pages you already left, for the back key. back = how many pages you look back (0 = current page).
        sealed class Snapshot { public string Number; public List<PathStep> Steps; }
        const int MaxHistory = 50;
        readonly List<Snapshot> history = new List<Snapshot>();
        int back;
        string currentNumber;
        List<PathStep> currentSteps;
        string savedState; // last text written to the state file
        bool rarityView; // F6: build path <-> one row per rarity (white, green)

        /// <summary>Called once per second; creates the tile once the objective panel exists.</summary>
        public static void Ensure(HUD hud)
        {
            if (!hud || hud.GetComponentInChildren<PathTile>(true)) return;
            var obj = hud.GetComponentInChildren<ObjectivePanelController>(true);
            if (!obj || !obj.transform.parent) return;

            var go = new GameObject(TileName, typeof(RectTransform));
            var parent = obj.transform.parent;
            go.transform.SetParent(parent, false);
            var tile = go.AddComponent<PathTile>();
            tile.hud = hud;
            tile.objective = (RectTransform)obj.transform;
            tile.layout = go.AddComponent<LayoutElement>();
            // Inside a vertical layout we become the next row; otherwise we just follow the panel's bottom edge.
            tile.inLayoutGroup = parent.GetComponent<VerticalLayoutGroup>();
            if (tile.inLayoutGroup) go.transform.SetSiblingIndex(obj.transform.GetSiblingIndex() + 1);
            else tile.layout.ignoreLayout = true;
            tile.Build();
        }

        void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);

            bg = Ui.MakeImage(transform, "Bg", Sprites.Fill);
            bg.color = Palette.Panel;
            bg.preserveAspect = false;
            Ui.Place(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CopyObjectiveStyle();

            var c = new GameObject("Content", typeof(RectTransform));
            c.transform.SetParent(transform, false);
            content = (RectTransform)c.transform;
            Ui.Place(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetVisible(false);
        }

        /// <summary>Takes over the objective panel's background sprite and heading font.</summary>
        void CopyObjectiveStyle()
        {
            // Background: the biggest image of the objective panel (the checkbox and icons are small).
            Image source = null;
            float area = 0f;
            foreach (var img in objective.GetComponentsInChildren<Image>(true))
            {
                if (!img.sprite) continue;
                var r = img.rectTransform.rect;
                if (r.width * r.height > area) { area = r.width * r.height; source = img; }
            }
            if (source)
            {
                bg.sprite = source.sprite;
                bg.type = source.type;
                bg.color = source.color;
                bg.material = source.material;
                bg.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                bg.fillCenter = source.fillCenter;
            }

            // Heading: the translated label ("Objective:"), not the objective entries themselves.
            foreach (var t in objective.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!t.GetComponent<LanguageTextMeshController>()) continue;
                headerStyle = t;
                break;
            }
            if (headerStyle) titleH = Mathf.Max(22f, headerStyle.fontSize + 8f);

            if (!loggedStyle)
            {
                loggedStyle = true;
                Plugin.Log.LogInfo($"Build path tile: layout group={inLayoutGroup}, background={(source ? source.name + "/" + source.sprite.name : "none")}, " +
                                   $"heading={(headerStyle ? headerStyle.name + " " + headerStyle.fontSize : "none")}");
            }
        }

        void SetVisible(bool on)
        {
            bg.enabled = on;
            content.gameObject.SetActive(on);
            layout.ignoreLayout = !on || !inLayoutGroup;
            if (!on) ((RectTransform)transform).sizeDelta = Vector2.zero;
        }

        void Update()
        {
            HandleHotkeys();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            try { Refresh(); }
            catch (System.Exception e) { Plugin.Log.LogError(e); nextRefresh = Time.unscaledTime + 10f; }
        }

        void LateUpdate()
        {
            if (inLayoutGroup || !objective || !bg.enabled) return;
            var corners = new Vector3[4];
            objective.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
            var parent = (RectTransform)transform.parent;
            Vector3 bottomRight = parent.InverseTransformPoint(corners[3]);
            transform.localPosition = new Vector3(bottomRight.x, bottomRight.y, 0f);
        }

        void Refresh()
        {
            for (int i = pendingIcons.Count - 1; i >= 0; i--)
            {
                var ai = pendingIcons[i];
                if (!ai || ((Image)ai.renderer).sprite) { pendingIcons.RemoveAt(i); continue; }
                ai.LoadAssetReference();
            }

            CharacterBody body = hud && hud.targetBodyObject ? hud.targetBodyObject.GetComponent<CharacterBody>() : null;
            if (!Plugin.Enabled.Value || !Plugin.ShowBuildPath.Value || !body)
            {
                if (bg.enabled) { SetVisible(false); signature = null; }
                return;
            }
            Plugin.Data.ReloadIfChanged();
            string bodyName = BodyCatalog.GetBodyName(body.bodyIndex);
            var pages = Paginate(Plugin.Data.GetPath(bodyName));
            if (pages.Count == 0)
            {
                if (bg.enabled) { SetVisible(false); signature = null; }
                return;
            }

            // Page 1 comes from the file (a fixed start). After that each page is generated once from
            // item roles, community ratings and your synergies, and stays until it is done.
            bool german = Plugin.German();
            if (lastRun != Run.instance)
            {
                // Skips and pages are kept for the whole run - also after a game restart (state file).
                lastRun = Run.instance;
                skipStart = false;
                skipped.Clear();
                genPage = null;
                genNumber = 1;
                genBody = null;
                lastPageItems.Clear();
                pageBeforeItems.Clear();
                history.Clear();
                back = 0;
                currentNumber = null;
                currentSteps = null;
                LoadState(bodyName);
            }

            float width = inLayoutGroup && objective && objective.rect.width > 100f ? objective.rect.width : DefaultWidth;
            if (rarityView) { RefreshRarity(body, bodyName, german, width); return; }

            List<PathStep> steps = null;
            var start = pages[0].FindAll(st => !skipped.Contains(st.Item));
            bool startDone = skipStart || Complete(body, start);
            onStartPage = !startDone;
            if (!startDone || !Plugin.LivePages.Value) steps = startDone ? pages[pages.Count - 1] : start;
            else steps = GeneratedPage(body, bodyName, german, pages);
            string number = "";
            if (steps == null) steps = pages[pages.Count - 1];
            else if (startDone && Plugin.LivePages.Value) number = genNumber.ToString();
            else if (!startDone) number = "1";

            // Remember the page you leave, so the back key can show it again.
            if (currentNumber != null && currentNumber != number)
            {
                history.Add(new Snapshot { Number = currentNumber, Steps = currentSteps });
                if (history.Count > MaxHistory) history.RemoveAt(0);
            }
            currentNumber = number;
            currentSteps = steps.ConvertAll(st => new PathStep { Item = st.Item, Target = st.Target, Optional = st.Optional });
            SaveState(bodyName);

            string label = PageLabel(german, number);
            if (back > history.Count) back = history.Count;
            if (back > 0)
            {
                var snap = history[history.Count - back];
                steps = snap.Steps;
                label = PageLabel(german, snap.Number) + (german ? " (zurück)" : " (back)");
            }
            steps = MapVoid(body, steps);

            var counts = new int[steps.Count];
            var sig = new StringBuilder(bodyName).Append(german).Append(Plugin.PathSeparator.Value);
            for (int i = 0; i < steps.Count; i++)
            {
                counts[i] = PathCount(body, steps[i].Item);
                sig.Append('|').Append(steps[i].Item).Append(steps[i].Target).Append(':').Append(counts[i]);
            }
            sig.Append(width).Append('#').Append(label).Append(Plugin.HintSignature());
            if (sig.ToString() == signature) return;
            signature = sig.ToString();
            Rebuild(steps, counts, width, german, label);
        }

        void HandleHotkeys()
        {
            if (!bg || !bg.enabled) return;
            if (Plugin.SwitchViewKey.Value.IsDown())
            {
                rarityView = !rarityView;
                signature = null;
                nextRefresh = 0f;
                return;
            }
            if (rarityView) return;
            bool changed = false;
            if (Plugin.BackPageKey.Value.IsDown())
            {
                if (back < history.Count) { back++; changed = true; }
            }
            else if (back > 0)
            {
                // Looking at an older page: the skip-page key goes forward again, swapping is off.
                if (Plugin.SkipPageKey.Value.IsDown()) { back--; changed = true; }
            }
            else if (shownSteps == null) return;
            else if (Plugin.SkipItemKey.Value.IsDown() && currentItem != null)
            {
                // Swap: the item is gone for this run, the free slot gets refilled.
                skipped.Add(currentItem);
                foreach (var n in NormalsOf(currentItem)) skipped.Add(n); // shown as its void version
                if (genPage != null) genPage.RemoveAll(st => skipped.Contains(st.Item));
                changed = true;
            }
            else if (Plugin.SkipPageKey.Value.IsDown())
            {
                if (onStartPage) skipStart = true;
                else if (genPage != null)
                {
                    foreach (var st in genPage) { skipped.Add(st.Item); lastPageItems.Add(st.Item); }
                    genPage = null;
                }
                changed = true;
            }
            if (changed) { signature = null; nextRefresh = 0f; }
        }

        static bool Complete(CharacterBody body, List<PathStep> steps)
        {
            // Optional (red/boss) steps don't block - unless the page has nothing else.
            bool onlyOptional = steps.TrueForAll(st => st.Optional);
            foreach (var st in steps)
                if ((onlyOptional || !st.Optional) && PathCount(body, st.Item) < st.Target) return false;
            return true;
        }

        /// <summary>
        /// Builds a page from item roles so it isn't eight damage items: a mix of offense,
        /// defense/sustain and one utility/mobility slot. Later pages lean on defense + sustain
        /// (config LatePageFocus). Items are scored by community rating, your survivor's rating,
        /// synergies with what you hold and the survivor's own Path pages; items from the last
        /// page are pushed back so pages don't repeat. Red/boss items are optional (max 2).
        /// The page stays until done; skipped items leave a slot that gets refilled.
        /// </summary>
        List<PathStep> GeneratedPage(CharacterBody body, string bodyName, bool german, List<List<PathStep>> filePages)
        {
            if (genBody != bodyName)
            {
                if (genBody != null) { history.Clear(); back = 0; } // other survivor mid-run
                genPage = null; genNumber = 1; genBody = bodyName; lastPageItems.Clear(); pageBeforeItems.Clear();
            }
            if (genPage != null && genPage.Count > 0 && Complete(body, genPage))
            {
                pageBeforeItems.Clear();
                pageBeforeItems.UnionWith(lastPageItems);
                lastPageItems.Clear();
                foreach (var st in genPage) lastPageItems.Add(st.Item);
                genPage = null;
            }
            if (genPage != null && genPage.Count >= GenSize) return genPage;
            if (genPage == null) { genPage = new List<PathStep>(); genNumber++; }

            System.Func<string, int> countOf = n => PickerPatches.PlayerCount(body, n);
            var profile = BuildProfile.Of(body);
            var planned = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int pg = 1; pg < filePages.Count; pg++) foreach (var st in filePages[pg]) planned.Add(st.Item);
            var onPage = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var st in genPage) onPage.Add(st.Item);

            var names = new HashSet<string>(Plugin.Data.CandidateItems(bodyName), System.StringComparer.OrdinalIgnoreCase);
            names.UnionWith(Roles.Items);
            var cands = new List<Cand>();
            foreach (var name in names)
            {
                if (skipped.Contains(name) || onPage.Contains(name)) continue;
                var ii = ItemCatalog.FindItemIndex(name);
                if (ii == ItemIndex.None) continue; // items only, equipment doesn't stack
                var def = ItemCatalog.GetItemDef(ii);
                if (!def || def.tier == ItemTier.NoTier || def.tier == ItemTier.Lunar || def.hidden) continue;
                if (Run.instance && !Run.instance.IsItemAvailable(ii)) continue;
                if (HeldVoid(body, name) != null) continue; // turns into its void version - that one counts
                var advice = Plugin.Data.Get(bodyName, name, countOf, german, profile);
                if (advice.Tier < 0) continue; // bad for this survivor or would convert your items
                int have = countOf(name);
                int cap = Roles.Cap(name);
                if (have >= cap) continue;
                var info = Roles.Get(name, advice.Tier);
                bool rare = def.tier == ItemTier.Tier3 || def.tier == ItemTier.Boss ||
                            def.tier == ItemTier.VoidTier3 || def.tier == ItemTier.VoidBoss;
                if (rare) continue; // red/boss items rarely drop - not part of the path
                int add = def.tier == ItemTier.Tier2 || def.tier == ItemTier.VoidTier2 ? 3 : 5;
                // Rare items rarely drop, so they only get in when they are clearly better.
                int score = info.Consensus * 10 + advice.Tier * 8 + advice.SynergyReasons.Count * 5 + advice.Bonus
                          + (planned.Contains(name) ? 6 : 0) - Mathf.Min(have, 10)
                          - (rare ? 15 : 0)
                          - (lastPageItems.Contains(name) ? 20 : pageBeforeItems.Contains(name) ? 10 : 0);
                cands.Add(new Cand
                {
                    Role = info.Role, Rare = rare, Score = score,
                    Step = new PathStep { Item = name, Target = Mathf.Min(have + add, cap), Optional = rare }
                });
            }
            cands.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Quotas: offense (D/P), defense (F: block/armor/barrier), max health/shield (H: against
            // one-shots), sustain (S: healing), utility/mobility (U/M).
            bool late = genNumber >= 3;
            string focus = (Plugin.LatePageFocus.Value ?? "sustain").ToLowerInvariant();
            int off, fdef, hp = 1, sus, util = 1;
            if (focus == "damage") { off = 5; fdef = 1; hp = 0; sus = 1; }
            else if (focus == "balanced" || !late) { off = 4; fdef = 1; sus = 1; }
            else { off = 3; fdef = 1; sus = 2; } // more defense + sustain than damage on later pages
            int rareLeft = 2;
            var rareRoles = new HashSet<char>();
            foreach (var st in genPage)
            {
                char r = Roles.Get(st.Item, 0).Role;
                if (r == 'D' || r == 'P') off--; else if (r == 'F') fdef--; else if (r == 'H') hp--; else if (r == 'S') sus--; else util--;
                if (st.Optional) { rareLeft--; rareRoles.Add(r); }
            }

            System.Func<System.Func<char, bool>, int, bool> take = (match, n) =>
            {
                for (int i = 0; i < cands.Count && n > 0 && genPage.Count < GenSize; i++)
                {
                    var c = cands[i];
                    if (c.Used || !match(c.Role)) continue;
                    if (c.Rare && (rareLeft <= 0 || rareRoles.Contains(c.Role))) continue;
                    if (ConflictsWithPage(c.Step.Item)) continue;
                    c.Used = true;
                    if (c.Rare) { rareLeft--; rareRoles.Add(c.Role); }
                    genPage.Add(c.Step);
                    n--;
                }
                return true;
            };
            take(r => r == 'F', fdef);
            take(r => r == 'H', hp);
            take(r => r == 'S', sus);
            take(r => r == 'U' || r == 'M', util);
            take(r => r == 'D' || r == 'P', off);
            take(r => true, GenSize); // fill up if a role had no candidates

            if (genPage.Count == 0) { genPage = null; return null; }
            // Show offense first, then defense, so the page reads like a plan.
            genPage.Sort((a, b) => RoleOrder(Roles.Get(a.Item, 0).Role).CompareTo(RoleOrder(Roles.Get(b.Item, 0).Role)));
            return genPage;
        }

        /// <summary>
        /// Rarity view: what to take next from a white and from a green Command menu, best first.
        /// Same scoring as the path; each row has defense, healing and at most n-3 damage items.
        /// </summary>
        void RefreshRarity(CharacterBody body, string bodyName, bool german, float width)
        {
            shownSteps = null;
            currentItem = null;
            System.Func<string, int> countOf = n => PickerPatches.PlayerCount(body, n);
            var profile = BuildProfile.Of(body);
            var names = new HashSet<string>(Plugin.Data.CandidateItems(bodyName), System.StringComparer.OrdinalIgnoreCase);
            names.UnionWith(Roles.Items);
            var white = new List<KeyValuePair<int, string>>();
            var green = new List<KeyValuePair<int, string>>();
            foreach (var name in names)
            {
                var ii = ItemCatalog.FindItemIndex(name);
                if (ii == ItemIndex.None) continue;
                var def = ItemCatalog.GetItemDef(ii);
                if (!def || def.hidden || (def.tier != ItemTier.Tier1 && def.tier != ItemTier.Tier2)) continue;
                if (Run.instance && !Run.instance.IsItemAvailable(ii)) continue;
                if (HeldVoid(body, name) != null) continue;
                var advice = Plugin.Data.Get(bodyName, name, countOf, german, profile);
                if (advice.Tier < 0) continue;
                int have = countOf(name);
                if (have >= Roles.Cap(name)) continue;
                var entry = new KeyValuePair<int, string>(Roles.Score(name, advice, have), name);
                (def.tier == ItemTier.Tier1 ? white : green).Add(entry);
            }
            white.Sort((a, b) => b.Key.CompareTo(a.Key));
            green.Sort((a, b) => b.Key.CompareTo(a.Key));
            int n = Mathf.Clamp(Plugin.RarityRowSize.Value, 3, 8);
            var rows = new List<List<string>>
            {
                Roles.TopMixed(white, kv => kv.Value, n, n - 3).ConvertAll(kv => kv.Value),
                Roles.TopMixed(green, kv => kv.Value, n, n - 3).ConvertAll(kv => kv.Value),
            };

            var sig = new StringBuilder("rarity").Append(bodyName).Append(german).Append(width).Append(Plugin.HintSignature());
            foreach (var r in rows) foreach (var it in r) sig.Append('|').Append(it).Append(countOf(it));
            if (sig.ToString() == signature) return;
            signature = sig.ToString();

            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            pendingIcons.Clear();
            var title = MakeTitle();
            Ui.Fixed(title.rectTransform, new Vector2(Pad, -Pad), new Vector2(width - Pad * 2, titleH));
            title.text = german ? "Nach Seltenheit:" : "By rarity:";

            float rowH = Icon + CountH, y = -Pad - titleH - 2f, inner = width - Pad * 2;
            var colors = new[] { new Color(0.92f, 0.92f, 0.92f), new Color(0.47f, 0.86f, 0.35f) };
            for (int r = 0; r < rows.Count; r++)
            {
                var row = new List<RectTransform>();
                var dot = Ui.MakeImage(content, "Rarity" + r, Sprites.Dot);
                dot.color = colors[r];
                Ui.Fixed(dot.rectTransform, new Vector2(0f, y - 4f), new Vector2(10f, 10f));
                row.Add(dot.rectTransform);
                float x = 16f;
                for (int i = 0; i < rows[r].Count; i++)
                {
                    var step = new PathStep { Item = rows[r][i], Target = -1 };
                    Step(step, countOf(step.Item), r * 10 + i, i == 0, x, y, row);
                    x += Icon + Gap + 4f;
                }
                CenterRow(row, x - Gap - 4f, inner);
                y -= rowH + Gap + 2f;
            }
            FinishLayout(width, -y + Pad - Gap - 2f, german);
        }

        static int RoleOrder(char r) => r == 'D' || r == 'P' ? 0 : r == 'F' || r == 'H' ? 1 : r == 'S' ? 2 : 3;

        // A void item and the normal item it converts never go on the same page.
        bool ConflictsWithPage(string item)
        {
            var partners = VoidPartners(item);
            foreach (var st in genPage) if (partners.Contains(st.Item)) return true;
            return false;
        }

        static Dictionary<string, HashSet<string>> voidPairs;
        static HashSet<string> VoidPartners(string item)
        {
            if (voidPairs == null)
            {
                voidPairs = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
                if (DLC1Content.ItemRelationshipTypes.ContagiousItem)
                    foreach (var pair in ItemCatalog.GetItemPairsForRelationship(DLC1Content.ItemRelationshipTypes.ContagiousItem))
                    {
                        if (!pair.itemDef1 || !pair.itemDef2) continue;
                        Link(pair.itemDef1.name, pair.itemDef2.name);
                        Link(pair.itemDef2.name, pair.itemDef1.name);
                    }
            }
            return voidPairs.TryGetValue(item, out var set) ? set : Empty;
        }

        static readonly HashSet<string> Empty = new HashSet<string>();
        static void Link(string a, string b)
        {
            if (!voidPairs.TryGetValue(a, out var set))
                voidPairs[a] = set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            set.Add(b);
        }

        static string PageLabel(bool german, string number) =>
            (german ? "Build-Pfad" : "Build path") + (string.IsNullOrEmpty(number) ? "" : " " + number);

        // ---- Void items ----
        // Once you hold a void item, its normal item turns into it on pickup. So the path shows the
        // void item instead, and both stacks count together.
        static Dictionary<string, string> voidOf;          // normal -> void
        static Dictionary<string, List<string>> normalsOf; // void -> normals
        static void EnsureVoidMaps()
        {
            if (voidOf != null) return;
            voidOf = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            normalsOf = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
            if (!DLC1Content.ItemRelationshipTypes.ContagiousItem) return;
            foreach (var pair in ItemCatalog.GetItemPairsForRelationship(DLC1Content.ItemRelationshipTypes.ContagiousItem))
            {
                if (!pair.itemDef1 || !pair.itemDef2) continue;
                voidOf[pair.itemDef1.name] = pair.itemDef2.name;
                if (!normalsOf.TryGetValue(pair.itemDef2.name, out var list))
                    normalsOf[pair.itemDef2.name] = list = new List<string>();
                list.Add(pair.itemDef1.name);
            }
        }

        static readonly List<string> NoNames = new List<string>();
        static List<string> NormalsOf(string item)
        {
            EnsureVoidMaps();
            return item != null && normalsOf.TryGetValue(item, out var list) ? list : NoNames;
        }

        /// <summary>The void version of this item if you hold it, else null.</summary>
        static string HeldVoid(CharacterBody body, string item)
        {
            EnsureVoidMaps();
            return voidOf.TryGetValue(item, out var v) && PickerPatches.PlayerCount(body, v) > 0 ? v : null;
        }

        /// <summary>Item count for the path: normal + void stacks together once the void item is held.</summary>
        static int PathCount(CharacterBody body, string item)
        {
            int c = PickerPatches.PlayerCount(body, item);
            string v = HeldVoid(body, item);
            if (v != null) return c + PickerPatches.PlayerCount(body, v);
            if (c > 0) foreach (var n in NormalsOf(item)) c += PickerPatches.PlayerCount(body, n);
            return c;
        }

        /// <summary>Shows normal items as their void version once you hold it (merging duplicates).</summary>
        static List<PathStep> MapVoid(CharacterBody body, List<PathStep> steps)
        {
            var result = new List<PathStep>(steps.Count);
            foreach (var st in steps)
            {
                string item = HeldVoid(body, st.Item) ?? st.Item;
                var same = result.Find(r => string.Equals(r.Item, item, System.StringComparison.OrdinalIgnoreCase));
                if (same != null) { same.Target = Mathf.Max(same.Target, st.Target); continue; }
                result.Add(new PathStep { Item = item, Target = st.Target, Optional = st.Optional });
            }
            return result;
        }

        // ---- State file: skips and pages survive a game restart (e.g. with ProperSave) ----
        static string StatePath => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "ItemRecommender.state.txt");

        static string StepsText(List<PathStep> steps) =>
            steps == null ? "" : string.Join(",", steps.ConvertAll(st => $"{st.Item}:{st.Target}:{(st.Optional ? 1 : 0)}"));

        static List<PathStep> ParseSteps(string text)
        {
            var list = new List<PathStep>();
            foreach (var part in text.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(':');
                if (f.Length < 3 || !int.TryParse(f[1], out int target)) continue;
                list.Add(new PathStep { Item = f[0], Target = target, Optional = f[2] == "1" });
            }
            return list;
        }

        void SaveState(string bodyName)
        {
            if (!Run.instance) return;
            var sb = new StringBuilder();
            sb.Append("seed=").Append(Run.instance.seed).Append('\n');
            sb.Append("body=").Append(bodyName).Append('\n');
            sb.Append("skipStart=").Append(skipStart ? 1 : 0).Append('\n');
            sb.Append("gen=").Append(genNumber).Append('\n');
            sb.Append("skipped=").Append(string.Join(",", skipped)).Append('\n');
            sb.Append("last=").Append(string.Join(",", lastPageItems)).Append('\n');
            sb.Append("before=").Append(string.Join(",", pageBeforeItems)).Append('\n');
            if (genPage != null) sb.Append("page=").Append(StepsText(genPage)).Append('\n');
            if (currentNumber != null) sb.Append("cur=").Append(currentNumber).Append('|').Append(StepsText(currentSteps)).Append('\n');
            foreach (var h in history) sb.Append("hist=").Append(h.Number).Append('|').Append(StepsText(h.Steps)).Append('\n');
            string text = sb.ToString();
            if (text == savedState) return;
            savedState = text;
            try { System.IO.File.WriteAllText(StatePath, text); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Could not save build path state: {e.Message}"); }
        }

        void LoadState(string bodyName)
        {
            try
            {
                if (!Run.instance || !System.IO.File.Exists(StatePath)) return;
                var kv = new List<KeyValuePair<string, string>>();
                foreach (var line in System.IO.File.ReadAllLines(StatePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) kv.Add(new KeyValuePair<string, string>(line.Substring(0, eq), line.Substring(eq + 1)));
                }
                string Get(string key) { foreach (var p in kv) if (p.Key == key) return p.Value; return null; }
                if (Get("seed") != Run.instance.seed.ToString() || Get("body") != bodyName) return;

                System.Func<string, string[]> names = v => (v ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                genBody = bodyName;
                skipStart = Get("skipStart") == "1";
                if (int.TryParse(Get("gen"), out int g)) genNumber = g;
                skipped.UnionWith(names(Get("skipped")));
                lastPageItems.UnionWith(names(Get("last")));
                pageBeforeItems.UnionWith(names(Get("before")));
                string page = Get("page");
                if (page != null) genPage = ParseSteps(page);
                foreach (var p in kv)
                {
                    if (p.Key != "hist" && p.Key != "cur") continue;
                    int bar = p.Value.IndexOf('|');
                    if (bar < 0) continue;
                    var snap = new Snapshot { Number = p.Value.Substring(0, bar), Steps = ParseSteps(p.Value.Substring(bar + 1)) };
                    if (p.Key == "hist") history.Add(snap);
                    else { currentNumber = snap.Number; currentSteps = snap.Steps; }
                }
                Plugin.Log.LogInfo($"Build path state restored: page {currentNumber}, {skipped.Count} skipped, {history.Count} older pages");
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Could not read build path state: {e.Message}"); }
        }

        sealed class Cand
        {
            public char Role;
            public bool Rare, Used;
            public int Score;
            public PathStep Step;
        }

        static List<List<PathStep>> Paginate(List<List<PathStep>> lines)
        {
            var pages = new List<List<PathStep>>();
            if (lines == null) return pages;
            foreach (var line in lines)
                for (int i = 0; i < line.Count; i += PageSize)
                    pages.Add(line.GetRange(i, Mathf.Min(PageSize, line.Count - i)));
            return pages;
        }

        void Rebuild(List<PathStep> steps, int[] counts, float width, bool german, string label)
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            pendingIcons.Clear();

            int current = -1;
            for (int i = 0; i < steps.Count; i++)
                if (counts[i] < steps[i].Target) { current = i; break; }
            shownSteps = steps;
            currentItem = current >= 0 ? steps[current].Item : null;

            var title = MakeTitle();
            Ui.Fixed(title.rectTransform, new Vector2(Pad, -Pad), new Vector2(width - Pad * 2, titleH));
            title.text = current < 0
                ? $"{label}: {(german ? "fertig!" : "done!")}"
                : $"{label}:";

            // Lay out steps in rows, then centre each row like the rest of the panel.
            float rowH = Icon + CountH;
            float x = 0f, y = -Pad - titleH - 2f, inner = width - Pad * 2;
            var row = new List<RectTransform>();
            float rowWidth = 0f;
            for (int i = 0; i < steps.Count; i++)
            {
                if (i > 0)
                {
                    if (x + Sep + Icon > inner)
                    {
                        CenterRow(row, rowWidth, inner);
                        row.Clear();
                        x = 0f;
                        y -= rowH + Gap;
                    }
                    else
                    {
                        var sep = Ui.MakeText(content, "Sep" + i, 11f, new Color(0.75f, 0.75f, 0.75f));
                        sep.alignment = TextAlignmentOptions.Center;
                        sep.overflowMode = TextOverflowModes.Overflow;
                        sep.text = Plugin.PathSeparator.Value;
                        Ui.Fixed(sep.rectTransform, new Vector2(x, y), new Vector2(Sep, Icon));
                        row.Add(sep.rectTransform);
                        x += Sep;
                    }
                }
                Step(steps[i], counts[i], i, i == current, x, y, row);
                x += Icon + Gap;
                rowWidth = x - Gap;
            }
            CenterRow(row, rowWidth, inner);

            FinishLayout(width, -y + rowH + Pad, german);
        }

        void FinishLayout(float width, float height, bool german)
        {
            if (Plugin.ShowSkipHint.Value)
            {
                var hint = Ui.MakeText(content, "Hint", 9f, new Color(0.6f, 0.6f, 0.6f));
                hint.alignment = TextAlignmentOptions.Center;
                string view = Plugin.SwitchViewKey.Value.ToString();
                hint.text = rarityView
                    ? (german ? $"{view}: Build-Pfad" : $"{view}: build path")
                    : german
                        ? $"{view}: Seltenheit · {Plugin.SkipItemKey.Value}: tauschen · {Plugin.BackPageKey.Value}/{Plugin.SkipPageKey.Value}: Seite zurück/vor"
                        : $"{view}: rarity · {Plugin.SkipItemKey.Value}: swap · {Plugin.BackPageKey.Value}/{Plugin.SkipPageKey.Value}: page back/next";
                Ui.Fixed(hint.rectTransform, new Vector2(Pad, -height + Pad - 2f), new Vector2(width - Pad * 2, 12f));
                height += 12f;
            }
            ((RectTransform)transform).sizeDelta = new Vector2(width, height);
            layout.preferredHeight = layout.minHeight = height;
            layout.preferredWidth = width;
            SetVisible(true);
        }

        static void CenterRow(List<RectTransform> row, float rowWidth, float inner)
        {
            float shift = Pad + Mathf.Max(0f, (inner - rowWidth) / 2f);
            foreach (var r in row) r.anchoredPosition += new Vector2(shift, 0f);
        }

        TMP_Text MakeTitle()
        {
            var t = Ui.MakeText(content, "Title", 18f, Color.white);
            t.alignment = TextAlignmentOptions.Center;
            if (headerStyle)
            {
                t.font = headerStyle.font;
                t.fontSharedMaterial = headerStyle.fontSharedMaterial;
                t.fontSize = headerStyle.fontSize;
                t.fontStyle = headerStyle.fontStyle;
                t.color = headerStyle.color;
                t.characterSpacing = headerStyle.characterSpacing;
            }
            return t;
        }

        void Step(PathStep step, int have, int index, bool isCurrent, float x, float y, List<RectTransform> row)
        {
            bool counter = step.Target < 0; // rarity view: just show how many you have
            bool done = !counter && have >= step.Target;
            var icon = Ui.MakeImage(content, "Icon" + index, null);
            Ui.Fixed(icon.rectTransform, new Vector2(x, y), new Vector2(Icon, Icon));
            icon.color = done ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            icon.enabled = false; // no white box while the icon loads
            SetIcon(step.Item, icon);
            row.Add(icon.rectTransform);

            if (isCurrent)
            {
                var frame = Ui.MakeImage(content, "Frame" + index, Sprites.Frame);
                frame.type = Image.Type.Sliced;
                frame.preserveAspect = false;
                frame.pixelsPerUnitMultiplier = 2.2f;
                frame.color = Palette.Gold;
                Ui.Fixed(frame.rectTransform, new Vector2(x - 2f, y + 2f), new Vector2(Icon + 4f, Icon + 4f));
                row.Add(frame.rectTransform);
            }

            var count = Ui.MakeText(content, "Count" + index, 9f,
                done ? new Color(0.45f, 0.95f, 0.45f) : isCurrent ? Palette.Gold
                : step.Optional ? new Color(0.55f, 0.75f, 1f) : new Color(0.8f, 0.8f, 0.8f));
            count.alignment = TextAlignmentOptions.Center;
            count.overflowMode = TextOverflowModes.Overflow;
            count.text = counter ? $"{have}x" : $"{Mathf.Min(have, step.Target)}/{step.Target}"; // no check mark: the game font may lack it
            Ui.Fixed(count.rectTransform, new Vector2(x - 6f, y - Icon), new Vector2(Icon + 12f, CountH));
            row.Add(count.rectTransform);
        }

        /// <summary>Uses the game's own async icon component (same as the inventory bar).</summary>
        void SetIcon(string internalName, Image target)
        {
            PickupIndex pi = PickupIndex.none;
            var ii = ItemCatalog.FindItemIndex(internalName);
            if (ii != ItemIndex.None) pi = PickupCatalog.FindPickupIndex(ii);
            else
            {
                var ei = EquipmentCatalog.FindEquipmentIndex(internalName);
                if (ei != EquipmentIndex.None) pi = PickupCatalog.FindPickupIndex(ei);
            }
            var def = PickupCatalog.GetPickupDef(pi);
            if (def == null) return;

            if (def.iconSprite)
            {
                target.sprite = def.iconSprite;
                target.enabled = true;
                return;
            }
            var ai = target.gameObject.AddComponent<AsyncImage>();
            ai.renderer = target;
            ai.disableWhileLoading = true;
            ai.OnAssetFoundCallback += _ => { if (target) target.enabled = true; };
            ai.SetAssetOrDirectReference(def);
            ai.LoadAssetReference();
            if (!target.sprite) pendingIcons.Add(ai);
        }
    }
}
