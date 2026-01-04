using UIFramework.API;
using UIFramework.Attributes;
using BepInEx.Configuration;
using UnityEngine;

namespace UIFramework.Core
{
    /// <summary>
    /// Mod Hub's own settings, exposed as a native mod in the registry.
    /// </summary>
    [UIFMod("hakusai.modhub", "Mod Hub", AssemblyInfo.ModVersion)]
    [UIFAuthor("Hakusai")]
    [UIFDescription("Universal settings UI framework for SPT mods.")]
    [UIFTags("UI", "QoL")]
    public class ModHubSettings : UIFModBase
    {
        public override string ModId => AssemblyInfo.ModGUID;
        public override string ModName => AssemblyInfo.ModName;
        public override string ModVersion => AssemblyInfo.ModVersion;
        public override string ModAuthor => "Hakusai";
        public override string Description => "Universal settings UI framework for SPT mods. Configure Mod Hub appearance and behavior.";
        public override string[] Tags => new[] { "UI", "QoL" };

        public override object GetSettings() => this;

        // ═══════════════════════════════════════════════════════════════
        // SECTION: Appearance
        // ═══════════════════════════════════════════════════════════════

        [UIFSection("Appearance", 1)]
        [UIFName("UI Scale")]
        [UIFTooltip("1 = Normal (up to 2K), 2 = Large (for large monitors), 3 = Extra Large (4K+)")]
        public ConfigEntry<int> UIScale => Plugin.UIScaleConfig;

        [UIFSection("Appearance")]
        [UIFName("Collapse Sections By Default")]
        [UIFTooltip("When enabled, all sections in mod settings will be collapsed by default")]
        public ConfigEntry<bool> CollapseSectionsByDefault => Plugin.CollapseSectionsByDefaultConfig;

        [UIFSection("Appearance")]
        [UIFName("Accent Color")]
        [UIFTooltip("Accent color used for UI highlights and active elements")]
        public ConfigEntry<string> AccentColor => Plugin.AccentColorConfig;

        [UIFSection("Appearance")]
        [UIFName("Right Margin")]
        [UIFTooltip("Right margin for grid content (pixels). Adjust if cards are too close to scrollbar.")]
        public ConfigEntry<int> RightMargin => Plugin.RightMarginConfig;

        // ═══════════════════════════════════════════════════════════════
        // SECTION: System
        // ═══════════════════════════════════════════════════════════════

        [UIFSection("System", 2)]
        [UIFName("Toggle Key")]
        [UIFTooltip("Key to open/close Mod Hub window")]
        public ConfigEntry<KeyboardShortcut> ToggleKey => Plugin.ToggleKey;

        [UIFSection("System")]
        [UIFName("Show Advanced Settings")]
        [UIFTooltip("Show advanced settings in mod pages")]
        public ConfigEntry<bool> ShowAdvanced => Plugin.ShowAdvanced;

        public override void OnSettingsSaved()
        {
            Plugin.Log.LogInfo("[Mod Hub] Settings saved");
        }

        public override void OnSettingsReset()
        {
            Plugin.UIScaleConfig.Value = 1;
            Plugin.CollapseSectionsByDefaultConfig.Value = false;
            Plugin.AccentColorConfig.Value = "Red";
            Plugin.RightMarginConfig.Value = 30;
            Plugin.ShowAdvanced.Value = false;
            Plugin.Log.LogInfo("[Mod Hub] Settings reset to defaults");
        }
    }
}
