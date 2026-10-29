using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;

namespace ItemRecommender
{
    /// <summary>
    /// In-game settings via Risk of Options (optional). Only touched when that mod is loaded,
    /// so ItemRecommender keeps working without it.
    /// </summary>
    static class RiskOfOptionsCompat
    {
        internal const string Guid = "com.rune580.riskofoptions";

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Setup(string pluginDir)
        {
            ModSettingsManager.SetModDescription(
                "Item recommendations in the Command menu and a build path tile in the HUD. " +
                "Item-Empfehlungen im Command-Menü und eine Build-Pfad-Kachel im HUD.");
            try
            {
                string icon = Path.Combine(pluginDir, "icon.png");
                if (File.Exists(icon))
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(File.ReadAllBytes(icon)))
                        ModSettingsManager.SetModIcon(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)));
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Risk of Options icon: {e.Message}"); }

            // General
            Add(new CheckBoxOption(Plugin.Enabled));
            Add(new CheckBoxOption(Plugin.ShowGoodTier));
            AddChoice(Plugin.LanguageSetting);
            Add(new CheckBoxOption(Plugin.ShowLegend));
            Add(new CheckBoxOption(Plugin.ShowMenuOrder));
            Add(new CheckBoxOption(Plugin.HideTooltipUntilMouseMoves));
            Add(new CheckBoxOption(Plugin.ShowBuildPath));
            Add(new CheckBoxOption(Plugin.LivePages));
            Add(new StringInputFieldOption(Plugin.PathSeparator));

            // Build path
            Add(new KeyBindOption(Plugin.SwitchViewKey));
            Add(new KeyBindOption(Plugin.SkipItemKey));
            Add(new KeyBindOption(Plugin.SkipPageKey));
            Add(new KeyBindOption(Plugin.BackPageKey));
            Add(new CheckBoxOption(Plugin.ShowSkipHint));
            AddChoice(Plugin.LatePageFocus);
            Add(new IntSliderOption(Plugin.RarityRowSize, new IntSliderConfig { min = 3, max = 8 }));
        }

        static void Add(BaseOption option)
        {
            try { ModSettingsManager.AddOption(option); }
            catch (Exception e) { Plugin.Log.LogWarning($"Risk of Options: {e.Message}"); }
        }

        // Dropdown for string entries with an AcceptableValueList; text field as fallback.
        static void AddChoice(ConfigEntry<string> entry)
        {
            try { ModSettingsManager.AddOption(new ChoiceOption(entry)); }
            catch { Add(new StringInputFieldOption(entry)); }
        }
    }
}
