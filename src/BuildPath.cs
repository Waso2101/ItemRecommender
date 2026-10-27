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
        // Generated page once every page from the file is done; kept until it is complete too.
        List<PathStep> bonusSteps;
        string bonusBody;
        int bonusRound;

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

            // Show the first page that isn't finished yet. When all are done, generate bonus pages
            // from the ratings and your current synergies (or stay on the last page if that's off).
            bool german = Plugin.German();
            int page = -1;
            for (int pg = 0; pg < pages.Count && page < 0; pg++)
                if (!Complete(body, pages[pg])) page = pg;
            List<PathStep> steps;
            string label = german ? "Build-Pfad" : "Build path";
            if (page >= 0)
            {
                steps = pages[page];
                if (pages.Count > 1) label += $" {page + 1}/{pages.Count}";
            }
            else if (Plugin.BonusPages.Value && (steps = BonusPage(body, bodyName, german)) != null)
                label = (german ? "Bonus-Pfad " : "Bonus path ") + bonusRound;
            else
            {
                steps = pages[pages.Count - 1];
                if (pages.Count > 1) label += $" {pages.Count}/{pages.Count}";
            }

            var counts = new int[steps.Count];
            var sig = new StringBuilder(bodyName).Append(german).Append(Plugin.PathSeparator.Value);
            for (int i = 0; i < steps.Count; i++)
            {
                counts[i] = PickerPatches.PlayerCount(body, steps[i].Item);
                sig.Append('|').Append(steps[i].Item).Append(steps[i].Target).Append(':').Append(counts[i]);
            }
            float width = inLayoutGroup && objective && objective.rect.width > 100f ? objective.rect.width : DefaultWidth;
            sig.Append(width).Append('#').Append(label);
            if (sig.ToString() == signature) return;
            signature = sig.ToString();
            Rebuild(steps, counts, width, german, label);
        }

        static bool Complete(CharacterBody body, List<PathStep> steps)
        {
            foreach (var st in steps)
                if (PickerPatches.PlayerCount(body, st.Item) < st.Target) return false;
            return true;
        }

        /// <summary>
        /// Best-rated items for this survivor right now (ratings + synergies with what you hold),
        /// with targets above your current count. Frozen until complete, then a new one is made.
        /// </summary>
        List<PathStep> BonusPage(CharacterBody body, string bodyName, bool german)
        {
            if (bonusSteps != null && bonusBody == bodyName && !Complete(body, bonusSteps)) return bonusSteps;

            System.Func<string, int> countOf = n => PickerPatches.PlayerCount(body, n);
            var picks = new List<KeyValuePair<int, PathStep>>();
            foreach (var name in Plugin.Data.CandidateItems(bodyName))
            {
                var ii = ItemCatalog.FindItemIndex(name);
                if (ii == ItemIndex.None) continue; // items only, equipment doesn't stack
                var def = ItemCatalog.GetItemDef(ii);
                if (!def || def.tier == ItemTier.NoTier || def.tier == ItemTier.Lunar) continue;
                if (Run.instance && !Run.instance.IsItemAvailable(ii)) continue;
                var advice = Plugin.Data.Get(bodyName, name, countOf, german);
                if (advice.Tier < 1) continue;
                int have = countOf(name);
                int add = def.tier == ItemTier.Tier1 || def.tier == ItemTier.VoidTier1 ? 5
                        : def.tier == ItemTier.Tier2 || def.tier == ItemTier.VoidTier2 ? 3 : 1;
                // Higher score first; among equals prefer items you don't have much of yet.
                int score = advice.Score * 100 - Mathf.Min(have, 99);
                picks.Add(new KeyValuePair<int, PathStep>(score, new PathStep { Item = name, Target = have + add }));
            }
            if (picks.Count == 0) return null;
            picks.Sort((a, b) => b.Key.CompareTo(a.Key));
            bonusSteps = new List<PathStep>();
            for (int i = 0; i < picks.Count && bonusSteps.Count < PageSize - 2; i++) bonusSteps.Add(picks[i].Value);
            bonusBody = bodyName;
            bonusRound++;
            return bonusSteps;
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

            float height = -y + rowH + Pad;
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
            bool done = have >= step.Target;
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
                done ? new Color(0.45f, 0.95f, 0.45f) : isCurrent ? Palette.Gold : new Color(0.8f, 0.8f, 0.8f));
            count.alignment = TextAlignmentOptions.Center;
            count.overflowMode = TextOverflowModes.Overflow;
            count.text = $"{Mathf.Min(have, step.Target)}/{step.Target}"; // no check mark: the game font may lack it
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
