using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemRecommender
{
    public static class Palette
    {
        public static readonly Color Gold = new Color(1f, 0.80f, 0.15f, 1f);
        public static readonly Color LightGold = new Color(1f, 0.93f, 0.55f, 0.75f);
        public static readonly Color Turquoise = new Color(0.25f, 0.95f, 0.90f, 1f);
        public static readonly Color Orange = new Color(1f, 0.55f, 0.10f, 1f);
        public static readonly Color Red = new Color(0.95f, 0.15f, 0.12f, 1f);
        public static readonly Color Panel = new Color(0.06f, 0.07f, 0.09f, 0.88f);

        public static Color ForTier(int tier) =>
            tier >= 2 ? Gold : tier == 1 ? LightGold : tier == -1 ? Orange : Red;
    }

    /// <summary>Frame, star / X and synergy dot drawn on top of one picker button.</summary>
    public sealed class Overlay : MonoBehaviour
    {
        Image tint, frame, star, cross, dot;
        Pulse pulse;

        // Tooltip text we appended last time, so we can strip it when the button is reused.
        public string AppendedTooltip;

        public static Overlay GetOrCreate(GameObject button)
        {
            var existing = button.transform.Find("ItemRecommenderOverlay");
            if (existing) return existing.GetComponent<Overlay>();

            var go = new GameObject("ItemRecommenderOverlay", typeof(RectTransform));
            go.transform.SetParent(button.transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.Place((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var o = go.AddComponent<Overlay>();
            o.tint = Ui.MakeImage(go.transform, "Tint", Sprites.Fill);
            Ui.Place(o.tint.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            o.frame = Ui.MakeImage(go.transform, "Frame", Sprites.Frame);
            o.frame.type = Image.Type.Sliced;
            o.frame.preserveAspect = false;
            Ui.Place(o.frame.rectTransform, Vector2.zero, Vector2.one, new Vector2(-3, -3), new Vector2(3, 3));

            o.dot = Ui.MakeImage(go.transform, "SynergyDot", Sprites.Dot);
            Ui.Place(o.dot.rectTransform, new Vector2(-0.06f, 0.70f), new Vector2(0.26f, 1.02f), Vector2.zero, Vector2.zero);

            o.star = Ui.MakeImage(go.transform, "Star", Sprites.Star);
            Ui.Place(o.star.rectTransform, new Vector2(0.56f, 0.56f), new Vector2(1.08f, 1.08f), Vector2.zero, Vector2.zero);
            o.pulse = o.star.gameObject.AddComponent<Pulse>();

            o.cross = Ui.MakeImage(go.transform, "Cross", Sprites.Cross);
            Ui.Place(o.cross.rectTransform, new Vector2(0.58f, 0.58f), new Vector2(1.06f, 1.06f), Vector2.zero, Vector2.zero);
            return o;
        }

        public void Show(Advice advice, bool showGoodTier, bool isBest)
        {
            transform.SetAsLastSibling();
            int tier = advice?.Tier ?? 0;
            bool any = tier >= 2 || tier < 0 || (tier == 1 && showGoodTier);
            gameObject.SetActive(any);
            if (!any) return;

            Color c = Palette.ForTier(tier);
            frame.color = c;
            frame.pixelsPerUnitMultiplier = tier == 1 ? 1.8f : 1f; // higher = thinner frame

            star.gameObject.SetActive(tier >= 2);
            star.color = Palette.Gold;
            pulse.enabled = isBest;
            if (!isBest) star.rectTransform.localScale = Vector3.one;

            cross.gameObject.SetActive(tier < 0);
            cross.color = c;
            tint.gameObject.SetActive(tier < 0);
            tint.color = new Color(c.r, c.g, c.b, tier == -2 ? 0.28f : 0.18f);

            dot.gameObject.SetActive(advice.SynergyReasons.Count > 0 && tier > 0);
            dot.color = Palette.Turquoise;
        }
    }

    /// <summary>Explains the markers, docked to the right of the picker window.</summary>
    public sealed class Legend : MonoBehaviour
    {
        const float Width = 300f, RowH = 26f, Pad = 10f;
        RectTransform anchorRect;
        HGTextMeshProUGUI title;
        HGTextMeshProUGUI[] labels;

        public static Legend GetOrCreate(PickupPickerPanel panel)
        {
            var existing = panel.transform.Find("ItemRecommenderLegend");
            if (existing) return existing.GetComponent<Legend>();

            var go = new GameObject("ItemRecommenderLegend", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var l = go.AddComponent<Legend>();
            l.Build(panel.buttonContainer);
            return l;
        }

        void Build(RectTransform anchor)
        {
            anchorRect = anchor;
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Width, Pad * 2 + RowH * 7 + 4f);

            var bg = Ui.MakeImage(transform, "Bg", Sprites.Fill);
            bg.color = Palette.Panel;
            bg.preserveAspect = false;
            Ui.Place(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var border = Ui.MakeImage(transform, "Border", Sprites.Frame);
            border.type = Image.Type.Sliced;
            border.preserveAspect = false;
            border.pixelsPerUnitMultiplier = 2.5f;
            border.color = new Color(1f, 0.80f, 0.15f, 0.5f);
            Ui.Place(border.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            title = Ui.MakeText(transform, "Title", 17f, Palette.Gold);
            Row(title.rectTransform, 0, 0f);

            labels = new HGTextMeshProUGUI[6];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = Ui.MakeText(transform, "Label" + i, 15f, i == 5 ? new Color(0.7f, 0.7f, 0.7f) : Color.white);
                Row(labels[i].rectTransform, i + 1, i == 5 ? 0f : 34f);
            }
            Icon(1, Palette.Gold, 1f, Sprites.Star, Palette.Gold, false);
            Icon(2, Palette.LightGold, 1.8f, null, default, false);
            Icon(3, Palette.Gold, 1f, Sprites.Dot, Palette.Turquoise, true);
            Icon(4, Palette.Orange, 1f, Sprites.Cross, Palette.Orange, false);
            Icon(5, Palette.Red, 1f, Sprites.Cross, Palette.Red, false);
        }

        static float RowTop(int row) => -Pad - RowH * row - (row > 0 ? 4f : 0f);

        static void Row(RectTransform r, int row, float indent)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.offsetMin = new Vector2(Pad + indent, RowTop(row) - RowH);
            r.offsetMax = new Vector2(-Pad, RowTop(row));
        }

        void Icon(int row, Color frameColor, float thin, Sprite badge, Color badgeColor, bool badgeLeft)
        {
            float size = RowH - 4f;
            float y = RowTop(row) - 2f;
            var f = Ui.MakeImage(transform, "IconFrame" + row, Sprites.Frame);
            f.type = Image.Type.Sliced;
            f.preserveAspect = false;
            f.pixelsPerUnitMultiplier = thin * 2.2f;
            f.color = frameColor;
            Ui.Fixed(f.rectTransform, new Vector2(Pad + 2f, y), new Vector2(size, size));
            if (badge == null) return;
            var b = Ui.MakeImage(transform, "IconBadge" + row, badge);
            b.color = badgeColor;
            float bs = size * 0.62f;
            Ui.Fixed(b.rectTransform, new Vector2(Pad + 2f + (badgeLeft ? -3f : size - bs + 3f), y + 3f), new Vector2(bs, bs));
        }

        public void SetTexts(string survivor, bool german)
        {
            title.text = string.IsNullOrEmpty(survivor) ? "ItemRecommender" : $"ItemRecommender – {survivor}";
            string[] t = german
                ? new[] { "Kern-Item – bauen!", "Gute Wahl", "Synergie mit deinen Items", "Eher nicht", "Nicht bauen / zerstört Items", "Hover/Inspect zeigt die Begründung" }
                : new[] { "Core item – build it!", "Good pick", "Synergy with your items", "Rather not", "Don't build / destroys items", "Hover/inspect shows the reason" };
            for (int i = 0; i < labels.Length; i++) labels[i].text = t[i];
        }

        void LateUpdate()
        {
            // Follow the item grid so we stay next to the window even if other mods resize it.
            if (!anchorRect) return;
            var corners = new Vector3[4];
            anchorRect.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
            var parent = (RectTransform)transform.parent;
            Vector3 topRight = parent.InverseTransformPoint(corners[2]);
            transform.localPosition = new Vector3(topRight.x + 48f, topRight.y + 20f, 0f);
        }
    }

    /// <summary>Gentle pulse for the single best pick in the menu.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        void Update()
        {
            float s = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    static class Ui
    {
        public static Image MakeImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        public static HGTextMeshProUGUI MakeText(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<HGTextMeshProUGUI>();
            if (!t.font && HGTextMeshProUGUI.defaultLanguageFont) t.font = HGTextMeshProUGUI.defaultLanguageFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        public static void Place(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Fixed-size element at a top-left offset inside its parent.</summary>
        public static void Fixed(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }
    }

    /// <summary>Sprites are drawn in code so the mod needs no asset bundle.</summary>
    public static class Sprites
    {
        static Sprite frame, star, dot, cross, fill;

        public static Sprite Frame => frame ? frame : (frame = MakeFrame());
        public static Sprite Star => star ? star : (star = MakeStar());
        public static Sprite Dot => dot ? dot : (dot = MakeDot());
        public static Sprite Cross => cross ? cross : (cross = MakeCross());
        public static Sprite Fill => fill ? fill : (fill = MakeFill());

        static Texture2D NewTex(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        static Sprite MakeFill()
        {
            var tex = NewTex(4);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeFrame()
        {
            const int size = 32, b = 4;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    float a = edge < b ? 1f : edge < b + 2 ? 0.35f / (edge - b + 1) : 0f; // soft inner glow
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b + 2, b + 2, b + 2, b + 2));
        }

        static Sprite MakeStar()
        {
            const int size = 64;
            var tex = NewTex(size);
            var outer = StarPolygon(size / 2f, size / 2f, 31f, 13.5f);
            var inner = StarPolygon(size / 2f, size / 2f, 25f, 10.5f);
            var outline = new Color(0.18f, 0.10f, 0f, 1f);
            Supersample(tex, size, (px, py) =>
                Inside(inner, px, py) ? Color.white : Inside(outer, px, py) ? outline : Color.clear);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeCross()
        {
            const int size = 64;
            var tex = NewTex(size);
            var outline = new Color(0.12f, 0f, 0f, 1f);
            Supersample(tex, size, (px, py) =>
            {
                // distance to both diagonals, clipped to a square
                float x = px - size / 2f, y = py - size / 2f;
                if (Mathf.Abs(x) > 26f || Mathf.Abs(y) > 26f) return Color.clear;
                float d = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y)) / 1.4142f;
                return d < 5.5f ? Color.white : d < 9f ? outline : Color.clear;
            });
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeDot()
        {
            const int size = 32;
            var tex = NewTex(size);
            var outline = new Color(0f, 0.12f, 0.12f, 1f);
            Supersample(tex, size, (px, py) =>
            {
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(size / 2f, size / 2f));
                return d < 10f ? Color.white : d < 13f ? outline : Color.clear;
            });
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // 4x4 supersampling for smooth edges.
        static void Supersample(Texture2D tex, int size, System.Func<float, float, Color> sample)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            var c = sample(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                            sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    sum /= 16f;
                    tex.SetPixel(x, y, sum.a > 0 ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear);
                }
            tex.Apply();
        }

        static Vector2[] StarPolygon(float cx, float cy, float rOuter, float rInner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? rOuter : rInner;
                pts[i] = new Vector2(cx + r * Mathf.Cos(ang), cy + r * Mathf.Sin(ang));
            }
            return pts;
        }

        static bool Inside(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > y) != (poly[j].y > y) &&
                    x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
