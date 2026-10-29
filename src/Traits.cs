using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RoR2;

namespace ItemRecommender
{
    /// <summary>What an item or skill does (gives) and what it gets better with (wants).</summary>
    [Flags]
    public enum Trait : uint
    {
        None = 0,
        // Debuff types on enemies (Death Mark counts different types, Noxious Thorn spreads them)
        Bleed = 1 << 0, Burn = 1 << 1, Poison = 1 << 2, Slow = 1 << 3, Weak = 1 << 4, Cripple = 1 << 5,
        Armor = 1 << 6, Collapse = 1 << 7, Root = 1 << 8, Launch = 1 << 9, Frost = 1 << 10, Expose = 1 << 11,
        OtherDebuff = 1 << 12,
        Debuffs = (1 << 13) - 1,
        // What it gives
        Aoe = 1 << 14,        // hits several enemies
        OnKill = 1 << 15,     // triggers on kills -> better with area damage
        Crit = 1 << 16,       // gives crit chance
        AttackSpeed = 1 << 17,
        Proc = 1 << 18,       // chance on hit -> better with attack speed
        Stun = 1 << 19,
        // What it wants
        WantsDebuffs = 1 << 20, WantsBurn = 1 << 21, WantsCrit = 1 << 22, WantsBleed = 1 << 23, WantsStun = 1 << 24,
    }

    /// <summary>
    /// Reads item and skill descriptions (in English, whatever the game language is) and turns them
    /// into traits. Works for every item, also modded ones. Vanilla items whose text would be read
    /// wrong (e.g. "15% chance" on Tougher Times is not an on-hit effect) are corrected by a table.
    /// </summary>
    public static class Traits
    {
        const Trait Bleed = Trait.Bleed, Burn = Trait.Burn, Poison = Trait.Poison, Slow = Trait.Slow, Weak = Trait.Weak,
            Armor = Trait.Armor, Collapse = Trait.Collapse, Root = Trait.Root, Launch = Trait.Launch, Frost = Trait.Frost,
            Other = Trait.OtherDebuff, Aoe = Trait.Aoe, OnKill = Trait.OnKill, Crit = Trait.Crit, AtkSpd = Trait.AttackSpeed,
            Proc = Trait.Proc, Stun = Trait.Stun, WDebuffs = Trait.WantsDebuffs, WBurn = Trait.WantsBurn,
            WCrit = Trait.WantsCrit, WBleed = Trait.WantsBleed, WStun = Trait.WantsStun, None = Trait.None;

        // Vanilla items by description token ("ITEM_BEAR_DESC" -> "BEAR"). Replaces the text analysis.
        static readonly Dictionary<string, Trait> Known = new Dictionary<string, Trait>(StringComparer.OrdinalIgnoreCase)
        {
            // white
            ["SYRINGE"] = AtkSpd, ["CRITGLASSES"] = Crit, ["BLEEDONHIT"] = Bleed | Proc, ["STICKYBOMB"] = Proc,
            ["IGNITEONKILL"] = Burn | Aoe | OnKill, ["TOOTH"] = OnKill, ["BARRIERONKILL"] = OnKill,
            ["ATTACKSPEEDANDMOVESPEED"] = AtkSpd, ["STUNCHANCEONHIT"] = Stun | Proc, ["ATTACKSPEEDPERNEARBYALLYORENEMY"] = AtkSpd,
            ["CRITATLOWERELEVATION"] = Crit, ["INCREASEDAMAGEONMULTIKILL"] = OnKill, ["BEAR"] = None, ["CROWBAR"] = None,
            ["NEARBYDAMAGEBONUS"] = None, ["FIREWORK"] = None, ["MUSHROOM"] = None, ["WARDONLEVEL"] = None,
            ["SPEEDBOOSTPICKUP"] = None, ["GOLDONHIT"] = None, ["MEDKIT"] = None, ["HOOF"] = None, ["SPRINTBONUS"] = None,
            ["SECONDARYSKILLMAGAZINE"] = None, ["FRAGILEDAMAGEBONUS"] = None, ["OUTOFCOMBATARMOR"] = None,
            ["REPULSIONARMORPLATE"] = None, ["PERSONALSHIELD"] = None, ["FLATHEALTH"] = None, ["HEALWHILESAFE"] = None,
            ["DELAYEDDAMAGE"] = None, ["BOSSDAMAGEBONUS"] = None, ["TREASURECACHE"] = None, ["HEALINGPOTION"] = None,
            ["GOLDONHURT"] = None,
            // green
            ["MISSILE"] = Proc, ["CHAINLIGHTNING"] = Proc | Aoe, ["EXPLODEONDEATH"] = Aoe | OnKill, ["FIRERING"] = Burn | Aoe,
            ["ICERING"] = Slow, ["SEED"] = None, ["HEALONCRIT"] = Crit | WCrit, ["ATTACKSPEEDONCRIT"] = Crit | WCrit | AtkSpd,
            ["DEATHMARK"] = WDebuffs, ["TRIGGERENEMYDEBUFFS"] = Bleed | Proc | OnKill | WDebuffs, ["SLOWONHIT"] = Slow | Proc,
            ["STRENGTHENBURN"] = WBurn, ["KNOCKBACKHITENEMIES"] = Launch | Stun, ["BANDOLIER"] = OnKill, ["INFUSION"] = OnKill,
            ["WARCRYONMULTIKILL"] = OnKill | AtkSpd, ["THORNS"] = Aoe, ["MOVESPEEDONKILL"] = OnKill, ["PHASING"] = None,
            ["ENERGIZEDONEQUIPMENTUSE"] = AtkSpd, ["STUNANDPIERCE"] = Stun | Proc | Aoe, ["VILECENSER"] = WStun | Aoe,
            ["SANGUINECHALICE"] = Bleed | Proc | WBleed, ["WYRMONHIT"] = Burn | Proc, ["GHOSTBLADES"] = Bleed,
            ["CAUSTICPHEROMONES"] = Stun | Other, ["JUMPDAMAGESTRIKE"] = Aoe, ["SQUIDTURRET"] = None, ["FEATHER"] = None,
            ["EXECUTELOWHEALTHELITE"] = None, ["PRIMARYSKILLSHURIKEN"] = None, ["INCREASEPRIMARYDAMAGE"] = None,
            ["TPHEALINGNOVA"] = None, ["SPRINTARMOR"] = None, ["TELEPORTONLOWHEALTH"] = None, ["DRONEWEAPONS"] = None,
            ["EQUIPMENTMAGAZINE"] = None, ["JUMPBOOST"] = None, ["SPRINTOUTOFCOMBAT"] = None, ["SHIELDBOOSTER"] = None,
            ["EXTRASTATSONLEVELUP"] = None, ["INTERSTELLARDESKPLANT"] = OnKill,
            // red
            ["BEHEMOTH"] = Aoe, ["DAGGER"] = OnKill, ["BOUNCENEARBY"] = Proc | Aoe, ["PERMANENTDEBUFFONHIT"] = Armor,
            ["ARMORREDUCTIONONHIT"] = Armor, ["CRITDAMAGE"] = WCrit, ["ICICLE"] = Frost | Slow | Aoe | OnKill,
            ["SHOCKNEARBY"] = Aoe, ["LASERTURBINE"] = Aoe, ["METEORATTACKONHIGHDAMAGE"] = Aoe, ["HEADHUNTER"] = OnKill,
            ["GHOSTONKILL"] = OnKill, ["NOVAONHEAL"] = Aoe, ["FALLBOOTS"] = Aoe, ["SHAREDSUFFERING"] = Other | Aoe,
            ["MOREMISSILE"] = None, ["KILLELITEFRENZY"] = None, ["CLOVER"] = None, ["ALIENHEAD"] = None,
            ["COOLDOWNONCRIT"] = Crit | WCrit,
            // boss
            ["BLEEDONHITANDEXPLODE"] = Bleed | Aoe | OnKill | Crit | WCrit, ["LIGHTNINGSTRIKEONHIT"] = Proc,
            ["FIREBALLSONHIT"] = Burn | Proc | Aoe, ["NOVAONLOWHEALTH"] = Aoe, ["SIPHONONLOWHEALTH"] = Aoe,
            ["MINORCONSTRUCTONKILL"] = OnKill, ["BEETLEGLAND"] = None, ["TITANGOLDDURINGTP"] = None, ["SPRINTWISP"] = Aoe,
            // void
            ["BLEEDONHITVOID"] = Collapse | Proc, ["SLOWONHITVOID"] = Root | Proc, ["CHAINLIGHTNINGVOID"] = Proc,
            ["MISSILEVOID"] = Proc, ["EXPLODEONDEATHVOID"] = Aoe, ["ELEMENTALRINGVOID"] = Aoe, ["CRITGLASSESVOID"] = None,
            ["BEARVOID"] = None, ["MUSHROOMVOID"] = None, ["CLOVERVOID"] = None,
            // lunar
            ["LUNARSECONDARYREPLACEMENT"] = Root | Aoe, ["LUNARSPECIALREPLACEMENT"] = Other,
            // equipment
            ["MOLOTOV"] = Burn | Aoe, ["BURNNEARBY"] = Burn | Aoe, ["CRITONUSE"] = Crit,
        };

        // Debuff keywords. Order = display order.
        static readonly KeyValuePair<Trait, Regex>[] DebuffWords =
        {
            Kw(Bleed, @"\bbleed|hemorrhag"),
            Kw(Burn, @"\bignit|\bburn|\bscorch"),
            Kw(Poison, @"poison|blight|toxic|\bacid"),
            Kw(Slow, @"\bslow"),
            Kw(Weak, @"\bweaken"),
            Kw(Trait.Cripple, @"cripple"),
            Kw(Armor, @"(reduc\w*|lower\w*|shred\w*) (their |the enemy\W?s |enemy )?armor"),
            Kw(Collapse, @"collapse (an )?enem"),
            Kw(Root, @"\broots?\b|entangl"),
            Kw(Launch, @"launched|launch(es)? (enemies|them)|(knocks?|launch\w*) enemies into the air"),
            Kw(Frost, @"\bfrost\b|\bchill"),
            Kw(Trait.Expose, @"\bexpos"),
            Kw(Other, @"haunt|nano-?bug|lunar ruin|\boil(s|ed)? enem|soul-brand|random debuff to enem"),
        };
        static readonly Regex AoeWords = new Regex(@"nearby|radius|\barea\b|explo|all enemies|chain|bounc|ricochet|nova|pierc|in all directions|\bcone\b|spreads? to|storm|cloud|shockwave|up to \d+ enemies|\b\d+m\b", RegexOptions.Compiled);
        static readonly Regex OnKillWords = new Regex(@"on kill|killing an enem|on killing|kills? (an enemy|enemies)", RegexOptions.Compiled);
        static readonly Regex CritGive = new Regex(@"critical (strike )?chance|chance to critically strike", RegexOptions.Compiled);
        static readonly Regex CritWant = new Regex(@"critical strikes? (deal|heal|reduce|increase|also|fire|grant)|dealing a critical|on critical", RegexOptions.Compiled);
        static readonly Regex AtkSpdWords = new Regex(@"(\+|gain|increase\w*)[^.]{0,20}attack speed", RegexOptions.Compiled);
        static readonly Regex ProcWords = new Regex(@"chance on hit|on hit, \d+% chance|\d+%[^.]{0,25}chance to (?!block|dodge|avoid|negate|crit|not )\w+", RegexOptions.Compiled);
        static readonly Regex StunWords = new Regex(@"\bstun|shocking", RegexOptions.Compiled);
        static readonly Regex WantsDebuffWords = new Regex(@"(enemies|enemy) with [^.]{0,20}debuff|transfer[^.]{0,30}debuff|per (unique )?debuff", RegexOptions.Compiled);
        static readonly Regex WantsBurnWords = new Regex(@"ignite effects|burn effects|burn damage", RegexOptions.Compiled);
        static readonly Regex WantsBleedWords = new Regex(@"your bleed|bleed damage heals", RegexOptions.Compiled);
        static readonly Regex WantsStunWords = new Regex(@"stunned (target|enem)", RegexOptions.Compiled);
        static readonly Regex Tags = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        static KeyValuePair<Trait, Regex> Kw(Trait t, string pattern) =>
            new KeyValuePair<Trait, Regex>(t, new Regex(pattern, RegexOptions.Compiled));

        static readonly Dictionary<string, Trait> cache = new Dictionary<string, Trait>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Traits of an item or equipment by internal name.</summary>
        public static Trait Of(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return None;
            if (cache.TryGetValue(internalName, out var t)) return t;
            string token = null;
            var ii = ItemCatalog.FindItemIndex(internalName);
            if (ii != ItemIndex.None) token = ItemCatalog.GetItemDef(ii)?.descriptionToken;
            else
            {
                var ei = EquipmentCatalog.FindEquipmentIndex(internalName);
                if (ei != EquipmentIndex.None) token = EquipmentCatalog.GetEquipmentDef(ei)?.descriptionToken;
            }
            t = FromToken(token, internalName);
            if (ItemCatalog.itemCount > 0) cache[internalName] = t; // catalogs ready: safe to remember
            return t;
        }

        static Trait FromToken(string token, string internalName)
        {
            string stem = Stem(token);
            if (stem != null && Known.TryGetValue(stem, out var known)) return known;
            if (Known.TryGetValue(internalName, out known)) return known;
            return Parse(English(token));
        }

        static string Stem(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            string s = token;
            if (s.StartsWith("ITEM_")) s = s.Substring(5);
            else if (s.StartsWith("EQUIPMENT_")) s = s.Substring(10);
            if (s.EndsWith("_DESC")) s = s.Substring(0, s.Length - 5);
            return s;
        }

        /// <summary>Description text in English (keywords are English), without rich-text tags.</summary>
        public static string English(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            string s = null;
            try
            {
                var en = Language.english;
                if (en != null) s = en.GetLocalizedStringByToken(token);
            }
            catch { }
            if (string.IsNullOrEmpty(s) || s == token) s = Language.GetString(token);
            return Tags.Replace(s ?? "", "").ToLowerInvariant();
        }

        /// <summary>Keyword analysis of a description (used for modded items and survivor skills).</summary>
        public static Trait Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return None;
            var t = None;
            bool wantsBurn = WantsBurnWords.IsMatch(text);
            if (wantsBurn) t |= WBurn;
            foreach (var kw in DebuffWords)
            {
                if (kw.Key == Burn && wantsBurn) continue; // "ignite effects deal more" doesn't ignite
                if (kw.Value.IsMatch(text)) t |= kw.Key;
            }
            if (AoeWords.IsMatch(text)) t |= Aoe;
            if (OnKillWords.IsMatch(text)) t |= OnKill;
            if (CritGive.IsMatch(text)) t |= Crit;
            if (CritWant.IsMatch(text)) t |= WCrit;
            if (AtkSpdWords.IsMatch(text)) t |= AtkSpd;
            if (ProcWords.IsMatch(text)) t |= Proc;
            if (StunWords.IsMatch(text)) t |= Stun;
            if (WantsDebuffWords.IsMatch(text)) t |= WDebuffs;
            if (WantsBleedWords.IsMatch(text)) t |= WBleed;
            if (WantsStunWords.IsMatch(text)) t |= WStun;
            return t;
        }

        /// <summary>Area words for a survivor's primary skill (single target or not).</summary>
        public static bool HitsArea(string text) => !string.IsNullOrEmpty(text) && AoeWords.IsMatch(text);

        /// <summary>Debuff types in a trait set, in display order.</summary>
        public static IEnumerable<Trait> DebuffTypes(Trait t)
        {
            foreach (var kw in DebuffWords) if ((t & kw.Key) != 0) yield return kw.Key;
        }

        public static int CountDebuffs(Trait t)
        {
            int n = 0;
            foreach (var kw in DebuffWords) if ((t & kw.Key) != 0) n++;
            return n;
        }

        public static string DebuffName(Trait t, bool german)
        {
            switch (t)
            {
                case Trait.Bleed: return german ? "Bluten" : "bleed";
                case Trait.Burn: return german ? "Brand" : "burn";
                case Trait.Poison: return german ? "Gift" : "poison";
                case Trait.Slow: return german ? "Verlangsamung" : "slow";
                case Trait.Weak: return german ? "Schwächung" : "weaken";
                case Trait.Cripple: return german ? "Verkrüppelt" : "cripple";
                case Trait.Armor: return german ? "Rüstung runter" : "armor shred";
                case Trait.Collapse: return german ? "Kollaps" : "collapse";
                case Trait.Root: return german ? "Festhalten" : "root";
                case Trait.Launch: return german ? "Hochgeschleudert" : "launched";
                case Trait.Frost: return german ? "Frost" : "frost";
                case Trait.Expose: return german ? "Entblößt" : "expose";
                default: return german ? "Sonstiger Debuff" : "other debuff";
            }
        }
    }
}
