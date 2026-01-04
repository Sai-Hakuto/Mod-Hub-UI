using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UIFramework.Core;
using UIFramework.UI;
using UnityEngine;

namespace UIFramework
{
    public static class AssemblyInfo
    {
        public const string ModName = "Mod Hub";
        public const string ModVersion = "1.1.0";
        public const string ModGUID = "hakusai.modhub";
    }

    [BepInPlugin(AssemblyInfo.ModGUID, AssemblyInfo.ModName, AssemblyInfo.ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        public static Plugin Instance { get; private set; }

        // Config
        public static ConfigEntry<KeyboardShortcut> ToggleKey { get; private set; }
        public static ConfigEntry<bool> ShowAdvanced { get; private set; }
        public static ConfigEntry<string> FavoriteModsConfig { get; private set; }
        public static ConfigEntry<string> HiddenModsConfig { get; private set; }
        public static ConfigEntry<string> CollapsedSectionsConfig { get; private set; }
        public static ConfigEntry<string> CustomTagsConfig { get; private set; }
        public static ConfigEntry<string> ModTagsConfig { get; private set; }

        // General settings (visible in Mod Hub)
        public static ConfigEntry<int> UIScaleConfig { get; private set; }
        public static ConfigEntry<bool> CollapseSectionsByDefaultConfig { get; private set; }
        public static ConfigEntry<string> AccentColorConfig { get; private set; }
        public static ConfigEntry<int> RightMarginConfig { get; private set; }

        // Core systems
        private ModRegistry _modRegistry;
        private WindowManager _windowManager;
        private MainWindow _mainWindow;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"[{AssemblyInfo.ModName}] Loading v{AssemblyInfo.ModVersion}...");

            // Bind config
            ToggleKey = Config.Bind(
                "General",
                "Toggle Key",
                new KeyboardShortcut(KeyCode.F10),
                "Key to open/close Mod Hub window"
            );

            ShowAdvanced = Config.Bind(
                "General",
                "Show Advanced Settings",
                false,
                "Show advanced settings in mod pages"
            );

            FavoriteModsConfig = Config.Bind(
                "General",
                "Favorite Mods",
                "",
                "Comma-separated list of favorite mod IDs"
            );

            HiddenModsConfig = Config.Bind(
                "General",
                "Hidden Mods",
                "",
                "Comma-separated list of hidden mod IDs"
            );

            CollapsedSectionsConfig = Config.Bind(
                "UI",
                "Collapsed Sections",
                "",
                "Collapsed sections in format: modId|sectionName,modId|sectionName,..."
            );

            CustomTagsConfig = Config.Bind(
                "Tags",
                "Custom Tags",
                "",
                "User-defined tags separated by |"
            );

            ModTagsConfig = Config.Bind(
                "Tags",
                "Mod Tags",
                "",
                "Mod-to-tags mapping in format: modId:tag1,tag2;modId2:tag3"
            );

            // General settings (visible in Mod Hub's own settings page)
            UIScaleConfig = Config.Bind(
                "Appearance",
                "UI Scale",
                1,
                new ConfigDescription(
                    "UI scale factor: 1 = Normal (up to 2K), 2 = Large (for large monitors), 3 = Extra Large (4K+)",
                    new AcceptableValueList<int>(1, 2, 3)
                )
            );

            CollapseSectionsByDefaultConfig = Config.Bind(
                "Appearance",
                "Collapse Sections By Default",
                false,
                "When enabled, all sections in mod settings will be collapsed by default"
            );

            AccentColorConfig = Config.Bind(
                "Appearance",
                "Accent Color",
                "Red",
                new ConfigDescription(
                    "Accent color used for UI highlights and active elements",
                    new AcceptableValueList<string>("Red", "Gold", "Blue", "Green", "Purple", "Teal", "Orange")
                )
            );

            RightMarginConfig = Config.Bind(
                "Appearance",
                "Right Margin",
                30,
                new ConfigDescription(
                    "Right margin for grid content (pixels). Adjust if cards are too close to scrollbar.",
                    new AcceptableValueRange<int>(0, 50)
                )
            );

            // Initialize core systems
            _modRegistry = new ModRegistry();
            _modRegistry.LoadFavorites();
            _modRegistry.LoadHidden();
            _modRegistry.LoadCustomTags();
            _modRegistry.LoadModTags();
            _windowManager = new WindowManager();
            _mainWindow = new MainWindow();

            Log.LogInfo($"[{AssemblyInfo.ModName}] Loaded successfully! Press {ToggleKey.Value} to open.");
        }

        private void Start()
        {
            // Register Mod Hub as its own mod (so users can configure it)
            _modRegistry.RegisterModHub();

            // Auto-discover BepInEx plugins after all plugins are loaded
            _modRegistry.DiscoverBepInExPlugins();
        }

        private void Update()
        {
            // Check toggle key
            if (ToggleKey.Value.IsDown())
            {
                _windowManager.Toggle();
            }

            // Check escape to close
            if (_windowManager.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                _windowManager.Close();
            }

            _windowManager.Update();
        }

        private void OnGUI()
        {
            if (_windowManager.IsOpen)
            {
                _mainWindow.Draw();
            }
        }

        private void OnDestroy()
        {
            _mainWindow?.Cleanup();
        }
    }
}
