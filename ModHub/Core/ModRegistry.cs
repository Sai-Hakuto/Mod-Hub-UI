using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UIFramework.API;
using UIFramework.Attributes;
using UnityEngine;

namespace UIFramework.Core
{
    /// <summary>
    /// Central registry for all UIFramework-compatible mods.
    /// </summary>
    public class ModRegistry
    {
        public static ModRegistry Instance { get; private set; }

        private Dictionary<string, ModInfo> _mods = new Dictionary<string, ModInfo>();
        private HashSet<string> _favorites = new HashSet<string>();
        private HashSet<string> _hidden = new HashSet<string>();
        private HashSet<string> _customTags = new HashSet<string>();
        private Dictionary<string, HashSet<string>> _modTags = new Dictionary<string, HashSet<string>>();

        // Icon cache directory
        private static readonly string IconCacheDir = Path.Combine(BepInEx.Paths.CachePath, "ModHub", "icons");

        // System tags - predefined, cannot be deleted
        private static readonly HashSet<string> _systemTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AI", "Weapons", "Graphics", "Audio", "UI", "Gameplay",
            "Performance", "Realism", "QoL", "Cheats", "Debug",
            "Bots", "Items", "Maps", "Quests", "Traders", "Skills",
            "Medical", "Ballistics", "Economy", "Flea", "Hideout",
            "Hidden"
        };

        public IReadOnlyDictionary<string, ModInfo> Mods => _mods;
        public int ModCount => _mods.Count;
        public IReadOnlyCollection<string> Favorites => _favorites;
        public IReadOnlyCollection<string> Hidden => _hidden;
        public IReadOnlyCollection<string> CustomTags => _customTags;
        public static IReadOnlyCollection<string> SystemTags => _systemTags;

        public event Action<ModInfo> OnModRegistered;
        public event Action<string> OnModUnregistered;
        public event Action OnFavoritesChanged;
        public event Action OnHiddenChanged;
        public event Action OnTagsChanged;

        public ModRegistry()
        {
            Instance = this;
        }

        // ═══════════════════════════════════════════════════════════════
        // FAVORITES
        // ═══════════════════════════════════════════════════════════════

        public bool IsFavorite(string modId)
        {
            return _favorites.Contains(modId);
        }

        public void ToggleFavorite(string modId)
        {
            if (_favorites.Contains(modId))
                _favorites.Remove(modId);
            else
                _favorites.Add(modId);

            OnFavoritesChanged?.Invoke();
            SaveFavorites();
        }

        public void SetFavorites(IEnumerable<string> favorites)
        {
            _favorites.Clear();
            foreach (var f in favorites)
                _favorites.Add(f);
        }

        private void SaveFavorites()
        {
            // Save to config
            Plugin.FavoriteModsConfig.Value = string.Join(",", _favorites);
        }

        public void LoadFavorites()
        {
            var saved = Plugin.FavoriteModsConfig?.Value ?? "";
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var modId in saved.Split(','))
                {
                    if (!string.IsNullOrEmpty(modId))
                        _favorites.Add(modId.Trim());
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HIDDEN MODS
        // ═══════════════════════════════════════════════════════════════

        public bool IsHidden(string modId)
        {
            return _hidden.Contains(modId);
        }

        public void ToggleHidden(string modId)
        {
            if (_hidden.Contains(modId))
            {
                _hidden.Remove(modId);
                RemoveTagFromMod(modId, "Hidden", isInternal: true);
            }
            else
            {
                _hidden.Add(modId);
                AddTagToMod(modId, "Hidden", isInternal: true);
            }

            OnHiddenChanged?.Invoke();
            SaveHidden();
        }

        private void SaveHidden()
        {
            Plugin.HiddenModsConfig.Value = string.Join(",", _hidden);
        }

        public void LoadHidden()
        {
            var saved = Plugin.HiddenModsConfig?.Value ?? "";
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var modId in saved.Split(','))
                {
                    if (!string.IsNullOrEmpty(modId))
                    {
                        var trimmedId = modId.Trim();
                        _hidden.Add(trimmedId);

                        // Add Hidden tag to mod
                        if (!_modTags.ContainsKey(trimmedId))
                            _modTags[trimmedId] = new HashSet<string>();
                        _modTags[trimmedId].Add("Hidden");
                    }
                }
            }
        }

        /// <summary>
        /// Get all mods sorted with favorites first, excluding hidden by default.
        /// </summary>
        public IEnumerable<ModInfo> GetModsSortedByFavorites(bool includeHidden = false)
        {
            var source = includeHidden
                ? _mods.Values
                : _mods.Values.Where(m => !_hidden.Contains(m.ModId));

            var favorites = source.Where(m => _favorites.Contains(m.ModId)).OrderBy(m => m.ModName);
            var others = source.Where(m => !_favorites.Contains(m.ModId)).OrderBy(m => m.ModName);
            return favorites.Concat(others);
        }

        /// <summary>
        /// Get only hidden mods.
        /// </summary>
        public IEnumerable<ModInfo> GetHiddenMods()
        {
            return _mods.Values.Where(m => _hidden.Contains(m.ModId)).OrderBy(m => m.ModName);
        }

        // ═══════════════════════════════════════════════════════════════
        // TAGS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if a tag is a system tag (predefined, cannot be deleted).
        /// </summary>
        public static bool IsSystemTag(string tag)
        {
            return _systemTags.Contains(tag);
        }

        /// <summary>
        /// Validate a tag. Returns null if valid, or error message if invalid.
        /// </summary>
        public string ValidateTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return "Tag cannot be empty";

            tag = tag.Trim();

            if (tag.Contains(" "))
                return "Tag cannot contain spaces";

            if (tag.Length > 20)
                return "Tag is too long (max 20 chars)";

            if (IsSystemTag(tag))
                return "This tag already exists as system tag";

            if (_customTags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                return "This tag already exists";

            return null; // Valid
        }

        /// <summary>
        /// Get all available tags (system + custom).
        /// </summary>
        public IEnumerable<string> GetAllAvailableTags()
        {
            return _systemTags.Concat(_customTags).OrderBy(t => t);
        }

        /// <summary>
        /// Get all custom tags for a specific mod (user-assigned).
        /// </summary>
        public IEnumerable<string> GetModCustomTags(string modId)
        {
            if (_modTags.TryGetValue(modId, out var tags))
                return tags;
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Check if a mod has a specific tag (custom or native).
        /// </summary>
        public bool ModHasTag(string modId, string tag)
        {
            // Check custom tags
            if (_modTags.TryGetValue(modId, out var tags) && tags.Contains(tag))
                return true;

            // Check native tags from mod's Tags array
            if (_mods.TryGetValue(modId, out var mod) && mod.Tags != null)
                return mod.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));

            return false;
        }

        /// <summary>
        /// Add a tag to a mod (user action).
        /// </summary>
        public void AddTagToMod(string modId, string tag, bool isInternal = false)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;

            tag = tag.Trim();

            // Don't add if it contains spaces
            if (tag.Contains(" ")) return;

            // Prevent manual adding of Hidden tag - only via hiding
            if (!isInternal && tag.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                return;

            // Add to global custom tag list (if not system tag)
            if (!IsSystemTag(tag))
                _customTags.Add(tag);

            // Add to mod's tags
            if (!_modTags.ContainsKey(modId))
                _modTags[modId] = new HashSet<string>();

            _modTags[modId].Add(tag);

            OnTagsChanged?.Invoke();
            SaveCustomTags();
            SaveModTags();
        }

        /// <summary>
        /// Remove a tag from a mod (user action).
        /// </summary>
        public void RemoveTagFromMod(string modId, string tag, bool isInternal = false)
        {
            // Prevent manual removal of Hidden tag - only via unhiding
            if (!isInternal && tag.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                return;

            if (_modTags.TryGetValue(modId, out var tags))
            {
                tags.Remove(tag);
                if (tags.Count == 0)
                    _modTags.Remove(modId);

                OnTagsChanged?.Invoke();
                SaveModTags();
            }
        }

        /// <summary>
        /// Create a new custom tag (without assigning to any mod).
        /// </summary>
        public bool CreateCustomTag(string tag, out string error)
        {
            error = ValidateTag(tag);
            if (error != null)
                return false;

            tag = tag.Trim();

            if (_customTags.Add(tag))
            {
                OnTagsChanged?.Invoke();
                SaveCustomTags();
            }

            return true;
        }

        /// <summary>
        /// Delete a custom tag from the system (removes from all mods).
        /// Cannot delete system tags.
        /// </summary>
        public bool DeleteCustomTag(string tag)
        {
            // Cannot delete system tags
            if (IsSystemTag(tag))
                return false;

            if (!_customTags.Remove(tag))
                return false;

            // Remove from all mods
            foreach (var modTags in _modTags.Values)
            {
                modTags.Remove(tag);
            }

            OnTagsChanged?.Invoke();
            SaveCustomTags();
            SaveModTags();
            return true;
        }

        // Max tag length to prevent layout issues in cards
        private const int MAX_TAG_LENGTH = 12;

        /// <summary>
        /// Filter tags array, removing invalid tags (too long, empty, with spaces).
        /// </summary>
        private string[] FilterTags(string[] tags)
        {
            if (tags == null) return Array.Empty<string>();

            var filtered = new System.Collections.Generic.List<string>();
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;
                var trimmed = tag.Trim();
                if (trimmed.Length > MAX_TAG_LENGTH) continue;
                if (trimmed.Contains(" ")) continue;
                filtered.Add(trimmed);
            }
            return filtered.ToArray();
        }

        /// <summary>
        /// Import tags from a native mod registration.
        /// Adds new tags to custom tags for reuse by user.
        /// </summary>
        private void ImportNativeTags(string[] tags)
        {
            if (tags == null) return;

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;
                var trimmed = tag.Trim();

                // Skip if too long
                if (trimmed.Length > MAX_TAG_LENGTH) continue;

                // Skip if contains spaces
                if (trimmed.Contains(" ")) continue;

                // Skip system tags (already available)
                if (IsSystemTag(trimmed)) continue;

                // Add to custom tags for reuse
                _customTags.Add(trimmed);
            }

            SaveCustomTags();
        }

        private void SaveCustomTags()
        {
            Plugin.CustomTagsConfig.Value = string.Join("|", _customTags);
        }

        private void SaveModTags()
        {
            // Format: modId:tag1,tag2,tag3;modId2:tag1,tag4
            var parts = new List<string>();
            foreach (var kvp in _modTags)
            {
                if (kvp.Value.Count > 0)
                {
                    parts.Add($"{kvp.Key}:{string.Join(",", kvp.Value)}");
                }
            }
            Plugin.ModTagsConfig.Value = string.Join(";", parts);
        }

        public void LoadCustomTags()
        {
            var saved = Plugin.CustomTagsConfig?.Value ?? "";
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var tag in saved.Split('|'))
                {
                    if (!string.IsNullOrEmpty(tag))
                        _customTags.Add(tag.Trim());
                }
            }
        }

        public void LoadModTags()
        {
            var saved = Plugin.ModTagsConfig?.Value ?? "";
            if (!string.IsNullOrEmpty(saved))
            {
                // Format: modId:tag1,tag2,tag3;modId2:tag1,tag4
                foreach (var part in saved.Split(';'))
                {
                    if (string.IsNullOrEmpty(part)) continue;

                    var colonIndex = part.IndexOf(':');
                    if (colonIndex <= 0) continue;

                    var modId = part.Substring(0, colonIndex);
                    var tagsPart = part.Substring(colonIndex + 1);

                    if (!_modTags.ContainsKey(modId))
                        _modTags[modId] = new HashSet<string>();

                    foreach (var tag in tagsPart.Split(','))
                    {
                        if (!string.IsNullOrEmpty(tag))
                            _modTags[modId].Add(tag.Trim());
                    }
                }
            }
        }

        /// <summary>
        /// Register a mod using IUIFMod interface.
        /// </summary>
        public void RegisterMod(IUIFMod mod)
        {
            if (mod == null)
            {
                Plugin.Log.LogError("[UIFramework] Cannot register null mod");
                return;
            }

            if (string.IsNullOrEmpty(mod.ModId))
            {
                Plugin.Log.LogError("[UIFramework] Mod has no ModId");
                return;
            }

            if (_mods.ContainsKey(mod.ModId))
            {
                Plugin.Log.LogWarning($"[UIFramework] Mod '{mod.ModId}' is already registered, updating...");
            }

            var modInfo = new ModInfo
            {
                ModId = mod.ModId,
                ModName = mod.ModName ?? mod.ModId,
                ModVersion = mod.ModVersion ?? "1.0.0",
                ModAuthor = mod.ModAuthor ?? "Unknown",
                Description = mod.Description ?? "",
                Tags = FilterTags(mod.Tags),
                IconPath = mod.IconPath,
                ImagePaths = mod.ImagePaths ?? Array.Empty<string>(),
                SettingsObject = mod.GetSettings(),
                ModInterface = mod
            };

            modInfo.ParseSettings();
            LoadModImages(modInfo);

            _mods[mod.ModId] = modInfo;

            // Import native tags to custom tags for user reuse
            ImportNativeTags(modInfo.Tags);

            Plugin.Log.LogInfo($"[UIFramework] Registered mod: {modInfo.ModName} v{modInfo.ModVersion} ({modInfo.Sections.Count} sections)");

            OnModRegistered?.Invoke(modInfo);
        }

        /// <summary>
        /// Register a mod from a settings object with UIFMod attribute.
        /// </summary>
        public void RegisterModFromAttributes(object settingsObject, string pluginFolder = null)
        {
            if (settingsObject == null)
            {
                Plugin.Log.LogError("[UIFramework] Cannot register null settings object");
                return;
            }

            var type = settingsObject.GetType();

            // Get UIFMod attribute
            var modAttr = type.GetCustomAttributes(typeof(UIFModAttribute), false).FirstOrDefault() as UIFModAttribute;
            if (modAttr == null)
            {
                Plugin.Log.LogError($"[UIFramework] Settings class '{type.Name}' has no [UIFMod] attribute");
                return;
            }

            var modInfo = new ModInfo
            {
                ModId = modAttr.ModId,
                ModName = modAttr.ModName,
                ModVersion = modAttr.ModVersion,
                SettingsObject = settingsObject,
                PluginFolder = pluginFolder
            };

            // Get optional attributes
            var authorAttr = type.GetCustomAttributes(typeof(UIFAuthorAttribute), false).FirstOrDefault() as UIFAuthorAttribute;
            modInfo.ModAuthor = authorAttr?.Author ?? "Unknown";

            var descAttr = type.GetCustomAttributes(typeof(UIFDescriptionAttribute), false).FirstOrDefault() as UIFDescriptionAttribute;
            modInfo.Description = descAttr?.Description ?? "";

            var tagsAttr = type.GetCustomAttributes(typeof(UIFTagsAttribute), false).FirstOrDefault() as UIFTagsAttribute;
            modInfo.Tags = FilterTags(tagsAttr?.Tags);

            var iconAttr = type.GetCustomAttributes(typeof(UIFIconAttribute), false).FirstOrDefault() as UIFIconAttribute;
            modInfo.IconPath = iconAttr?.IconPath;

            var imagesAttr = type.GetCustomAttributes(typeof(UIFImagesAttribute), false).FirstOrDefault() as UIFImagesAttribute;
            modInfo.ImagePaths = imagesAttr?.ImagePaths ?? Array.Empty<string>();

            modInfo.ParseSettings();
            LoadModImages(modInfo);

            _mods[modInfo.ModId] = modInfo;

            // Import native tags to custom tags for user reuse
            ImportNativeTags(modInfo.Tags);

            Plugin.Log.LogInfo($"[UIFramework] Registered mod (from attributes): {modInfo.ModName} v{modInfo.ModVersion}");

            OnModRegistered?.Invoke(modInfo);
        }

        /// <summary>
        /// Unregister a mod.
        /// </summary>
        public void UnregisterMod(string modId)
        {
            if (_mods.TryGetValue(modId, out var modInfo))
            {
                // Cleanup textures
                if (modInfo.IconTexture != null)
                {
                    UnityEngine.Object.Destroy(modInfo.IconTexture);
                }
                foreach (var tex in modInfo.ImageTextures)
                {
                    if (tex != null) UnityEngine.Object.Destroy(tex);
                }

                _mods.Remove(modId);
                Plugin.Log.LogInfo($"[UIFramework] Unregistered mod: {modId}");

                OnModUnregistered?.Invoke(modId);
            }
        }

        /// <summary>
        /// Get mod info by ID.
        /// </summary>
        public ModInfo GetMod(string modId)
        {
            _mods.TryGetValue(modId, out var mod);
            return mod;
        }

        /// <summary>
        /// Get all mods with a specific tag.
        /// </summary>
        public IEnumerable<ModInfo> GetModsByTag(string tag)
        {
            return _mods.Values.Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all unique tags from all mods (native + user-assigned custom tags).
        /// </summary>
        public Dictionary<string, int> GetAllTags()
        {
            var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in _mods.Values)
            {
                // Native tags from mod author
                foreach (var tag in mod.Tags)
                {
                    if (tagCounts.ContainsKey(tag))
                        tagCounts[tag]++;
                    else
                        tagCounts[tag] = 1;
                }

                // User-assigned custom tags
                foreach (var tag in GetModCustomTags(mod.ModId))
                {
                    if (tagCounts.ContainsKey(tag))
                        tagCounts[tag]++;
                    else
                        tagCounts[tag] = 1;
                }
            }

            return tagCounts;
        }

        /// <summary>
        /// Search mods by name or tags.
        /// </summary>
        public IEnumerable<ModInfo> Search(string query)
        {
            if (string.IsNullOrEmpty(query))
                return _mods.Values;

            query = query.ToLower();

            return _mods.Values.Where(m =>
                m.ModName.ToLower().Contains(query) ||
                m.ModId.ToLower().Contains(query) ||
                m.Tags.Any(t => t.ToLower().Contains(query)) ||
                m.Description.ToLower().Contains(query)
            );
        }

        /// <summary>
        /// Load icon and images for a mod.
        /// </summary>
        private void LoadModImages(ModInfo modInfo)
        {
            string baseFolder = modInfo.PluginFolder ??
                Path.Combine(BepInEx.Paths.PluginPath, modInfo.ModId);

            // Load icon (try explicit path first, then icon.png in root)
            if (!string.IsNullOrEmpty(modInfo.IconPath))
            {
                string iconFullPath = Path.Combine(baseFolder, modInfo.IconPath);
                modInfo.IconTexture = LoadTexture(iconFullPath, isIcon: true);
            }

            if (modInfo.IconTexture == null)
            {
                // Try default icon.png
                string defaultIcon = Path.Combine(baseFolder, "icon.png");
                if (File.Exists(defaultIcon))
                {
                    modInfo.IconTexture = LoadTexture(defaultIcon, isIcon: true);
                }
            }

            // Get cached or generate initials icon if still no icon found
            if (modInfo.IconTexture == null)
            {
                modInfo.IconTexture = GetOrCreateCachedIcon(modInfo.ModName, modInfo.ModId);
            }

            // Load images from explicit paths
            if (modInfo.ImagePaths != null && modInfo.ImagePaths.Length > 0)
            {
                foreach (var imagePath in modInfo.ImagePaths)
                {
                    string imageFullPath = Path.Combine(baseFolder, imagePath);
                    var tex = LoadTexture(imageFullPath);
                    if (tex != null)
                    {
                        modInfo.ImageTextures.Add(tex);
                    }
                }
            }

            // Auto-discover images from "images" folder
            string imagesFolder = Path.Combine(baseFolder, "images");
            if (Directory.Exists(imagesFolder))
            {
                try
                {
                    var imageFiles = Directory.GetFiles(imagesFolder)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f);

                    foreach (var imageFile in imageFiles)
                    {
                        // Skip if already loaded via explicit path
                        string relativePath = Path.Combine("images", Path.GetFileName(imageFile));
                        if (modInfo.ImagePaths != null && modInfo.ImagePaths.Contains(relativePath))
                            continue;

                        var tex = LoadTexture(imageFile);
                        if (tex != null)
                        {
                            modInfo.ImageTextures.Add(tex);
                            Plugin.Log.LogInfo($"[UIFramework] Auto-loaded image: {imageFile}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[UIFramework] Failed to scan images folder: {ex.Message}");
                }
            }
        }

        // Max texture size to prevent memory issues
        private const int MAX_ICON_SIZE = 256;
        private const int MAX_IMAGE_SIZE = 1024;

        /// <summary>
        /// Load a texture from file with size limiting.
        /// </summary>
        private Texture2D LoadTexture(string path, bool isIcon = false)
        {
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning($"[UIFramework] Image not found: {path}");
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    int maxSize = isIcon ? MAX_ICON_SIZE : MAX_IMAGE_SIZE;
                    tex = ResizeIfNeeded(tex, maxSize);

                    // Apply rounded corners to icons for consistent style
                    if (isIcon && tex != null)
                    {
                        tex = ApplyRoundedCorners(tex);
                    }

                    return tex;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[UIFramework] Failed to load image '{path}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Apply rounded corners to a texture (for consistent icon style).
        /// </summary>
        private Texture2D ApplyRoundedCorners(Texture2D source)
        {
            if (source == null) return null;

            int width = source.width;
            int height = source.height;

            // Corner radius proportional to size (same as generated icons)
            float cornerRadius = Mathf.Min(width, height) * 0.125f; // 12.5% of smallest dimension

            // Get pixels
            Color[] pixels = source.GetPixels();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsOutsideRoundedRect(x, y, width, height, cornerRadius))
                    {
                        // Make corner pixels transparent
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            source.SetPixels(pixels);
            source.Apply();

            return source;
        }

        /// <summary>
        /// Resize texture if it exceeds max size.
        /// </summary>
        private Texture2D ResizeIfNeeded(Texture2D source, int maxSize)
        {
            if (source.width <= maxSize && source.height <= maxSize)
                return source;

            // Calculate new size maintaining aspect ratio
            float aspect = (float)source.width / source.height;
            int newWidth, newHeight;

            if (source.width > source.height)
            {
                newWidth = maxSize;
                newHeight = Mathf.RoundToInt(maxSize / aspect);
            }
            else
            {
                newHeight = maxSize;
                newWidth = Mathf.RoundToInt(maxSize * aspect);
            }

            // Create render texture and resize
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // Destroy original
            UnityEngine.Object.Destroy(source);

            Plugin.Log.LogInfo($"[UIFramework] Resized texture from {source.width}x{source.height} to {newWidth}x{newHeight}");

            return result;
        }

        // Predefined colors for initials icons (nice gradient palette)
        private static readonly Color[] IconColors = new[]
        {
            new Color(0.23f, 0.51f, 0.96f), // Blue
            new Color(0.55f, 0.27f, 0.68f), // Purple
            new Color(0.91f, 0.30f, 0.24f), // Red
            new Color(0.95f, 0.61f, 0.07f), // Orange
            new Color(0.18f, 0.80f, 0.44f), // Green
            new Color(0.10f, 0.74f, 0.61f), // Teal
            new Color(0.20f, 0.29f, 0.37f), // Dark
            new Color(0.61f, 0.35f, 0.71f), // Violet
        };

        /// <summary>
        /// Get cached icon or generate and cache it.
        /// </summary>
        private Texture2D GetOrCreateCachedIcon(string modName, string modId)
        {
            // Create cache directory if needed
            try
            {
                if (!Directory.Exists(IconCacheDir))
                {
                    Directory.CreateDirectory(IconCacheDir);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ModHub] Failed to create icon cache directory: {ex.Message}");
                return GenerateInitialsIcon(modName, modId);
            }

            // Build cache file path using modId hash + initials for uniqueness
            string initials = GetInitials(modName);
            string cacheFileName = $"{SanitizeFileName(modId)}_{initials}.png";
            string cachePath = Path.Combine(IconCacheDir, cacheFileName);

            // Try to load from cache
            if (File.Exists(cachePath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(cachePath);
                    Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(data))
                    {
                        return tex;
                    }
                    UnityEngine.Object.Destroy(tex);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[ModHub] Failed to load cached icon for {modId}: {ex.Message}");
                }
            }

            // Generate new icon
            Texture2D icon = GenerateInitialsIcon(modName, modId);

            // Save to cache
            try
            {
                byte[] pngData = icon.EncodeToPNG();
                File.WriteAllBytes(cachePath, pngData);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ModHub] Failed to cache icon for {modId}: {ex.Message}");
            }

            return icon;
        }

        /// <summary>
        /// Sanitize string for use as filename.
        /// </summary>
        private string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>
        /// Generate an icon with initials from mod name.
        /// </summary>
        private Texture2D GenerateInitialsIcon(string modName, string modId)
        {
            const int size = 128;

            // Get initials (first letters of first two words)
            string initials = GetInitials(modName);

            // Pick color based on modId hash for consistency
            int colorIndex = Mathf.Abs(modId.GetHashCode()) % IconColors.Length;
            Color bgColor = IconColors[colorIndex];

            // Create texture
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            // Fill with gradient background
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Slight gradient from top to bottom
                    float gradient = 1f - (y / (float)size) * 0.2f;
                    Color pixel = bgColor * gradient;
                    pixel.a = 1f;

                    // Rounded corners
                    float cornerRadius = 16f;
                    if (IsOutsideRoundedRect(x, y, size, size, cornerRadius))
                    {
                        pixel.a = 0f;
                    }

                    tex.SetPixel(x, y, pixel);
                }
            }

            // Draw initials using simple pixel font
            DrawInitials(tex, initials, size);

            tex.Apply();
            return tex;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            // Split by spaces, dashes, underscores
            var words = name.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0) return "?";
            if (words.Length == 1)
            {
                // Single word - take first 2 chars or first char
                return words[0].Length >= 2
                    ? words[0].Substring(0, 2).ToUpper()
                    : words[0].Substring(0, 1).ToUpper();
            }

            // Take first letter of first two words
            return (words[0][0].ToString() + words[1][0].ToString()).ToUpper();
        }

        private bool IsOutsideRoundedRect(int x, int y, int width, int height, float radius)
        {
            // Check corners
            if (x < radius && y < radius)
                return Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) > radius;
            if (x >= width - radius && y < radius)
                return Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) > radius;
            if (x < radius && y >= height - radius)
                return Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) > radius;
            if (x >= width - radius && y >= height - radius)
                return Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1)) > radius;

            return false;
        }

        private void DrawInitials(Texture2D tex, string initials, int size)
        {
            // Simple 5x7 pixel font for uppercase letters
            var font = GetSimpleFont();

            int charWidth = 5;
            int charHeight = 7;
            int scale = size / 16; // Scale factor
            int spacing = 1;

            int totalWidth = initials.Length * (charWidth + spacing) * scale - spacing * scale;
            int startX = (size - totalWidth) / 2;
            int startY = (size - charHeight * scale) / 2;

            Color textColor = Color.white;

            for (int c = 0; c < initials.Length; c++)
            {
                char ch = initials[c];
                if (font.TryGetValue(ch, out var pattern))
                {
                    int offsetX = startX + c * (charWidth + spacing) * scale;

                    for (int py = 0; py < charHeight; py++)
                    {
                        for (int px = 0; px < charWidth; px++)
                        {
                            if (pattern[py * charWidth + px] == 1)
                            {
                                // Draw scaled pixel
                                for (int sy = 0; sy < scale; sy++)
                                {
                                    for (int sx = 0; sx < scale; sx++)
                                    {
                                        int tx = offsetX + px * scale + sx;
                                        int ty = startY + (charHeight - 1 - py) * scale + sy;
                                        if (tx >= 0 && tx < size && ty >= 0 && ty < size)
                                        {
                                            tex.SetPixel(tx, ty, textColor);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private Dictionary<char, int[]> GetSimpleFont()
        {
            // Simple 5x7 bitmap font for uppercase letters
            return new Dictionary<char, int[]>
            {
                ['A'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1 },
                ['B'] = new[] { 1,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,0 },
                ['C'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,1, 0,1,1,1,0 },
                ['D'] = new[] { 1,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,0 },
                ['E'] = new[] { 1,1,1,1,1, 1,0,0,0,0, 1,0,0,0,0, 1,1,1,1,0, 1,0,0,0,0, 1,0,0,0,0, 1,1,1,1,1 },
                ['F'] = new[] { 1,1,1,1,1, 1,0,0,0,0, 1,0,0,0,0, 1,1,1,1,0, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0 },
                ['G'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,0, 1,0,1,1,1, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['H'] = new[] { 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1 },
                ['I'] = new[] { 1,1,1,1,1, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 1,1,1,1,1 },
                ['J'] = new[] { 0,0,1,1,1, 0,0,0,1,0, 0,0,0,1,0, 0,0,0,1,0, 0,0,0,1,0, 1,0,0,1,0, 0,1,1,0,0 },
                ['K'] = new[] { 1,0,0,0,1, 1,0,0,1,0, 1,0,1,0,0, 1,1,0,0,0, 1,0,1,0,0, 1,0,0,1,0, 1,0,0,0,1 },
                ['L'] = new[] { 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0, 1,1,1,1,1 },
                ['M'] = new[] { 1,0,0,0,1, 1,1,0,1,1, 1,0,1,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1 },
                ['N'] = new[] { 1,0,0,0,1, 1,1,0,0,1, 1,0,1,0,1, 1,0,0,1,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1 },
                ['O'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['P'] = new[] { 1,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,0, 1,0,0,0,0, 1,0,0,0,0, 1,0,0,0,0 },
                ['Q'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,1,0,1, 1,0,0,1,0, 0,1,1,0,1 },
                ['R'] = new[] { 1,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,0, 1,0,1,0,0, 1,0,0,1,0, 1,0,0,0,1 },
                ['S'] = new[] { 0,1,1,1,1, 1,0,0,0,0, 1,0,0,0,0, 0,1,1,1,0, 0,0,0,0,1, 0,0,0,0,1, 1,1,1,1,0 },
                ['T'] = new[] { 1,1,1,1,1, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0 },
                ['U'] = new[] { 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['V'] = new[] { 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 0,1,0,1,0, 0,1,0,1,0, 0,0,1,0,0 },
                ['W'] = new[] { 1,0,0,0,1, 1,0,0,0,1, 1,0,0,0,1, 1,0,1,0,1, 1,0,1,0,1, 1,1,0,1,1, 1,0,0,0,1 },
                ['X'] = new[] { 1,0,0,0,1, 0,1,0,1,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,1,0,1,0, 1,0,0,0,1 },
                ['Y'] = new[] { 1,0,0,0,1, 0,1,0,1,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0 },
                ['Z'] = new[] { 1,1,1,1,1, 0,0,0,0,1, 0,0,0,1,0, 0,0,1,0,0, 0,1,0,0,0, 1,0,0,0,0, 1,1,1,1,1 },
                ['0'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,1,1, 1,0,1,0,1, 1,1,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['1'] = new[] { 0,0,1,0,0, 0,1,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,1,1,1,0 },
                ['2'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 0,0,0,0,1, 0,0,0,1,0, 0,0,1,0,0, 0,1,0,0,0, 1,1,1,1,1 },
                ['3'] = new[] { 1,1,1,1,0, 0,0,0,0,1, 0,0,0,0,1, 0,1,1,1,0, 0,0,0,0,1, 0,0,0,0,1, 1,1,1,1,0 },
                ['4'] = new[] { 0,0,0,1,0, 0,0,1,1,0, 0,1,0,1,0, 1,0,0,1,0, 1,1,1,1,1, 0,0,0,1,0, 0,0,0,1,0 },
                ['5'] = new[] { 1,1,1,1,1, 1,0,0,0,0, 1,1,1,1,0, 0,0,0,0,1, 0,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['6'] = new[] { 0,1,1,1,0, 1,0,0,0,0, 1,0,0,0,0, 1,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['7'] = new[] { 1,1,1,1,1, 0,0,0,0,1, 0,0,0,1,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,0,1,0,0 },
                ['8'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,0 },
                ['9'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 1,0,0,0,1, 0,1,1,1,1, 0,0,0,0,1, 0,0,0,0,1, 0,1,1,1,0 },
                ['?'] = new[] { 0,1,1,1,0, 1,0,0,0,1, 0,0,0,0,1, 0,0,0,1,0, 0,0,1,0,0, 0,0,0,0,0, 0,0,1,0,0 },
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // MOD HUB SELF-REGISTRATION
        // ═══════════════════════════════════════════════════════════════

        private ModHubSettings _modHubSettings;

        /// <summary>
        /// Register Mod Hub itself as a mod so users can configure it.
        /// </summary>
        public void RegisterModHub()
        {
            _modHubSettings = new ModHubSettings();
            RegisterMod(_modHubSettings);
            Plugin.Log.LogInfo("[UIFramework] Registered Mod Hub as native mod");
        }

        /// <summary>
        /// Get the ModId of Mod Hub for navigation purposes.
        /// </summary>
        public static string ModHubModId => AssemblyInfo.ModGUID;

        // ═══════════════════════════════════════════════════════════════
        // AUTO-DISCOVERY OF BEPINEX PLUGINS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Automatically discover and register all BepInEx plugins that have config entries.
        /// This allows any mod to appear in UIFramework without explicit integration.
        /// </summary>
        public void DiscoverBepInExPlugins()
        {
            Plugin.Log.LogInfo("[UIFramework] Starting auto-discovery of BepInEx plugins...");
            Plugin.Log.LogInfo($"[UIFramework] Total plugins in Chainloader: {Chainloader.PluginInfos.Count}");

            int discovered = 0;
            int skipped = 0;

            foreach (var kvp in Chainloader.PluginInfos)
            {
                string pluginGUID = kvp.Key;
                var pluginInfo = kvp.Value;

                // Skip UIFramework itself
                if (pluginGUID == "hakusai.modhub")
                    continue;

                // Skip if already registered (explicit registration takes priority)
                if (_mods.ContainsKey(pluginGUID))
                {
                    Plugin.Log.LogInfo($"[UIFramework] Skipping '{pluginGUID}' - already registered natively");
                    skipped++;
                    continue;
                }

                try
                {
                    var plugin = pluginInfo.Instance as BaseUnityPlugin;

                    // Also check if this plugin is already registered under a different ModId
                    if (plugin != null && IsPluginAlreadyRegistered(plugin, pluginGUID))
                    {
                        Plugin.Log.LogInfo($"[UIFramework] Skipping '{pluginGUID}' - plugin already registered under different ModId");
                        skipped++;
                        continue;
                    }
                    if (plugin == null)
                    {
                        Plugin.Log.LogWarning($"[UIFramework] '{pluginGUID}' - Instance is null");
                        continue;
                    }

                    // Get ConfigFile - it's a public property in BaseUnityPlugin
                    var configFile = plugin.Config;
                    if (configFile == null)
                    {
                        Plugin.Log.LogWarning($"[UIFramework] '{pluginGUID}' - Config is null");
                        continue;
                    }

                    // Get config entries - try multiple approaches
                    var configEntries = GetConfigEntries(configFile, pluginGUID);

                    // Skip plugins with no config entries
                    if (configEntries.Count == 0)
                    {
                        Plugin.Log.LogInfo($"[UIFramework] '{pluginGUID}' - No config entries, skipping");
                        continue;
                    }

                    // Create ModInfo from BepInEx plugin
                    var modInfo = CreateModInfoFromBepInEx(pluginInfo, plugin, configEntries);
                    if (modInfo != null)
                    {
                        _mods[pluginGUID] = modInfo;
                        discovered++;
                        Plugin.Log.LogInfo($"[UIFramework] Auto-discovered: {modInfo.ModName} ({configEntries.Count} settings)");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[UIFramework] Failed to auto-discover '{pluginGUID}': {ex}");
                }
            }

            Plugin.Log.LogInfo($"[UIFramework] Auto-discovery complete. Found {discovered} external mod(s), {skipped} native mod(s).");
        }

        /// <summary>
        /// Get config entries from a ConfigFile using multiple approaches.
        /// </summary>
        private List<ConfigEntryBase> GetConfigEntries(ConfigFile configFile, string pluginGUID)
        {
            var configEntries = new List<ConfigEntryBase>();

            // Approach 1: Try to access internal dictionary via reflection
            var possibleFieldNames = new[] { "_entries", "Entries", "_allConfigEntries", "entries" };

            foreach (var fieldName in possibleFieldNames)
            {
                var field = typeof(ConfigFile).GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (field != null)
                {
                    var dict = field.GetValue(configFile) as System.Collections.IDictionary;
                    if (dict != null && dict.Count > 0)
                    {
                        foreach (var value in dict.Values)
                        {
                            if (value is ConfigEntryBase entryBase)
                            {
                                configEntries.Add(entryBase);
                            }
                        }
                        if (configEntries.Count > 0)
                        {
                            Plugin.Log.LogInfo($"[UIFramework] '{pluginGUID}' - Found {configEntries.Count} entries via field '{fieldName}'");
                            return configEntries;
                        }
                    }
                }
            }

            // Approach 2: Try using Keys property and indexer
            try
            {
                var keys = configFile.Keys;
                if (keys != null)
                {
                    foreach (var key in keys)
                    {
                        // Use indexer
                        var entry = configFile[key];
                        if (entry != null)
                        {
                            configEntries.Add(entry);
                        }
                    }
                    if (configEntries.Count > 0)
                    {
                        Plugin.Log.LogInfo($"[UIFramework] '{pluginGUID}' - Found {configEntries.Count} entries via Keys indexer");
                        return configEntries;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[UIFramework] '{pluginGUID}' - Keys approach failed: {ex.Message}");
            }

            // Approach 3: Try IEnumerable
            try
            {
                var enumerable = configFile as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (var item in enumerable)
                    {
                        // item is KeyValuePair<ConfigDefinition, ConfigEntryBase>
                        var itemType = item.GetType();
                        var valueProp = itemType.GetProperty("Value");
                        if (valueProp != null)
                        {
                            var entry = valueProp.GetValue(item) as ConfigEntryBase;
                            if (entry != null)
                            {
                                configEntries.Add(entry);
                            }
                        }
                    }
                    if (configEntries.Count > 0)
                    {
                        Plugin.Log.LogInfo($"[UIFramework] '{pluginGUID}' - Found {configEntries.Count} entries via IEnumerable");
                        return configEntries;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[UIFramework] '{pluginGUID}' - IEnumerable approach failed: {ex.Message}");
            }

            return configEntries;
        }

        /// <summary>
        /// Create ModInfo from a BepInEx plugin's ConfigFile.
        /// </summary>
        private ModInfo CreateModInfoFromBepInEx(PluginInfo pluginInfo, BaseUnityPlugin plugin, List<ConfigEntryBase> entries)
        {
            var metadata = pluginInfo.Metadata;
            var assembly = plugin.GetType().Assembly;

            // Try to get author from assembly attributes
            string author = "Unknown";
            string description = "";

            try
            {
                var companyAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyCompanyAttribute), false)
                    .FirstOrDefault() as System.Reflection.AssemblyCompanyAttribute;
                if (companyAttr != null && !string.IsNullOrEmpty(companyAttr.Company))
                    author = companyAttr.Company;

                var descAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyDescriptionAttribute), false)
                    .FirstOrDefault() as System.Reflection.AssemblyDescriptionAttribute;
                if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description))
                    description = descAttr.Description;
            }
            catch { }

            var modInfo = new ModInfo
            {
                ModId = metadata.GUID,
                ModName = metadata.Name,
                ModVersion = metadata.Version.ToString(),
                ModAuthor = author,
                Description = !string.IsNullOrEmpty(description) ? description : $"BepInEx plugin",
                Tags = new[] { "External" },
                IsAutoDiscovered = true,
                BepInExPlugin = plugin,
                ConfigEntries = entries
            };

            // Try to find plugin folder for potential icon and images
            string pluginPath = pluginInfo.Location;
            if (!string.IsNullOrEmpty(pluginPath))
            {
                modInfo.PluginFolder = Path.GetDirectoryName(pluginPath);

                // Try to find an icon file
                string[] possibleIcons = { "icon.png", "Icon.png", "icon.jpg", "cover.png", "Cover.png" };
                foreach (var iconName in possibleIcons)
                {
                    string iconPath = Path.Combine(modInfo.PluginFolder, iconName);
                    if (File.Exists(iconPath))
                    {
                        modInfo.IconTexture = LoadTexture(iconPath, isIcon: true);
                        break;
                    }
                }

                // Try to find carousel images in images/ folder
                string imagesFolder = Path.Combine(modInfo.PluginFolder, "images");
                if (Directory.Exists(imagesFolder))
                {
                    var imageFiles = Directory.GetFiles(imagesFolder, "*.png")
                        .Concat(Directory.GetFiles(imagesFolder, "*.jpg"))
                        .OrderBy(f => f)
                        .Take(10); // Max 10 images

                    foreach (var imgPath in imageFiles)
                    {
                        var tex = LoadTexture(imgPath);
                        if (tex != null)
                        {
                            modInfo.ImageTextures.Add(tex);
                        }
                    }
                }
            }

            // Try to load embedded resources from DLL
            if (modInfo.IconTexture == null)
            {
                modInfo.IconTexture = LoadEmbeddedTexture(assembly, "icon.png") ??
                                      LoadEmbeddedTexture(assembly, "Icon.png") ??
                                      LoadEmbeddedTexture(assembly, "cover.png");
            }

            // Get cached or generate initials icon if still no icon
            if (modInfo.IconTexture == null)
            {
                modInfo.IconTexture = GetOrCreateCachedIcon(modInfo.ModName, modInfo.ModId);
            }

            // Build sections from config entries (grouped by section name)
            BuildSectionsFromConfig(modInfo, entries);

            return modInfo;
        }

        /// <summary>
        /// Load texture from embedded resource in assembly.
        /// </summary>
        private Texture2D LoadEmbeddedTexture(Assembly assembly, string resourceName, bool isIcon = true)
        {
            try
            {
                // Try to find resource by name (can be namespace.resourceName or just resourceName)
                var names = assembly.GetManifestResourceNames();
                string fullName = names.FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(fullName))
                    return null;

                using (var stream = assembly.GetManifestResourceStream(fullName))
                {
                    if (stream == null) return null;

                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);

                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(data))
                    {
                        int maxSize = isIcon ? MAX_ICON_SIZE : MAX_IMAGE_SIZE;
                        tex = ResizeIfNeeded(tex, maxSize);

                        // Apply rounded corners to icons for consistent style
                        if (isIcon && tex != null)
                        {
                            tex = ApplyRoundedCorners(tex);
                        }

                        return tex;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[UIFramework] Failed to load embedded resource '{resourceName}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Build UI sections from BepInEx ConfigEntry list.
        /// </summary>
        private void BuildSectionsFromConfig(ModInfo modInfo, List<ConfigEntryBase> entries)
        {
            // Group entries by section
            var groups = entries.GroupBy(e => e.Definition.Section);

            int sectionOrder = 0;
            foreach (var group in groups.OrderBy(g => g.Key))
            {
                var section = new SectionInfo
                {
                    Name = group.Key,
                    Order = sectionOrder++,
                    Fields = new List<FieldInfo_UIF>()
                };

                int fieldOrder = 0;
                foreach (var entry in group.OrderBy(e => e.Definition.Key))
                {
                    var field = new FieldInfo_UIF
                    {
                        Name = entry.Definition.Key,
                        DisplayName = FormatFieldName(entry.Definition.Key),
                        Tooltip = entry.Description?.Description ?? "",
                        Order = fieldOrder++,
                        FieldType = entry.SettingType,
                        IsConfigEntry = true,
                        ConfigEntry = entry
                    };

                    // Extract range/list from AcceptableValue if present
                    if (entry.Description?.AcceptableValues != null)
                    {
                        var acceptable = entry.Description.AcceptableValues;
                        var acceptableType = acceptable.GetType();

                        if (acceptableType.IsGenericType)
                        {
                            var genericDef = acceptableType.GetGenericTypeDefinition();

                            // AcceptableValueRange<T> - slider
                            if (genericDef == typeof(AcceptableValueRange<>))
                            {
                                var minProp = acceptableType.GetProperty("MinValue");
                                var maxProp = acceptableType.GetProperty("MaxValue");

                                if (minProp != null && maxProp != null)
                                {
                                    field.HasRange = true;
                                    field.MinValue = Convert.ToSingle(minProp.GetValue(acceptable));
                                    field.MaxValue = Convert.ToSingle(maxProp.GetValue(acceptable));
                                }
                            }
                            // AcceptableValueList<T> - dropdown
                            else if (genericDef == typeof(AcceptableValueList<>))
                            {
                                var valuesProp = acceptableType.GetProperty("AcceptableValues");
                                if (valuesProp != null)
                                {
                                    var valuesArray = valuesProp.GetValue(acceptable) as Array;
                                    if (valuesArray != null && valuesArray.Length > 0)
                                    {
                                        field.HasAcceptableValues = true;
                                        field.AcceptableValues = new object[valuesArray.Length];
                                        for (int i = 0; i < valuesArray.Length; i++)
                                        {
                                            field.AcceptableValues[i] = valuesArray.GetValue(i);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    section.Fields.Add(field);
                }

                modInfo.Sections.Add(section);
            }
        }

        /// <summary>
        /// Format a config key name into a display-friendly format.
        /// "EnableFeature" -> "Enable Feature"
        /// </summary>
        private string FormatFieldName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var result = new System.Text.StringBuilder();
            result.Append(name[0]);

            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    result.Append(' ');
                }
                result.Append(name[i]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Check if a BepInEx plugin is already registered (under any ModId).
        /// This prevents duplicates when a mod natively registers with a different ModId than its GUID.
        /// </summary>
        private bool IsPluginAlreadyRegistered(BaseUnityPlugin plugin, string bepinexGUID)
        {
            foreach (var mod in _mods.Values)
            {
                // Check by plugin instance
                if (mod.BepInExPlugin != null && mod.BepInExPlugin == plugin)
                    return true;

                // Check if ModId contains the GUID or vice versa (fuzzy match)
                if (mod.ModId.IndexOf(bepinexGUID, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    bepinexGUID.IndexOf(mod.ModId, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                // Check if the mod name matches closely
                var pluginName = plugin.Info?.Metadata?.Name;
                if (!string.IsNullOrEmpty(pluginName) &&
                    mod.ModName.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
