# Mod Hub

A universal settings UI framework for SPT (Single Player Tarkov) mods. Provides a comfortable, Steam-inspired interface for managing your mod collection.

<img width="201" height="208" alt="MH" src="https://github.com/user-attachments/assets/56bf3fea-df5d-440b-afa2-241b242b37af" />

**Version:** 1.1.0

---

## Features

### For Users
- **Dark theme UI** inspired by Tarkov/Steam aesthetic
- **Mod library** with icons, tags, and grid layout
- **Favorites system** - pin important mods to the top
- **Hide mods** - declutter your list by hiding unwanted mods
- **Search & Filter** - find mods by name or tags
- **Custom tags** - organize mods with your own tags
- **Image carousel** - view mod screenshots and banners
- **UI scaling** - supports 1x, 2x (1.25), 3x (1.5) for different monitor sizes
- **Accent colors** - customize UI with 7 color themes
- **Hotkey support** - F10 by default (configurable)
- **Auto-discovery** - automatically finds all BepInEx plugins

### For Developers
- Simple attribute-based API
- Support for BepInEx ConfigEntry
- Custom icons and image carousels
- Section grouping with collapsible headers
- Advanced field visibility toggle
- Callbacks for save/reset events

---

## Installation

1. Copy `UIFramework.dll` to `BepInEx/plugins/ModHub/`
2. Launch the game
3. Press **F10** to open Mod Hub

---

## For Users

### Opening Mod Hub
Press **F10** (configurable in Mod Hub settings or BepInEx config).
All installed BepInEx mods are automatically discovered and displayed.

### Mod Cards
Each mod appears as a card with:
- **Icon** - custom or auto-generated initials
- **Name & Version**
- **Author**
- **Tags** - native (from mod) and custom (from you)

Click a card to open the mod's settings page.

### Favorites & Hiding

| Button | Action |
|--------|--------|
| **<3** | Add/remove from favorites (favorites appear first) |
| **x** | Hide mod from main list |
| **+** | Show hidden mod (in Hidden section) |

Hidden mods appear in a collapsible "Hidden" section at the bottom.

### Search & Filter

Click **Find** in the header to open the search page:
- **Search bar** - filter by mod name, ID, description, or tags
- **Tag cloud** - click tags to filter (multiple tags = AND logic)
- **Results** - shows matching mods in a compact grid

### Custom Tags

1. Open any mod's settings page
2. Click **Add Tag** button
3. Choose from system tags or create your own
4. Tags appear on mod cards and in search filters

**Tag limits:**
- Maximum 12 characters per tag
- No spaces allowed
- System tags are predefined (AI, Weapons, UI, etc.)

### Settings Page

Each mod's settings page shows:
- **Image carousel** - screenshots/banners (click to enlarge)
- **Sections** - collapsible groups of settings
- **Fields** - various input types (toggles, sliders, dropdowns, etc.)
- **Tooltips** - hover over field names or ? buttons
- **Reset** - reset individual fields or all settings
- **Save** - apply changes

**Navigation:**
- **< Back** or **ESC** - return to mod list (discards unsaved changes)
- **Save** - save and keep changes

### Mod Hub Settings

Click **Cfg** in the header to configure Mod Hub itself:
- **UI Scale** - 1 (normal), 2 (large), 3 (extra large for 4K)
- **Collapse Sections** - start with all sections collapsed
- **Accent Color** - Red, Gold, Blue, Green, Purple, Teal, Orange
- **Right Margin** - adjust grid padding
- **Toggle Key** - change the hotkey
- **Show Advanced** - show/hide advanced settings globally

---

## For Mod Developers

### Quick Start

Add Mod Hub as a soft dependency and register your settings:

```csharp
using BepInEx;
using UIFramework.API;
using UIFramework.Attributes;

[BepInPlugin("com.example.mymod", "My Mod", "1.0.0")]
[BepInDependency("hakusai.modhub", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public static MySettings Settings;

    void Awake()
    {
        // Check if Mod Hub is available
        if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("hakusai.modhub"))
        {
            Settings = new MySettings();
            UIFApi.Register(Settings);
            Logger.LogInfo("Registered with Mod Hub");
        }
    }
}

[UIFMod("mymod", "My Mod", "1.0.0")]
[UIFAuthor("YourName")]
[UIFDescription("My awesome mod")]
[UIFTags("Gameplay", "QoL")]
[UIFIcon("icon.png")]
public class MySettings : UIFModBase
{
    public override string ModId => "mymod";
    public override string ModName => "My Mod";
    public override string ModVersion => "1.0.0";
    public override string ModAuthor => "YourName";
    public override object GetSettings() => this;

    [UIFSection("General", 1)]
    [UIFName("Enable Feature")]
    [UIFTooltip("Enables the main feature")]
    public bool EnableFeature = true;

    [UIFSection("General")]
    [UIFName("Intensity")]
    [UIFRange(0f, 1f, 0.1f)]
    public float Intensity = 0.5f;
}
```

### Adding Images (Carousel)

Place images in your mod's plugin folder for the carousel:

```
BepInEx/plugins/MyMod/
├── MyMod.dll
├── icon.png              <- Mod icon (recommended: 128x128)
└── images/               <- Auto-discovered for carousel
    ├── 01_screenshot.png
    ├── 02_settings.png
    └── 03_feature.jpg
```

**Auto-discovery rules:**
- All `.png`, `.jpg`, `.jpeg` files in the `images/` folder are loaded
- Files are sorted alphabetically (prefix with numbers for ordering)
- Click images in carousel to view fullscreen

**Recommended sizes:**
| Image Type | Size | Notes |
|------------|------|-------|
| Icon | 128x128 px | Shown in mod list, rounded corners applied |
| Carousel | 460x215 px | Steam capsule format, or any aspect ratio |

**Icon fallback order:**
1. Path specified in `[UIFIcon("path")]`
2. `icon.png` in mod folder
3. Auto-generated initials icon

### Adding a Custom Icon

```csharp
[UIFMod("mymod", "My Mod", "1.0.0")]
[UIFIcon("icon.png")]  // Relative to your plugin folder
public class MySettings : UIFModBase { ... }
```

Or specify images via attribute:
```csharp
[UIFImages("images/banner1.png", "images/banner2.png")]
```

### Mod-Level Attributes

| Attribute | Required | Description | Example |
|-----------|----------|-------------|---------|
| `[UIFMod]` | Yes | Mod ID, name, version | `[UIFMod("mymod", "My Mod", "1.0.0")]` |
| `[UIFAuthor]` | No | Author name | `[UIFAuthor("Hakusai")]` |
| `[UIFDescription]` | No | Mod description | `[UIFDescription("Does cool things")]` |
| `[UIFTags]` | No | Tags for filtering (max 12 chars each) | `[UIFTags("AI", "Gameplay")]` |
| `[UIFIcon]` | No | Icon path relative to plugin folder | `[UIFIcon("icon.png")]` |
| `[UIFImages]` | No | Carousel image paths | `[UIFImages("img1.png", "img2.png")]` |

### Field Attributes

| Attribute | Description | Example |
|-----------|-------------|---------|
| `[UIFSection]` | Group into section (name, order) | `[UIFSection("Combat", 1)]` |
| `[UIFName]` | Display name | `[UIFName("Detection Range")]` |
| `[UIFTooltip]` | Hover tooltip | `[UIFTooltip("Range in meters")]` |
| `[UIFRange]` | Slider with min/max/step | `[UIFRange(0f, 100f, 1f)]` |
| `[UIFPercentage]` | Display as percentage | `[UIFPercentage(true)]` |
| `[UIFAdvanced]` | Hide unless "Adv" enabled | `[UIFAdvanced]` |
| `[UIFHidden]` | Never show in UI | `[UIFHidden]` |
| `[UIFReadOnly]` | Display only, no editing | `[UIFReadOnly]` |
| `[UIFOrder]` | Field order within section | `[UIFOrder(5)]` |
| `[UIFFormat]` | Custom number format | `[UIFFormat("F3")]` |

### Supported Field Types

| Type | UI Control |
|------|------------|
| `bool` | Toggle switch |
| `int`, `float`, `double` | Text field or slider (with `[UIFRange]`) |
| `string` | Text field |
| `enum` | Dropdown selector |
| `Color` | Color picker with RGB sliders + presets |
| `Vector2`, `Vector3` | Multi-field input (X, Y, Z) |
| `KeyCode` | Key selector dropdown |
| `KeyboardShortcut` | Key + modifiers selector |
| `ConfigEntry<T>` | Auto-detected from BepInEx config |

### API Reference

```csharp
using UIFramework.API;

// Check if Mod Hub is loaded
bool loaded = UIFApi.IsLoaded;

// Register mod (two methods)
UIFApi.Register(IUIFMod mod);
UIFApi.Register(object settingsObject, string pluginFolder);

// Unregister mod
UIFApi.Unregister(string modId);

// Window control
UIFApi.Open();
UIFApi.Close();
UIFApi.Toggle();
UIFApi.OpenMod(string modId);  // Open specific mod's settings
```

### Callbacks

Override these methods to react to user actions:

```csharp
public override void OnSettingsSaved()
{
    // Called when user clicks "Save"
    ApplyMySettings();
}

public override void OnSettingsReset()
{
    // Called when user clicks "Reset All"
    // Reset your fields to defaults here
}
```

---

## System Tags

Available predefined tags for categorization:

`AI`, `Audio`, `Ballistics`, `Bots`, `Cheats`, `Debug`, `Economy`, `Flea`, `Gameplay`, `Graphics`, `Hideout`, `Items`, `Maps`, `Medical`, `Performance`, `QoL`, `Quests`, `Realism`, `Skills`, `Traders`, `UI`, `Weapons`

---

## Configuration

Mod Hub settings are stored in:
```
BepInEx/config/hakusai.modhub.cfg
```

| Setting | Default | Description |
|---------|---------|-------------|
| Toggle Key | F10 | Hotkey to open/close |
| UI Scale | 1 | 1 = Normal, 2 = Large (1.25x), 3 = Extra Large (1.5x) |
| Accent Color | Red | UI highlight color |
| Collapse Sections | false | Start with sections collapsed |
| Right Margin | 30 | Grid right padding in pixels |
| Show Advanced | false | Show advanced fields by default |

---

## Changelog

### v1.1.0
- Added UI scaling (1x, 2x, 3x) for different monitor sizes
- Added accent color customization (7 colors)
- Added right margin configuration
- Improved tag system with character limits
- Fixed ESC key handling when mouse over window
- Fixed label stretching issues
- Improved color picker with larger sliders
- Various UI polish and fixes

### v1.0.0
- Initial release

---

## License

MIT License - Free to use, modify, and distribute.

---

## Links

- [SPT Hub](https://forge.sp-tarkov.com)

**Made with love for the SPT community**
