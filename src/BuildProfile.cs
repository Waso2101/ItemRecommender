using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace ItemRecommender
{
    /// <summary>
    /// What your run already does: debuff types, burn/bleed/stun sources, area damage, on-kill and
    /// on-hit items, crit chance and attack speed. Built from the survivor's skill descriptions plus
    /// every item you hold. Each offered item is then scored against it, e.g. Death Mark gets better
    /// the more debuff types you apply, area items get better when your primary hits one target.
    /// </summary>
    public sealed class BuildProfile
    {
        sealed class Src { public string Item; public string Name; }

        public string Body;
        public Trait Have;
        public bool SingleTargetPrimary;
        public float Crit, AttackSpeedMult = 1f;
        readonly Dictionary<Trait, List<Src>> debuffs = new Dictionary<Trait, List<Src>>();
        readonly List<Src> aoe = new List<Src>(), onKill = new List<Src>(), procs = new List<Src>(),
            burn = new List<Src>(), bleed = new List<Src>(), stun = new List<Src>(),
            wantsDebuffs = new List<Src>(), wantsCrit = new List<Src>(), wantsBurn = new List<Src>(),
            wantsBleed = new List<Src>(), wantsStun = new List<Src>();

        // Survivors whose primary text reads single-target but hits an area (or the other way round).
        static readonly Dictionary<string, bool> PrimaryAreaOverride = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["EngiBody"] = true,   // bouncing grenades explode
        };

        static readonly HashSet<string> logged = new HashSet<string>();

        public static BuildProfile Of(CharacterBody body)
        {
            var p = new BuildProfile();
            if (!body) return p;
            p.Body = BodyCatalog.GetBodyName(body.bodyIndex);
            p.Crit = body.crit;
            p.AttackSpeedMult = body.baseAttackSpeed > 0f ? body.attackSpeed / body.baseAttackSpeed : 1f;

            // Survivor skills
            var sl = body.skillLocator;
            if (sl)
            {
                string primaryText = null;
                foreach (var gs in new[] { sl.primary, sl.secondary, sl.utility, sl.special })
                {
                    var def = gs ? gs.skillDef : null;
                    if (!def) continue;
                    string text = Traits.English(def.skillDescriptionToken);
                    if (gs == sl.primary) primaryText = text;
                    p.Add(Traits.Parse(text), null, Language.GetString(def.skillNameToken), gs == sl.primary);
                }
                if (sl.passiveSkill.enabled)
                    p.Add(Traits.Parse(Traits.English(sl.passiveSkill.skillDescriptionToken)), null,
                          Language.GetString(sl.passiveSkill.skillNameToken), false);
                p.SingleTargetPrimary = primaryText != null &&
                    !(PrimaryAreaOverride.TryGetValue(p.Body ?? "", out bool area) ? area : Traits.HitsArea(primaryText));
            }

            // Items + equipment
            var inv = body.inventory;
            if (inv)
            {
                foreach (var ii in inv.itemAcquisitionOrder)
                {
                    var def = ItemCatalog.GetItemDef(ii);
                    if (!def || def.hidden || inv.GetItemCountPermanent(ii) <= 0) continue;
                    p.Add(Traits.Of(def.name), def.name, Language.GetString(def.nameToken), false);
                }
            }
            if (body.equipmentSlot && body.equipmentSlot.equipmentIndex != EquipmentIndex.None)
            {
                var ed = EquipmentCatalog.GetEquipmentDef(body.equipmentSlot.equipmentIndex);
                if (ed) p.Add(Traits.Of(ed.name), ed.name, Language.GetString(ed.nameToken), false);
            }

            if (p.Body != null && logged.Add(p.Body))
                Plugin.Log.LogInfo($"Survivor analysis [{p.Body}]: primary {(p.SingleTargetPrimary ? "single target" : "hits an area")}, " +
                                   $"debuffs from skills: {string.Join(", ", p.debuffs.Keys.Select(k => Traits.DebuffName(k, false)))}");
            return p;
        }

        void Add(Trait t, string item, string name, bool primary)
        {
            if (t == Trait.None) return;
            Have |= t;
            var src = new Src { Item = item, Name = name };
            foreach (var d in Traits.DebuffTypes(t))
            {
                if (!debuffs.TryGetValue(d, out var list)) debuffs[d] = list = new List<Src>();
                list.Add(src);
            }
            // The primary's area is judged separately (SingleTargetPrimary); other area skills count.
            if ((t & Trait.Aoe) != 0 && !primary) aoe.Add(src);
            if ((t & Trait.OnKill) != 0 && item != null) onKill.Add(src);
            if ((t & Trait.Proc) != 0 && item != null) procs.Add(src);
            if ((t & Trait.Burn) != 0) burn.Add(src);
            if ((t & Trait.Bleed) != 0) bleed.Add(src);
            if ((t & Trait.Stun) != 0) stun.Add(src);
            if ((t & Trait.WantsDebuffs) != 0) wantsDebuffs.Add(src);
            if ((t & Trait.WantsCrit) != 0 && item != null) wantsCrit.Add(src);
            if ((t & Trait.WantsBurn) != 0) wantsBurn.Add(src);
            if ((t & Trait.WantsBleed) != 0) wantsBleed.Add(src);
            if ((t & Trait.WantsStun) != 0) wantsStun.Add(src);
        }

        // Sources other than the item itself and other than items an explicit [Synergy] rule already named.
        static List<Src> Others(List<Src> list, string item, Advice advice) =>
            list.Where(s => s.Item == null ||
                            (!s.Item.Equals(item, StringComparison.OrdinalIgnoreCase) && !advice.RuleHaves.Contains(s.Item))).ToList();

        static string Names(List<Src> list, int max = 3)
        {
            var names = list.Select(s => s.Name).Distinct().ToList();
            string text = string.Join(", ", names.Take(max));
            return names.Count > max ? text + ", …" : text;
        }

        /// <summary>Debuff types you apply without counting this item.</summary>
        List<Trait> DebuffTypesWithout(string item) =>
            debuffs.Where(kv => kv.Value.Any(s => s.Item == null || !s.Item.Equals(item, StringComparison.OrdinalIgnoreCase)))
                   .Select(kv => kv.Key).ToList();

        void Reason(Advice a, int bonus, string de, string en, bool german)
        {
            a.Bonus += bonus;
            a.BuildReasons.Add(german ? de : en);
        }

        void Note(Advice a, int malus, string de, string en, bool german)
        {
            a.Bonus += malus;
            a.Notes.Add(german ? de : en);
        }

        /// <summary>Adds build reasons and a score bonus for one offered item.</summary>
        public void Apply(string item, Advice a, bool german)
        {
            if (Body == null) return;
            var t = Traits.Of(item);
            if (t == Trait.None) return;

            // ---- Debuffs (Death Mark, Noxious Thorn and anything else that cares about debuffs)
            var types = DebuffTypesWithout(item);
            int n = types.Count;
            string typeList = string.Join(", ", types.Select(d => Traits.DebuffName(d, german)));
            if ((t & Trait.WantsDebuffs) != 0)
            {
                if (n >= 3)
                    Reason(a, 12 + 3 * Math.Min(n - 3, 3),
                        $"Dein Build verteilt schon {n} Debuff-Arten ({typeList}) – genau dafür ist dieses Item.",
                        $"Your build already applies {n} debuff types ({typeList}) – exactly what this item wants.", german);
                else if (n == 2)
                    Reason(a, 6, $"2 Debuff-Arten ({typeList}) – noch 1-2 mehr, dann lohnt es sich richtig.",
                        $"2 debuff types ({typeList}) – 1-2 more and it really pays off.", german);
                else
                    Note(a, -4, n == 1 ? $"Bisher nur 1 Debuff-Art ({typeList}) – braucht mehr Debuff-Items."
                                       : "Du verteilst noch keine Debuffs – braucht Debuff-Items.",
                        n == 1 ? $"Only 1 debuff type so far ({typeList}) – needs more debuff items."
                               : "You don't apply debuffs yet – needs debuff items.", german);
            }
            var newTypes = Traits.DebuffTypes(t).Where(d => !types.Contains(d)).ToList();
            if (newTypes.Count > 0)
            {
                string added = string.Join(", ", newTypes.Select(d => Traits.DebuffName(d, german)));
                var wanters = Others(wantsDebuffs, item, a);
                if (wanters.Count > 0)
                    Reason(a, Math.Min(12, 7 * newTypes.Count),
                        $"Neue Debuff-Art ({added}) für {Names(wanters)} – dann {n + newTypes.Count} Arten.",
                        $"New debuff type ({added}) for your {Names(wanters)} – {n + newTypes.Count} types then.", german);
                else if (n >= 2)
                    Reason(a, 3, $"Weitere Debuff-Art ({added}) – {n + newTypes.Count} Arten machen Death Mark / Noxious Thorn stark.",
                        $"Another debuff type ({added}) – {n + newTypes.Count} types make Death Mark / Noxious Thorn strong.", german);
            }

            // ---- Burn / bleed / stun: items that boost an effect you already apply, and the other way round
            Pair(a, t, item, german, Trait.WantsBurn, Trait.Burn, burn, wantsBurn, "Brand", "burn");
            Pair(a, t, item, german, Trait.WantsBleed, Trait.Bleed, bleed, wantsBleed, "Bluten", "bleed");
            Pair(a, t, item, german, Trait.WantsStun, Trait.Stun, stun, wantsStun, "Betäubung", "stun");

            // ---- Crit
            int crit = (int)Math.Round(Crit);
            if ((t & Trait.WantsCrit) != 0)
            {
                if (crit >= 40) Reason(a, 10, $"Du hast {crit}% Crit – das Item profitiert voll davon.", $"You have {crit}% crit – this item gets full value.", german);
                else if (crit >= 20) Reason(a, 5, $"Du hast {crit}% Crit – wird mit mehr Crit noch besser.", $"You have {crit}% crit – gets better with more.", german);
                else Note(a, -3, $"Braucht Crit (du hast {crit}%) – Lens-Maker's Glasses zuerst.", $"Needs crit (you have {crit}%) – Lens-Maker's Glasses first.", german);
            }
            if ((t & Trait.Crit) != 0 && (t & Trait.WantsCrit) == 0)
            {
                var wanters = Others(wantsCrit, item, a);
                if (crit >= 100) Note(a, -15, "Du hast schon 100% Crit – mehr bringt nichts.", "You already have 100% crit – more does nothing.", german);
                else if (wanters.Count > 0)
                    Reason(a, 6, $"Mehr Crit für {Names(wanters)}.", $"More crit for your {Names(wanters)}.", german);
            }

            // ---- Attack speed <-> on-hit items
            if ((t & Trait.Proc) != 0 && AttackSpeedMult >= 1.6f)
                Reason(a, 5, $"Du greifst schnell an (x{AttackSpeedMult:0.0}) – On-Hit-Effekte lösen öfter aus.",
                    $"You attack fast (x{AttackSpeedMult:0.0}) – on-hit effects proc more often.", german);
            if ((t & Trait.AttackSpeed) != 0)
            {
                var p = Others(procs, item, a);
                if (p.Count >= 3)
                    Reason(a, 6, $"Mehr Treffer für deine {p.Count} On-Hit-Items ({Names(p)}).",
                        $"More hits for your {p.Count} on-hit items ({Names(p)}).", german);
            }

            // ---- Area damage and on-kill chains
            var area = Others(aoe, item, a).Where(s => s.Item != null).ToList();
            var kills = Others(onKill, item, a);
            if ((t & Trait.Aoe) != 0)
            {
                if (SingleTargetPrimary && area.Count < 5)
                    Reason(a, Math.Max(3, 10 - 2 * area.Count),
                        "Dein Hauptangriff trifft nur ein Ziel – das bringt Flächenschaden gegen Gruppen.",
                        "Your primary hits one target – this adds area damage against groups.", german);
                if (kills.Count >= 2)
                    Reason(a, 4, $"Flächen-Kills lösen deine On-Kill-Items aus ({Names(kills)}).",
                        $"Area kills trigger your on-kill items ({Names(kills)}).", german);
            }
            if ((t & Trait.OnKill) != 0 && (t & Trait.Aoe) == 0)
            {
                if (area.Count >= 2)
                    Reason(a, 6, $"Dein Flächenschaden ({Names(area)}) tötet Gruppen – mehr Auslöser.",
                        $"Your area damage ({Names(area)}) kills groups – more triggers.", german);
                else if (SingleTargetPrimary && area.Count == 0)
                    Note(a, -2, "Ohne Flächenschaden tötest du langsam – besser erst Gasoline, Will-o'-the-wisp, Ukulele.",
                        "Without area damage you kill slowly – get Gasoline, Will-o'-the-wisp, Ukulele first.", german);
            }
        }

        // wantFlag: item boosts an effect (Ignition Tank -> burn). giveFlag: item applies it (Gasoline).
        void Pair(Advice a, Trait t, string item, bool german, Trait wantFlag, Trait giveFlag,
                  List<Src> givers, List<Src> wanters, string de, string en)
        {
            if ((t & wantFlag) != 0)
            {
                var g = Others(givers, item, a);
                if (g.Count > 0)
                    Reason(a, 10 + 3 * Math.Min(g.Count - 1, 3), $"Verstärkt {de} von {Names(g)}.",
                        $"Boosts your {en} from {Names(g)}.", german);
                else if (givers.Count == 0)
                    Note(a, -8, $"Du hast noch keine {de}-Quelle – erst ein Item, das {de} verursacht.",
                        $"You have no {en} source yet – get an item that causes {en} first.", german);
            }
            if ((t & giveFlag) != 0)
            {
                var w = Others(wanters, item, a);
                if (w.Count > 0)
                    Reason(a, 8, $"{Names(w)} macht {de} stärker.", $"Your {Names(w)} boosts this {en}.", german);
            }
        }
    }
}
