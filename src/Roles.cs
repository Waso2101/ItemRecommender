using System.Collections.Generic;

namespace ItemRecommender
{
    /// <summary>
    /// What an item is for, plus its community consensus rating (Tierspire, Oct 2026: six tier
    /// lists combined; S = 4, A = 3, B = 2, C = 1, D = 0). Used to build balanced build-path pages.
    /// </summary>
    public static class Roles
    {
        // D = damage, P = proc damage, S = sustain (healing), F = defense (block, armor, HP, shield),
        // H = max health / shield (against one-shots), M = mobility, U = utility (cooldowns, equipment, luck)
        public struct Info { public char Role; public int Consensus; }

        static readonly Dictionary<string, Info> table = new Dictionary<string, Info>(System.StringComparer.OrdinalIgnoreCase);

        static void Add(char role, int consensus, params string[] items)
        {
            foreach (var i in items) table[i] = new Info { Role = role, Consensus = consensus };
        }

        static Roles()
        {
            // white
            Add('F', 4, "Bear"); Add('M', 4, "Hoof"); Add('D', 4, "Syringe");
            Add('D', 3, "CritGlasses", "AttackSpeedAndMoveSpeed", "IgniteOnKill", "Crowbar", "BossDamageBonus");
            Add('M', 3, "SprintBonus"); Add('S', 3, "Tooth", "Medkit"); Add('U', 3, "SecondarySkillMagazine");
            Add('P', 3, "BleedOnHit"); Add('F', 3, "BarrierOnKill", "ArmorPlate"); Add('H', 3, "PersonalShield");
            Add('D', 2, "NearbyDamageBonus", "IncreaseDamageOnMultiKill", "AttackSpeedPerNearbyAllyOrEnemy", "CritAtLowerElevation", "FragileDamageBonus");
            Add('S', 2, "HealWhileSafe"); Add('F', 2, "OutOfCombatArmor", "DelayedDamage");
            Add('P', 2, "StickyBomb"); Add('U', 2, "StunChanceOnHit");
            Add('H', 1, "FlatHealth"); Add('S', 1, "Mushroom", "HealingPotion"); Add('P', 1, "Firework");
            Add('U', 1, "WardOnLevel"); Add('M', 1, "SpeedBoostPickup");
            // green
            Add('P', 4, "Missile", "ExplodeOnDeath"); Add('U', 4, "EquipmentMagazine");
            Add('P', 3, "ChainLightning", "FireRing", "IceRing", "PrimarySkillShuriken", "Thorns");
            Add('S', 3, "HealOnCrit"); Add('M', 3, "Feather"); Add('F', 3, "TeleportOnLowHealth", "SprintArmor");
            Add('D', 3, "ExecuteLowHealthElite", "AttackSpeedOnCrit"); Add('U', 3, "Bandolier");
            Add('H', 2, "Infusion", "ExtraStatsOnLevelUp"); Add('F', 2, "ShieldBooster"); Add('S', 2, "Seed", "TPHealingNova");
            Add('D', 2, "DeathMark", "WarCryOnMultiKill", "StrengthenBurn", "IncreasePrimaryDamage", "EnergizedOnEquipmentUse");
            Add('M', 2, "JumpBoost", "SprintOutOfCombat"); Add('U', 2, "SlowOnHit"); Add('P', 2, "KnockBackHitEnemies", "TriggerEnemyDebuffs");
            Add('P', 1, "Squid"); Add('F', 1, "Phasing"); Add('M', 0, "MoveSpeedOnKill");
            // red
            Add('U', 4, "Clover"); Add('F', 4, "SharedSuffering");
            Add('P', 3, "Behemoth", "Dagger", "BounceNearby", "MoreMissile");
            Add('F', 3, "ExtraLife", "ImmuneToDebuff"); Add('M', 3, "UtilitySkillMagazine");
            Add('D', 3, "PermanentDebuffOnHit", "CritDamage", "ArmorReductionOnHit");
            Add('U', 3, "KillEliteFrenzy", "Talisman", "AlienHead"); Add('S', 3, "IncreaseHealing", "NovaOnHeal");
            Add('M', 2, "FallBoots"); Add('P', 2, "MeteorAttackOnHighDamage", "ShockNearby", "LaserTurbine", "Icicle");
            Add('S', 2, "Plant"); Add('F', 2, "BarrierOnOverHeal"); Add('D', 1, "HeadHunter"); Add('P', 1, "GhostOnKill");
            // boss
            Add('P', 4, "BleedOnHitAndExplode", "LightningStrikeOnHit"); Add('F', 4, "NovaOnLowHealth"); Add('U', 4, "ExtraEquipment");
            Add('P', 3, "BeetleGland", "TitanGoldDuringTP"); Add('S', 3, "SiphonOnLowHealth"); Add('H', 3, "Pearl", "ShinyPearl", "Knurl");
            Add('P', 2, "FireballsOnHit", "MinorConstructOnKill", "SprintWisp");
            // void
            Add('P', 4, "ExplodeOnDeathVoid"); Add('S', 4, "MushroomVoid");
            Add('F', 3, "BearVoid", "ExtraLifeVoid"); Add('P', 3, "BleedOnHitVoid", "MissileVoid"); Add('U', 3, "SlowOnHitVoid");
            Add('P', 2, "ChainLightningVoid"); Add('U', 2, "CloverVoid");
        }

        public static IEnumerable<string> Items => table.Keys;

        /// <summary>Known item: its role and rating. Unknown (modded) item: guessed from the file tier.</summary>
        public static Info Get(string item, int fileTier) =>
            table.TryGetValue(item, out var i) ? i : new Info { Role = 'D', Consensus = fileTier + 1 };

        /// <summary>Community rating + survivor rating + synergies + build analysis, minus what you already stack.</summary>
        public static int Score(string item, Advice advice, int have) =>
            Get(item, advice.Tier).Consensus * 10 + advice.Tier * 8 + advice.SynergyReasons.Count * 5 + advice.Bonus
            - System.Math.Min(have, 10);

        static bool Offense(char r) => r == 'D' || r == 'P';

        /// <summary>
        /// Top items by score as a mixed row: the best defense/max-health item and the best healing
        /// item are always in, then at most maxOffense damage items and one mobility/utility item.
        /// Keeps the score order.
        /// </summary>
        public static List<T> TopMixed<T>(List<T> sorted, System.Func<T, string> item, int count, int maxOffense)
        {
            var picked = new HashSet<int>();
            System.Func<System.Func<char, bool>, int, int> take = (match, n) =>
            {
                for (int i = 0; i < sorted.Count && n > 0 && picked.Count < count; i++)
                    if (!picked.Contains(i) && match(Get(item(sorted[i]), 0).Role)) { picked.Add(i); n--; }
                return n;
            };
            take(r => r == 'F' || r == 'H', 1);
            take(r => r == 'S', 1);
            int offense = 0, other = 0;
            for (int i = 0; i < sorted.Count && picked.Count < count; i++)
            {
                if (picked.Contains(i)) continue;
                char r = Get(item(sorted[i]), 0).Role;
                if (Offense(r)) { if (offense >= maxOffense) continue; offense++; }
                else if (r == 'M' || r == 'U') { if (other >= 1) continue; other++; }
                picked.Add(i);
            }
            take(r => true, count); // not enough candidates: fill up
            var result = new List<T>();
            for (int i = 0; i < sorted.Count; i++) if (picked.Contains(i)) result.Add(sorted[i]);
            return result;
        }

        // Stacks beyond this do (almost) nothing.
        public static int Cap(string item) =>
            item.Equals("CritGlasses", System.StringComparison.OrdinalIgnoreCase) ? 10 :
            item.Equals("Bear", System.StringComparison.OrdinalIgnoreCase) ? 10 : int.MaxValue;
    }
}
