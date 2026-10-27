using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using RoR2;

namespace ItemRecommender
{
    public sealed class Entry
    {
        public int Tier;
        public string De;
        public string En;
    }

    public sealed class Synergy
    {
        public string Have;
        public string Want;
        public int Kind; // +1 = synergy, -1 = rather not, -2 = don't build
        public string De;
        public string En;
    }

    /// <summary>Result for one item in the picker.</summary>
    public sealed class Advice
    {
        // 2 = core, 1 = good, 0 = nothing, -1 = rather not, -2 = don't build
        public int Tier;
        public string Reason;
        public readonly List<string> SynergyReasons = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public int Score => Tier * 10 + SynergyReasons.Count;
    }

    /// <summary>Parses builds.txt and answers "how good is item X for survivor Y right now".</summary>
    public sealed class BuildData
    {
        public const int MinTier = -2, MaxTier = 2;
        const string DefaultSection = "Default";
        const string SynergySection = "Synergy";

        // "A > B", "A <> B", "A !> B", "A !!<> B" ...
        static readonly Regex RuleRegex = new Regex(@"^(.+?)\s*(!{0,2})\s*(<>|>)\s*(.+)$");

        readonly ManualLogSource log;
        readonly string path;
        DateTime loadedStamp = DateTime.MinValue;
        bool validated;

        // section name -> item name -> entry (case-insensitive)
        readonly Dictionary<string, Dictionary<string, Entry>> sections =
            new Dictionary<string, Dictionary<string, Entry>>(StringComparer.OrdinalIgnoreCase);
        readonly List<Synergy> rules = new List<Synergy>();
        // section name -> build path pages; every "Path:" line is one page ("Path: A x10 > B x5")
        readonly Dictionary<string, List<List<PathStep>>> paths =
            new Dictionary<string, List<List<PathStep>>>(StringComparer.OrdinalIgnoreCase);
        static readonly Regex StepRegex = new Regex(@"^(\S+?)(?:\s*[x\*]\s*(\d+))?$", RegexOptions.IgnoreCase);

        public BuildData(string path, ManualLogSource log)
        {
            this.path = path;
            this.log = log;
        }

        /// <summary>Reloads the file if it changed on disk. Cheap to call often.</summary>
        public void ReloadIfChanged()
        {
            DateTime stamp;
            try { stamp = File.GetLastWriteTimeUtc(path); }
            catch { return; }
            if (stamp != loadedStamp)
            {
                loadedStamp = stamp;
                Load();
                validated = false;
            }
            ValidateOnce();
        }

        void Load()
        {
            sections.Clear();
            rules.Clear();
            paths.Clear();
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception e) { log.LogError($"Could not read {path}: {e.Message}"); return; }

            var current = new List<string> { DefaultSection };
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = new List<string>();
                    foreach (var s in line.Substring(1, line.Length - 2).Split(','))
                        if (s.Trim().Length > 0) current.Add(s.Trim());
                    continue;
                }

                if (line.StartsWith("Path:", StringComparison.OrdinalIgnoreCase))
                {
                    var steps = ParsePath(line.Substring(5), n + 1);
                    foreach (var sec in current)
                    {
                        if (!paths.TryGetValue(sec, out var pages)) paths[sec] = pages = new List<List<PathStep>>();
                        if (steps.Count > 0) pages.Add(steps);
                    }
                    continue;
                }

                string[] parts = line.Split('|');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                if (current.Count == 1 && current[0].Equals(SynergySection, StringComparison.OrdinalIgnoreCase))
                {
                    ParseRule(parts, n + 1);
                    continue;
                }

                if (parts.Length < 2 || !int.TryParse(parts[1], out int tier))
                {
                    log.LogWarning($"builds.txt line {n + 1}: expected 'Item | Tier | DE | EN'");
                    continue;
                }
                var entry = new Entry
                {
                    Tier = Math.Max(MinTier, Math.Min(MaxTier, tier)),
                    De = parts.Length > 2 ? parts[2] : "",
                    En = parts.Length > 3 ? parts[3] : (parts.Length > 2 ? parts[2] : "")
                };
                foreach (var sec in current)
                {
                    if (!sections.TryGetValue(sec, out var dict))
                        sections[sec] = dict = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                    dict[parts[0]] = entry;
                }
            }
            log.LogInfo($"Loaded builds: {sections.Count} sections, {rules.Count} synergy rules, {paths.Count} build paths");
        }

        List<PathStep> ParsePath(string text, int lineNo)
        {
            var steps = new List<PathStep>();
            foreach (var raw in text.Split('>', '<', ','))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                var m = StepRegex.Match(part);
                if (!m.Success)
                {
                    log.LogWarning($"builds.txt line {lineNo}: expected 'Path: Item x10 > Item x5 > ...', got '{part}'");
                    continue;
                }
                int target = m.Groups[2].Success ? Math.Max(1, int.Parse(m.Groups[2].Value)) : 1;
                steps.Add(new PathStep { Item = m.Groups[1].Value, Target = target });
            }
            return steps;
        }

        /// <summary>Build path pages for the survivor, falling back to Default. Only an empty "Path:" disables it.</summary>
        public List<List<PathStep>> GetPath(string bodyName)
        {
            if (bodyName != null && paths.TryGetValue(bodyName, out var p)) return p;
            return paths.TryGetValue(DefaultSection, out p) ? p : null;
        }

        void ParseRule(string[] parts, int lineNo)
        {
            var m = RuleRegex.Match(parts[0]);
            if (!m.Success)
            {
                log.LogWarning($"builds.txt line {lineNo}: expected 'A > B', 'A <> B', 'A !> B' or 'A !!> B'");
                return;
            }
            string a = m.Groups[1].Value.Trim(), b = m.Groups[4].Value.Trim();
            int kind = m.Groups[2].Value.Length == 0 ? 1 : -m.Groups[2].Value.Length;
            bool both = m.Groups[3].Value == "<>";
            string de = parts.Length > 1 ? parts[1] : "";
            string en = parts.Length > 2 ? parts[2] : de;
            rules.Add(new Synergy { Have = a, Want = b, Kind = kind, De = de, En = en });
            if (both) rules.Add(new Synergy { Have = b, Want = a, Kind = kind, De = de, En = en });
        }

        /// <summary>Warns about typos in item names once the catalogs exist.</summary>
        void ValidateOnce()
        {
            if (validated || ItemCatalog.itemCount == 0) return;
            validated = true;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in sections.Values) foreach (var k in d.Keys) names.Add(k);
            foreach (var s in rules) { names.Add(s.Have); names.Add(s.Want); }
            foreach (var p in paths.Values) foreach (var page in p) foreach (var step in page) names.Add(step.Item);
            foreach (var name in names)
                if (ItemCatalog.FindItemIndex(name) == ItemIndex.None &&
                    EquipmentCatalog.FindEquipmentIndex(name) == EquipmentIndex.None)
                    log.LogWarning($"builds.txt: unknown item/equipment name '{name}'");
        }

        /// <summary>Static rating from the file (survivor section first, then Default).</summary>
        Entry Lookup(string bodyName, string itemName)
        {
            if (bodyName != null && sections.TryGetValue(bodyName, out var bodyDict) &&
                bodyDict.TryGetValue(itemName, out var e))
                return e;
            if (sections.TryGetValue(DefaultSection, out var def) && def.TryGetValue(itemName, out e))
                return e;
            return null;
        }

        int TierOf(string bodyName, string itemName) => Lookup(bodyName, itemName)?.Tier ?? 0;

        public Advice Get(string bodyName, string itemName, Func<string, int> countOf, bool german)
        {
            var advice = new Advice();
            var entry = Lookup(bodyName, itemName);
            int tier = entry?.Tier ?? 0;
            if (entry != null) advice.Reason = german ? entry.De : entry.En;

            int worst = 0;
            foreach (var s in rules)
            {
                if (!s.Want.Equals(itemName, StringComparison.OrdinalIgnoreCase)) continue;
                if (countOf(s.Have) <= 0) continue;
                string line = $"{DisplayName(s.Have)}: {(german ? s.De : s.En)}";
                if (s.Kind > 0) advice.SynergyReasons.Add(line);
                else { advice.Warnings.Add(line); worst = Math.Min(worst, s.Kind); }
            }

            worst = Math.Min(worst, VoidWarnings(bodyName, itemName, tier, countOf, german, advice, out int floor));

            if (advice.SynergyReasons.Count > 0 && tier >= 0)
                tier = Math.Min(MaxTier, tier + 1);
            tier = Math.Max(tier, floor);
            if (worst < 0)
                tier = Math.Min(tier, worst);
            advice.Tier = tier;
            return advice;
        }

        /// <summary>
        /// Void items corrupt ("convert") their normal counterparts. Uses the game's own
        /// relationship table, so it also covers modded void items.
        /// </summary>
        int VoidWarnings(string bodyName, string itemName, int ownTier, Func<string, int> countOf, bool german, Advice advice, out int floor)
        {
            floor = MinTier;
            var index = ItemCatalog.FindItemIndex(itemName);
            if (index == ItemIndex.None || DLC1Content.ItemRelationshipTypes.ContagiousItem == null) return 0;

            int worst = 0;
            foreach (var pair in ItemCatalog.GetItemPairsForRelationship(DLC1Content.ItemRelationshipTypes.ContagiousItem))
            {
                if (!pair.itemDef1 || !pair.itemDef2) continue;
                string normal = pair.itemDef1.name, voidItem = pair.itemDef2.name;

                if (pair.itemDef2.itemIndex == index)
                {
                    // Offered: void item. Do I hold the normal item it would eat?
                    int have = countOf(normal);
                    if (have <= 0) continue;
                    int normalTier = TierOf(bodyName, normal);
                    if (ownTier > normalTier)
                    {
                        advice.SynergyReasons.Add(german
                            ? $"Ersetzt deine {have}x {DisplayName(normal)} – hier lohnt es sich."
                            : $"Replaces your {have}x {DisplayName(normal)} – worth it here.");
                        continue;
                    }
                    int level = normalTier >= 1 ? -2 : -1;
                    advice.Warnings.Add(german
                        ? $"Wandelt deine {have}x {DisplayName(normal)} in {DisplayName(voidItem)} um!"
                        : $"Converts your {have}x {DisplayName(normal)} into {DisplayName(voidItem)}!");
                    worst = Math.Min(worst, level);
                }
                else if (pair.itemDef1.itemIndex == index && countOf(voidItem) > 0)
                {
                    // Offered: normal item, but I already hold its void version -> it turns into another
                    // stack of the void item, so it's worth what the void item is worth (at least "good").
                    int have = countOf(voidItem);
                    int voidTier = TierOf(bodyName, voidItem);
                    if (voidTier < 0)
                    {
                        advice.Warnings.Add(german
                            ? $"Wird zu {DisplayName(voidItem)} – das lohnt sich hier nicht."
                            : $"Turns into {DisplayName(voidItem)} – not worth it here.");
                        worst = Math.Min(worst, voidTier);
                        continue;
                    }
                    advice.SynergyReasons.Add(german
                        ? $"Wird zu {DisplayName(voidItem)} (du hast {have}x) – mehr Stacks!"
                        : $"Becomes {DisplayName(voidItem)} (you have {have}x) – more stacks!");
                    floor = Math.Max(floor, Math.Max(1, voidTier));
                }
            }
            return worst;
        }

        public static string DisplayName(string internalName)
        {
            var ii = ItemCatalog.FindItemIndex(internalName);
            if (ii != ItemIndex.None) return Language.GetString(ItemCatalog.GetItemDef(ii).nameToken);
            var ei = EquipmentCatalog.FindEquipmentIndex(internalName);
            if (ei != EquipmentIndex.None) return Language.GetString(EquipmentCatalog.GetEquipmentDef(ei).nameToken);
            return internalName;
        }
    }
}
