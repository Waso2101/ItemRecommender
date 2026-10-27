using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using UnityEngine;
using Path = System.IO.Path;

namespace ItemRecommender
{
    // Client-side only: nothing is networked, other players don't need the mod.
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Waso.ItemRecommender";
        const string OldGuid = "clxru.ItemRecommender"; // until 0.5.0
        public const string Name = "ItemRecommender";
        public const string Version = "0.7.0";

        internal static Plugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        internal static BuildData Data;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowGoodTier;
        internal static ConfigEntry<string> LanguageSetting;
        internal static ConfigEntry<bool> ShowLegend;
        internal static ConfigEntry<bool> HideTooltipUntilMouseMoves;
        internal static ConfigEntry<bool> ShowBuildPath;
        internal static ConfigEntry<string> PathSeparator;
        internal static ConfigEntry<bool> LivePages;
        internal static ConfigEntry<string> LatePageFocus;
        internal static ConfigEntry<KeyboardShortcut> SkipItemKey;
        internal static ConfigEntry<KeyboardShortcut> SkipPageKey;
        internal static ConfigEntry<bool> ShowSkipHint;
        float nextHudCheck;

        static readonly HashSet<string> loggedBodies = new HashSet<string>();

        void Awake()
        {
            Instance = this;
            // Keep settings from versions that used the old ID.
            try
            {
                string oldCfg = Path.Combine(Paths.ConfigPath, OldGuid + ".cfg");
                if (File.Exists(oldCfg) && !File.Exists(Config.ConfigFilePath))
                {
                    File.Copy(oldCfg, Config.ConfigFilePath);
                    Config.Reload();
                }
            }
            catch (Exception e) { Logger.LogWarning($"Could not take over old config: {e.Message}"); }
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Show item recommendations in the Command menu.");
            ShowGoodTier = Config.Bind("General", "ShowGoodTier", true,
                "Also frame tier-1 ('good') items, not only tier-2 core items with a star.");
            LanguageSetting = Config.Bind("General", "Language", "auto",
                new ConfigDescription("Language of the tooltip hints.", new AcceptableValueList<string>("auto", "de", "en")));
            ShowLegend = Config.Bind("General", "ShowLegend", true, "Show the legend next to the Command menu.");
            HideTooltipUntilMouseMoves = Config.Bind("General", "HideTooltipUntilMouseMoves", true,
                "The cursor starts in the middle of the screen, so it always lands on an item when the Command menu opens. " +
                "Hide that item's description until you move the mouse.");
            ShowBuildPath = Config.Bind("General", "ShowBuildPath", true,
                "Show the survivor's build path (the 'Path:' lines in the build file) under the objectives.");
            LivePages = Config.Bind("General", "LivePages", true,
                "After page 1 of the build path, generate new pages from item roles, community ratings and your synergies.");
            LatePageFocus = Config.Bind("Build path", "LatePageFocus", "sustain",
                new ConfigDescription("What generated pages from page 3 on focus on.",
                    new AcceptableValueList<string>("sustain", "balanced", "damage")));
            SkipItemKey = Config.Bind("Build path", "SkipItemKey", new KeyboardShortcut(KeyCode.F7),
                "Skip the highlighted item of the build path until the next stage.");
            SkipPageKey = Config.Bind("Build path", "SkipPageKey", new KeyboardShortcut(KeyCode.F8),
                "Skip the whole page: page 1 for this run, live items until the next stage.");
            ShowSkipHint = Config.Bind("Build path", "ShowSkipHint", true, "Show the hotkey hint under the build path.");
            PathSeparator = Config.Bind("General", "PathSeparator", ">", "Symbol drawn between the steps of the build path.");

            // The editable copy lives in BepInEx/config so mod updates don't overwrite your changes.
            string userFile = Path.Combine(Paths.ConfigPath, "ItemRecommender.builds.txt");
            string bundled = Path.Combine(Path.GetDirectoryName(Info.Location), "builds.txt");
            try { InstallBuildFile(bundled, userFile); }
            catch (Exception e) { Logger.LogError($"Could not create {userFile}: {e.Message}"); }

            Data = new BuildData(File.Exists(userFile) ? userFile : bundled, Logger);
            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(PickerPatches));
            harmony.PatchAll(typeof(TooltipPatches));
            Logger.LogInfo($"{Name} {Version} loaded. Build file: {userFile}");
        }

        /// <summary>
        /// Copies the bundled list to the config folder. A newer bundled list (higher
        /// "#@version") replaces the old copy; the old one is kept as .bak.
        /// </summary>
        void InstallBuildFile(string bundled, string userFile)
        {
            if (!File.Exists(bundled)) return;
            if (!File.Exists(userFile)) { File.Copy(bundled, userFile); return; }
            int newV = FileVersion(bundled), oldV = FileVersion(userFile);
            if (newV <= oldV) return;
            File.Copy(userFile, userFile + ".bak", true);
            File.Copy(bundled, userFile, true);
            Logger.LogInfo($"Updated build list v{oldV} -> v{newV} (old copy: {userFile}.bak)");
        }

        static int FileVersion(string file)
        {
            foreach (var line in File.ReadLines(file))
                if (line.StartsWith("#@version") && int.TryParse(line.Substring(9).Trim(), out int v)) return v;
            return 0;
        }

        void Update()
        {
            if (Time.unscaledTime < nextHudCheck) return;
            nextHudCheck = Time.unscaledTime + 1f;
            try { foreach (var hud in HUD.readOnlyInstanceList) PathTile.Ensure(hud); }
            catch (Exception e) { Logger.LogError(e); nextHudCheck = Time.unscaledTime + 30f; }
        }

        internal static bool German()
        {
            string s = LanguageSetting.Value;
            if (s == "de") return true;
            if (s == "en") return false;
            return Language.currentLanguageName != null &&
                   Language.currentLanguageName.StartsWith("de", StringComparison.OrdinalIgnoreCase);
        }

        internal static void LogBodyOnce(string bodyName)
        {
            if (bodyName != null && loggedBodies.Add(bodyName))
                Log.LogInfo($"Survivor for recommendations: [{bodyName}]");
        }
    }

    [HarmonyPatch]
    static class PickerPatches
    {
        static readonly AccessTools.FieldRef<PickupPickerPanel, UIElementAllocator<MPButton>> AllocatorRef =
            AccessTools.FieldRefAccess<PickupPickerPanel, UIElementAllocator<MPButton>>("buttonAllocator");
        static readonly AccessTools.FieldRef<PickupPickerPanel, PickupPickerController.Option[]> OptionsRef =
            AccessTools.FieldRefAccess<PickupPickerPanel, PickupPickerController.Option[]>("pickupOptions");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PickupPickerPanel), nameof(PickupPickerPanel.SetPickupOptions))]
        static void AfterSetOptions(PickupPickerPanel __instance)
        {
            TooltipPatches.MenuOpened(__instance);
            Safe(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PickupPickerPanel), nameof(PickupPickerPanel.RemovePickupButtonAvailability))]
        static void AfterRemove(PickupPickerPanel __instance) => Safe(__instance);

        static void Safe(PickupPickerPanel panel)
        {
            try { Refresh(panel); }
            catch (Exception e) { Plugin.Log.LogError(e); } // never break the vanilla menu
        }

        static void Refresh(PickupPickerPanel panel)
        {
            var allocator = AllocatorRef(panel);
            var options = OptionsRef(panel);
            if (allocator == null || options == null) return;
            var buttons = allocator.elements;

            bool active = Plugin.Enabled.Value && !IsScrapper(panel);
            CharacterBody body = LocalUserManager.GetFirstLocalUser()?.cachedBody;
            string bodyName = body ? BodyCatalog.GetBodyName(body.bodyIndex) : null;
            if (active) { Plugin.Data.ReloadIfChanged(); Plugin.LogBodyOnce(bodyName); }
            bool german = Plugin.German();
            Func<string, int> countOf = name => PlayerCount(body, name);

            int count = Math.Min(buttons.Count, options.Length);
            var advices = new Advice[count];
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (!active || !options[i].available) continue;
                string name = InternalName(PickupCatalog.GetPickupDef(options[i].pickup.pickupIndex));
                if (name == null) continue;
                advices[i] = Plugin.Data.Get(bodyName, name, countOf, german);
                if (advices[i].Tier >= 2 && (best < 0 || advices[i].Score > advices[best].Score)) best = i;
            }

            for (int i = 0; i < count; i++)
            {
                var overlay = Overlay.GetOrCreate(buttons[i].gameObject);
                overlay.Show(advices[i], Plugin.ShowGoodTier.Value, i == best);
                UpdateTooltip(buttons[i], overlay, advices[i], german, i == best);
            }

            var legendTf = panel.transform.Find("ItemRecommenderLegend");
            if (active && Plugin.ShowLegend.Value)
            {
                string survivor = body ? Language.GetString(body.baseNameToken) : null;
                var legend = Legend.GetOrCreate(panel);
                legend.gameObject.SetActive(true);
                legend.SetTexts(survivor, german);
            }
            else if (legendTf) legendTf.gameObject.SetActive(false);
        }

        static void UpdateTooltip(MPButton button, Overlay overlay, Advice advice, bool german, bool isBest)
        {
            if (!button.TryGetComponent<TooltipProvider>(out var tip)) return;

            // Strip what we added last time (buttons are reused between menus).
            string text = tip.bodyText ?? "";
            if (!string.IsNullOrEmpty(overlay.AppendedTooltip) && text.EndsWith(overlay.AppendedTooltip))
                text = text.Substring(0, text.Length - overlay.AppendedTooltip.Length);
            overlay.AppendedTooltip = null;

            if (advice == null || (advice.Tier == 0 && advice.Warnings.Count == 0 && advice.SynergyReasons.Count == 0))
            {
                tip.overrideBodyText = text;
                return;
            }

            var sb = new System.Text.StringBuilder("\n\n");
            string head, color;
            switch (advice.Tier)
            {
                case 2: head = german ? "EMPFOHLEN" : "RECOMMENDED"; color = "#FFCC26"; break;
                case 1: head = german ? "Gute Wahl" : "Good pick"; color = "#FFEE8C"; break;
                case -1: head = german ? "EHER NICHT" : "RATHER NOT"; color = "#FF8C1A"; break;
                case -2: head = german ? "NICHT BAUEN" : "DON'T BUILD"; color = "#F2261F"; break;
                default: head = german ? "Hinweis" : "Note"; color = "#BBBBBB"; break;
            }
            if (isBest) head += german ? " – beste Wahl hier" : " – best pick here";
            sb.Append("<color=").Append(color).Append(">").Append(head).Append("</color>");
            foreach (var w in advice.Warnings)
                sb.Append("\n<color=#FF5A4A>! ").Append(w).Append("</color>");
            if (!string.IsNullOrEmpty(advice.Reason)) sb.Append("\n").Append(advice.Reason);
            foreach (var r in advice.SynergyReasons)
                sb.Append("\n<color=#40F2E6>+ ").Append(r).Append("</color>");

            overlay.AppendedTooltip = sb.ToString();
            tip.overrideBodyText = text + overlay.AppendedTooltip;
        }

        static bool IsScrapper(PickupPickerPanel panel)
        {
            var ctrl = panel.pickerController;
            return ctrl && ctrl.GetComponent<ScrapperController>();
        }

        static string InternalName(PickupDef def)
        {
            if (def == null) return null;
            if (def.itemIndex != ItemIndex.None) return ItemCatalog.GetItemDef(def.itemIndex)?.name;
            if (def.equipmentIndex != EquipmentIndex.None) return EquipmentCatalog.GetEquipmentDef(def.equipmentIndex)?.name;
            return null;
        }

        internal static int PlayerCount(CharacterBody body, string name)
        {
            if (!body || !body.inventory) return 0;
            var ii = ItemCatalog.FindItemIndex(name);
            if (ii != ItemIndex.None) return body.inventory.GetItemCountPermanent(ii);
            var ei = EquipmentCatalog.FindEquipmentIndex(name);
            return ei != EquipmentIndex.None && body.equipmentSlot && body.equipmentSlot.equipmentIndex == ei ? 1 : 0;
        }
    }

    /// <summary>
    /// When the Command menu opens, the cursor sits in the screen centre on top of an item, so the
    /// game shows that item's description right away. Hide picker tooltips until the mouse moves.
    /// Controller navigation is not affected.
    /// </summary>
    [HarmonyPatch]
    static class TooltipPatches
    {
        const float MoveThreshold = 25f; // pixels

        static readonly AccessTools.FieldRef<TooltipProvider, Vector2?> EnterPosRef =
            AccessTools.FieldRefAccess<TooltipProvider, Vector2?>("pointerEnterPosition");

        static int panelId;
        static bool suppressed;
        static Vector3 startMouse;

        internal static void MenuOpened(PickupPickerPanel panel)
        {
            // SetPickupOptions can run again while the same menu stays open; only reset for a new menu.
            int id = panel.GetInstanceID();
            if (id == panelId) return;
            panelId = id;
            suppressed = Plugin.HideTooltipUntilMouseMoves.Value;
            startMouse = Input.mousePosition;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TooltipProvider), "SetTooltip")]
        static bool BeforeSetTooltip(TooltipProvider __instance)
        {
            if (!suppressed) return true;
            try
            {
                if (!EnterPosRef(__instance).HasValue) return true;          // controller navigation
                if (!__instance.transform.Find("ItemRecommenderOverlay")) return true; // not a picker button
                if ((Input.mousePosition - startMouse).sqrMagnitude > MoveThreshold * MoveThreshold)
                {
                    suppressed = false;
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                suppressed = false;
                Plugin.Log.LogError(e);
                return true;
            }
        }
    }
}
