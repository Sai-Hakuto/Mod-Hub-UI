using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UIFramework.Core;
using UIFramework.UI.Components;
using UIFramework.UI.Styles;
using UnityEngine;

namespace UIFramework.UI.Pages
{
    /// <summary>
    /// Page showing settings for a specific mod.
    /// </summary>
    public class ModSettingsPage
    {
        // Carousel state
        private int _carouselIndex;
        private int _carouselPrevIndex;
        private float _carouselTimer;
        private float _transitionProgress;
        private bool _isTransitioning;
        private const float CAROUSEL_INTERVAL = 8f;
        private const float TRANSITION_DURATION = 0.5f;

        // Image popup state
        private bool _showImagePopup;
        private Texture2D _popupImage;

        // Tooltip system
        private string _activeTooltip = "";
        private Rect _tooltipRect;
        private bool _showTooltip;

        // Track current mod for dropdown handling
        private ModInfo _currentMod;

        // Collapsed sections state - stores sections toggled FROM default state
        private static HashSet<string> _toggledSections;
        private static bool _collapsedLoaded;
        private static bool _lastCollapseByDefault;

        // Tag popup state
        private bool _showTagPopup;
        private Vector2 _tagPopupScroll;
        private string _newTagInput = "";
        private string _tagError = "";
        private string _tagToDelete = null; // Tag pending deletion confirmation

        private static void LoadCollapsedSections()
        {
            if (_collapsedLoaded) return;
            _collapsedLoaded = true;
            _toggledSections = new HashSet<string>();
            _lastCollapseByDefault = Plugin.CollapseSectionsByDefaultConfig?.Value ?? false;

            var saved = Plugin.CollapsedSectionsConfig?.Value ?? "";
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var item in saved.Split(','))
                {
                    if (!string.IsNullOrEmpty(item))
                        _toggledSections.Add(item.Trim());
                }
            }
        }

        private static void SaveCollapsedSections()
        {
            Plugin.CollapsedSectionsConfig.Value = string.Join(",", _toggledSections);
        }

        private static bool IsSectionCollapsed(string modId, string sectionName)
        {
            LoadCollapsedSections();

            // Check if "Collapse by default" setting changed - if so, clear user toggles
            bool currentCollapseByDefault = Plugin.CollapseSectionsByDefaultConfig?.Value ?? false;
            if (currentCollapseByDefault != _lastCollapseByDefault)
            {
                _toggledSections.Clear();
                SaveCollapsedSections();
                _lastCollapseByDefault = currentCollapseByDefault;
            }

            string key = $"{modId}|{sectionName}";
            bool isToggled = _toggledSections.Contains(key);

            // If collapse by default is ON: collapsed unless toggled (expanded)
            // If collapse by default is OFF: expanded unless toggled (collapsed)
            return currentCollapseByDefault ? !isToggled : isToggled;
        }

        private static void ToggleSectionCollapsed(string modId, string sectionName)
        {
            LoadCollapsedSections();
            string key = $"{modId}|{sectionName}";
            if (_toggledSections.Contains(key))
                _toggledSections.Remove(key);
            else
                _toggledSections.Add(key);
            SaveCollapsedSections();
        }

        public void Draw(ModInfo mod)
        {
            _currentMod = mod;

            // Reset tooltip each frame
            _showTooltip = false;
            _activeTooltip = "";

            try
            {
                // ═══════════════════════════════════════════════════════════════
                // IMAGE CAROUSEL (if images available)
                // ═══════════════════════════════════════════════════════════════
                try
                {
                    if (mod.ImageTextures != null && mod.ImageTextures.Count > 0)
                    {
                        DrawCarousel(mod);
                        GUILayout.Space(15);
                    }
                }
                catch (Exception ex)
                {
                    GUILayout.Label($"[Carousel error: {ex.Message}]", UIStyles.ErrorStyle);
                }

                // ═══════════════════════════════════════════════════════════════
                // MOD INFO
                // ═══════════════════════════════════════════════════════════════
                GUILayout.BeginHorizontal();

                // Icon
                if (mod.IconTexture != null)
                {
                    GUILayout.Box(mod.IconTexture, GUILayout.Width(64), GUILayout.Height(64));
                    GUILayout.Space(15);
                }

                GUILayout.BeginVertical();

                // Name and version
                GUILayout.Label($"{mod.ModName ?? "Unknown"} v{mod.ModVersion ?? "?"}", UIStyles.TitleStyle);
                GUILayout.Label($"by {mod.ModAuthor ?? "Unknown"}", UIStyles.LabelMutedStyle);

                // Description
                if (!string.IsNullOrEmpty(mod.Description))
                {
                    GUILayout.Space(5);
                    GUILayout.Label(mod.Description, UIStyles.LabelStyle);
                }

                GUILayout.EndVertical();

                // Favorite/Hide buttons
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();

                float scale = UIStyles.ScaleFactor;
                float sideBtnWidth = 80 * scale;
                float sideBtnHeight = 24 * scale;

                bool isFav = ModRegistry.Instance.IsFavorite(mod.ModId);
                bool isHidden = ModRegistry.Instance.IsHidden(mod.ModId);

                GUIStyle favStyle = isFav ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;
                if (GUILayout.Button(isFav ? "Unfavorite" : "Favorite", favStyle, GUILayout.Width(sideBtnWidth), GUILayout.Height(sideBtnHeight)))
                {
                    ModRegistry.Instance.ToggleFavorite(mod.ModId);
                }

                GUILayout.Space(4 * scale);

                // Tags button
                int tagCount = 0;
                foreach (var _ in ModRegistry.Instance.GetModCustomTags(mod.ModId)) tagCount++;
                string tagBtnText = tagCount > 0 ? $"Tags ({tagCount})" : "Tags";
                GUIStyle tagStyle = (tagCount > 0 || _showTagPopup) ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;
                if (GUILayout.Button(tagBtnText, tagStyle, GUILayout.Width(sideBtnWidth), GUILayout.Height(sideBtnHeight)))
                {
                    _showTagPopup = !_showTagPopup;
                }

                GUILayout.Space(4 * scale);

                GUIStyle hideStyle = isHidden ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;
                if (GUILayout.Button(isHidden ? "Unhide" : "Hide", hideStyle, GUILayout.Width(sideBtnWidth), GUILayout.Height(sideBtnHeight)))
                {
                    ModRegistry.Instance.ToggleHidden(mod.ModId);
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                // ═══════════════════════════════════════════════════════════════
                // TAGS (author + custom)
                // ═══════════════════════════════════════════════════════════════
                try
                {
                    var customTags = ModRegistry.Instance.GetModCustomTags(mod.ModId);
                    bool hasAnyTags = mod.Tags != null && mod.Tags.Length > 0;
                    foreach (var _ in customTags) { hasAnyTags = true; break; }

                    if (hasAnyTags)
                    {
                        GUILayout.Space(10);
                        GUILayout.BeginHorizontal();

                        // Author tags
                        if (mod.Tags != null)
                        {
                            foreach (var tag in mod.Tags)
                            {
                                GUILayout.Label(tag, UIStyles.TagStyle);
                            }
                        }

                        // Custom tags (with different style)
                        foreach (var tag in customTags)
                        {
                            GUILayout.Label(tag, UIStyles.TagAccentStyle);
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }
                catch (Exception ex)
                {
                    GUILayout.Label($"[Tags error: {ex.Message}]", UIStyles.ErrorStyle);
                }

                GUILayout.Space(15);

                // ═══════════════════════════════════════════════════════════════
                // SETTINGS SECTIONS
                // ═══════════════════════════════════════════════════════════════
                bool showAdvanced = Plugin.ShowAdvanced?.Value ?? false;

                if (mod.Sections != null)
                {
                    foreach (var section in mod.Sections)
                    {
                        DrawSection(mod, section, showAdvanced);
                    }
                }

                GUILayout.Space(20);
            }
            catch (Exception ex)
            {
                // Fatal error - draw error message
                GUILayout.Label($"Error rendering mod settings: {ex.Message}", UIStyles.ErrorStyle);
                GUILayout.Space(10);
                GUILayout.Label("Please report this issue to the Mod Hub developer.", UIStyles.LabelMutedStyle);
            }
        }

        private void DrawCarousel(ModInfo mod)
        {
            if (mod.ImageTextures.Count == 0) return;

            float scale = UIStyles.ScaleFactor;
            int imageCount = mod.ImageTextures.Count;

            // Handle transition animation
            if (_isTransitioning)
            {
                _transitionProgress += Time.deltaTime / TRANSITION_DURATION;
                if (_transitionProgress >= 1f)
                {
                    _transitionProgress = 0f;
                    _isTransitioning = false;
                }
            }
            else
            {
                // Auto-advance carousel
                _carouselTimer += Time.deltaTime;
                if (_carouselTimer >= CAROUSEL_INTERVAL && imageCount > 1)
                {
                    StartTransition((_carouselIndex + 1) % imageCount);
                }
            }

            float carouselHeight = 200f * scale;
            float mainImageWidth = 500f * scale;
            float previewWidth = 80f * scale;
            float buttonWidth = 35f * scale;
            float spacing = 8f * scale;

            GUILayout.BeginHorizontal(UIStyles.BoxStyle, GUILayout.Height(carouselHeight));

            // Previous button
            if (imageCount > 1)
            {
                if (GUILayout.Button("<", UIStyles.ButtonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(carouselHeight - 20 * scale)))
                {
                    int newIndex = (_carouselIndex - 1 + imageCount) % imageCount;
                    StartTransition(newIndex);
                }
            }

            GUILayout.Space(spacing);

            // Left preview (previous image)
            if (imageCount > 1)
            {
                int prevIndex = (_carouselIndex - 1 + imageCount) % imageCount;
                var prevTex = mod.ImageTextures[prevIndex];
                if (prevTex != null)
                {
                    Rect prevRect = GUILayoutUtility.GetRect(previewWidth, carouselHeight - 20 * scale);
                    Color prevColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.DrawTexture(prevRect, prevTex, ScaleMode.ScaleToFit);
                    GUI.color = prevColor;
                }
            }

            GUILayout.Space(spacing);

            // Main image area with fade transition
            Rect mainRect = GUILayoutUtility.GetRect(mainImageWidth, carouselHeight - 20 * scale);

            Texture2D currentTex = null;
            if (_isTransitioning && _carouselPrevIndex >= 0 && _carouselPrevIndex < imageCount)
            {
                // Draw fading out image
                var prevTex = mod.ImageTextures[_carouselPrevIndex];
                if (prevTex != null)
                {
                    Color fadeOut = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 1f - _transitionProgress);
                    GUI.DrawTexture(mainRect, prevTex, ScaleMode.ScaleToFit);
                    GUI.color = fadeOut;
                }

                // Draw fading in image
                var newTex = mod.ImageTextures[_carouselIndex];
                if (newTex != null)
                {
                    Color fadeIn = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, _transitionProgress);
                    GUI.DrawTexture(mainRect, newTex, ScaleMode.ScaleToFit);
                    GUI.color = fadeIn;
                    currentTex = newTex;
                }
            }
            else
            {
                // Draw current image
                var tex = mod.ImageTextures[_carouselIndex];
                if (tex != null)
                {
                    GUI.DrawTexture(mainRect, tex, ScaleMode.ScaleToFit);
                    currentTex = tex;
                }
            }

            // Click on main image to open popup
            if (Event.current.type == EventType.MouseDown && mainRect.Contains(Event.current.mousePosition) && currentTex != null)
            {
                _popupImage = currentTex;
                _showImagePopup = true;
                Event.current.Use();
            }

            GUILayout.Space(spacing);

            // Right preview (next image)
            if (imageCount > 1)
            {
                int nextIndex = (_carouselIndex + 1) % imageCount;
                var nextTex = mod.ImageTextures[nextIndex];
                if (nextTex != null)
                {
                    Rect nextRect = GUILayoutUtility.GetRect(previewWidth, carouselHeight - 20 * scale);
                    Color nextColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.DrawTexture(nextRect, nextTex, ScaleMode.ScaleToFit);
                    GUI.color = nextColor;
                }
            }

            GUILayout.Space(spacing);

            // Next button
            if (imageCount > 1)
            {
                if (GUILayout.Button(">", UIStyles.ButtonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(carouselHeight - 20 * scale)))
                {
                    int newIndex = (_carouselIndex + 1) % imageCount;
                    StartTransition(newIndex);
                }
            }

            GUILayout.EndHorizontal();

            // Dots indicator
            if (imageCount > 1)
            {
                GUILayout.Space(5 * scale);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int i = 0; i < imageCount; i++)
                {
                    bool isActive = i == _carouselIndex;
                    GUIStyle dotStyle = isActive ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;
                    float dotSize = isActive ? 14 * scale : 10 * scale;

                    if (GUILayout.Button("", dotStyle, GUILayout.Width(dotSize), GUILayout.Height(dotSize)))
                    {
                        if (i != _carouselIndex)
                        {
                            StartTransition(i);
                        }
                    }
                    GUILayout.Space(4 * scale);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private void StartTransition(int newIndex)
        {
            if (newIndex == _carouselIndex) return;

            _carouselPrevIndex = _carouselIndex;
            _carouselIndex = newIndex;
            _carouselTimer = 0f;
            _transitionProgress = 0f;
            _isTransitioning = true;
        }

        /// <summary>
        /// Check if image popup is open.
        /// </summary>
        public bool IsImagePopupOpen => _showImagePopup;

        /// <summary>
        /// Draw full-screen image popup.
        /// </summary>
        public void DrawImagePopup(Rect windowRect)
        {
            if (!_showImagePopup || _popupImage == null) return;

            float scale = UIStyles.ScaleFactor;

            // Darken background
            GUI.Box(new Rect(0, 0, windowRect.width, windowRect.height), "", UIStyles.PopupStyle);

            // Calculate image size to fit in window with padding
            float padding = 60 * scale;
            float maxWidth = windowRect.width - padding * 2;
            float maxHeight = windowRect.height - padding * 2 - 40 * scale; // Extra space for close button

            float imgAspect = (float)_popupImage.width / _popupImage.height;
            float boxAspect = maxWidth / maxHeight;

            float imgWidth, imgHeight;
            if (imgAspect > boxAspect)
            {
                // Image is wider - fit by width
                imgWidth = maxWidth;
                imgHeight = maxWidth / imgAspect;
            }
            else
            {
                // Image is taller - fit by height
                imgHeight = maxHeight;
                imgWidth = maxHeight * imgAspect;
            }

            // Center the image
            float imgX = (windowRect.width - imgWidth) / 2;
            float imgY = (windowRect.height - imgHeight) / 2;

            Rect imageRect = new Rect(imgX, imgY, imgWidth, imgHeight);
            GUI.DrawTexture(imageRect, _popupImage, ScaleMode.ScaleToFit);

            // Close button in top-right corner
            float btnSize = 40 * scale;
            Rect closeRect = new Rect(windowRect.width - padding - btnSize, padding / 2, btnSize, btnSize);
            if (GUI.Button(closeRect, "X", UIStyles.ButtonAccentStyle))
            {
                _showImagePopup = false;
                _popupImage = null;
            }

            // Click anywhere outside image to close
            if (Event.current.type == EventType.MouseDown && !imageRect.Contains(Event.current.mousePosition))
            {
                _showImagePopup = false;
                _popupImage = null;
                Event.current.Use();
            }

            // ESC key to close
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _showImagePopup = false;
                _popupImage = null;
                Event.current.Use();
            }
        }

        private void DrawSection(ModInfo mod, SectionInfo section, bool showAdvanced)
        {
            try
            {
                // Count visible fields first
                int visibleCount = 0;
                foreach (var field in section.Fields)
                {
                    if (!field.IsAdvanced || showAdvanced)
                        visibleCount++;
                }

                // Don't draw empty sections
                if (visibleCount == 0)
                    return;

                float scale = UIStyles.ScaleFactor;
                bool isCollapsed = IsSectionCollapsed(mod.ModId, section.SectionName);

                // Section header with collapse button
                GUILayout.BeginHorizontal();

                string arrow = isCollapsed ? ">" : "v";
                if (GUILayout.Button($"{arrow} {section.SectionName ?? "Settings"}", UIStyles.SectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(28 * scale)))
                {
                    ToggleSectionCollapsed(mod.ModId, section.SectionName);
                }

                GUILayout.EndHorizontal();

                // Only draw fields if not collapsed
                if (!isCollapsed)
                {
                    GUILayout.BeginVertical(UIStyles.BoxStyle);

                    foreach (var field in section.Fields)
                    {
                        // Skip advanced if not showing
                        if (field.IsAdvanced && !showAdvanced)
                            continue;

                        try
                        {
                            DrawField(mod, field);
                        }
                        catch (Exception ex)
                        {
                            // Draw error placeholder for failed field
                            GUILayout.BeginHorizontal();
                            GUILayout.Label($"[Error: {field.DisplayName ?? field.Name}]", UIStyles.ErrorStyle);
                            GUILayout.Label(ex.Message, UIStyles.LabelMutedStyle);
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        }
                    }

                    GUILayout.EndVertical();
                }

                GUILayout.Space(10);
            }
            catch (Exception ex)
            {
                // Section-level error - draw a simple error box
                GUILayout.Label($"[Section error: {section.SectionName ?? "Unknown"}] {ex.Message}", UIStyles.ErrorStyle);
                GUILayout.Space(10);
            }
        }

        private void DrawField(ModInfo mod, FieldInfo_UIF field)
        {
            float scale = UIStyles.ScaleFactor;
            float smallBtnHeight = 22 * scale;

            GUILayout.BeginHorizontal();

            try
            {
                // Advanced indicator
                if (field.IsAdvanced)
                {
                    GUILayout.Label("[A]", UIStyles.LabelMutedStyle, GUILayout.Width(25 * scale));
                }

                // Label - check for hover to show tooltip
                GUILayout.Label(field.DisplayName ?? "???", UIStyles.LabelStyle, GUILayout.Width(180 * scale));
                Rect labelRect = GUILayoutUtility.GetLastRect();

                // Check hover on label for tooltip
                if (!string.IsNullOrEmpty(field.Tooltip) && labelRect.Contains(Event.current.mousePosition))
                {
                    _showTooltip = true;
                    _activeTooltip = field.Tooltip;
                    _tooltipRect = new Rect(Event.current.mousePosition.x + 15, Event.current.mousePosition.y, 300, 0);
                }

                // Value editor
                object currentValue = null;
                object newValue = null;

                try
                {
                    currentValue = mod.GetFieldValue(field);
                    newValue = currentValue;
                }
                catch (Exception ex)
                {
                    GUILayout.Label($"[Error reading value: {ex.Message}]", UIStyles.ErrorStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2 * scale);
                    return;
                }

                // Draw control - sliders get more width for precision
                float sliderWidth = 350f * scale;
                float dropdownWidth = 180f * scale;

                try
                {
                    // KeyCode and KeyboardShortcut get specialized renderers (even with AcceptableValueList)
                    // because their AcceptableValueList just lists all keys which is handled better by the picker
                    if (field.FieldType == typeof(KeyboardShortcut))
                    {
                        string fieldId = $"{mod.ModId}_{field.Name}";
                        var shortcut = currentValue is KeyboardShortcut ks ? ks : new KeyboardShortcut(KeyCode.None);
                        newValue = FieldRenderers.DrawKeyboardShortcut(shortcut, fieldId, field.IsReadOnly);
                    }
                    else if (field.FieldType == typeof(KeyCode))
                    {
                        string fieldId = $"{mod.ModId}_{field.Name}";
                        var keyCode = currentValue is KeyCode kc ? kc : KeyCode.None;
                        newValue = FieldRenderers.DrawKeyCode(keyCode, fieldId, field.IsReadOnly);
                    }
                    // Check for AcceptableValueList (except for types with specialized renderers)
                    else if (field.HasAcceptableValues && field.AcceptableValues != null)
                    {
                        string fieldId = $"{mod.ModId}_{field.Name}";
                        newValue = FieldRenderers.DrawValueListDropdown(currentValue, field.AcceptableValues, fieldId, field.IsReadOnly);
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        newValue = FieldRenderers.DrawToggle((bool)(currentValue ?? false), field.IsReadOnly);
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        if (field.HasRange)
                        {
                            newValue = FieldRenderers.DrawSlider((float)(currentValue ?? 0f), field.Min, field.Max, field.Step, field.IsReadOnly, sliderWidth);
                        }
                        else
                        {
                            newValue = FieldRenderers.DrawFloatField((float)(currentValue ?? 0f), field.IsReadOnly);
                        }
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        if (field.HasRange)
                        {
                            float floatVal = FieldRenderers.DrawSlider((int)(currentValue ?? 0), field.Min, field.Max, 1f, field.IsReadOnly, sliderWidth);
                            newValue = Mathf.RoundToInt(floatVal);
                        }
                        else
                        {
                            newValue = FieldRenderers.DrawIntField((int)(currentValue ?? 0), field.IsReadOnly);
                        }
                    }
                    else if (field.FieldType == typeof(double))
                    {
                        if (field.HasRange)
                        {
                            float floatVal = FieldRenderers.DrawSlider((float)(double)(currentValue ?? 0.0), field.Min, field.Max, field.Step, field.IsReadOnly, sliderWidth);
                            newValue = (double)floatVal;
                        }
                        else
                        {
                            float floatVal = FieldRenderers.DrawFloatField((float)(double)(currentValue ?? 0.0), field.IsReadOnly);
                            newValue = (double)floatVal;
                        }
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        newValue = FieldRenderers.DrawTextField((string)currentValue ?? "", field.IsReadOnly, sliderWidth);
                    }
                    else if (field.FieldType != null && field.FieldType.IsEnum)
                    {
                        string fieldId = $"{mod.ModId}_{field.Name}";
                        // Check if it's a flags enum (multi-select)
                        if (FieldRenderers.IsFlagsEnum(field.FieldType))
                        {
                            newValue = FieldRenderers.DrawFlagsEnumField(currentValue, field.FieldType, fieldId, field.IsReadOnly);
                        }
                        else
                        {
                            newValue = FieldRenderers.DrawEnumDropdown(currentValue, field.FieldType, fieldId, field.IsReadOnly);
                        }
                    }
                    else if (field.FieldType == typeof(Color))
                    {
                        string fieldId = $"{mod.ModId}_{field.Name}";
                        var color = currentValue is Color c ? c : Color.white;
                        newValue = FieldRenderers.DrawColorField(color, fieldId, field.IsReadOnly);
                    }
                    else if (field.FieldType == typeof(Vector2))
                    {
                        var vec2 = currentValue is Vector2 v ? v : Vector2.zero;
                        newValue = DrawVector2Field(vec2, field.IsReadOnly);
                    }
                    else if (field.FieldType == typeof(Vector3))
                    {
                        var vec3 = currentValue is Vector3 v ? v : Vector3.zero;
                        newValue = FieldRenderers.DrawVector3Field(vec3, field.IsReadOnly);
                    }
                    else
                    {
                        // Unsupported type - show as read-only text
                        GUILayout.Label($"[{field.FieldType?.Name ?? "Unknown"}]: {currentValue}", UIStyles.LabelMutedStyle);
                    }
                }
                catch (Exception ex)
                {
                    GUILayout.Label($"[Render error: {ex.Message}]", UIStyles.ErrorStyle);
                }

                // FlexibleSpace pushes buttons to the right
                GUILayout.FlexibleSpace();

                // Tooltip button (show ? if has tooltip)
                if (!string.IsNullOrEmpty(field.Tooltip))
                {
                    if (GUILayout.Button("?", UIStyles.SmallButtonStyle, GUILayout.Width(24 * scale), GUILayout.Height(smallBtnHeight)))
                    {
                        // Click does nothing, hover shows tooltip
                    }
                    Rect btnRect = GUILayoutUtility.GetLastRect();
                    if (btnRect.Contains(Event.current.mousePosition))
                    {
                        _showTooltip = true;
                        _activeTooltip = field.Tooltip;
                        _tooltipRect = new Rect(Event.current.mousePosition.x + 15, Event.current.mousePosition.y, 300, 0);
                    }
                }
                else
                {
                    GUILayout.Space(28 * scale); // Keep alignment
                }

                // Reset to default button
                if (GUILayout.Button("Reset", UIStyles.SmallButtonStyle, GUILayout.Width(50 * scale), GUILayout.Height(smallBtnHeight)))
                {
                    mod.ResetField(field);
                }

                // Update value if changed
                if (newValue != null && !Equals(currentValue, newValue))
                {
                    try
                    {
                        mod.SetFieldValue(field, newValue);
                    }
                    catch (Exception)
                    {
                        // Silently ignore set errors
                    }
                }
            }
            catch (Exception ex)
            {
                // Fatal error in field rendering - show error and ensure layout is closed
                try
                {
                    GUILayout.Label($"[Field error: {ex.Message}]", UIStyles.ErrorStyle);
                    GUILayout.FlexibleSpace();
                }
                catch { }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(2 * scale);
        }

        /// <summary>
        /// Draw tooltip if active. Call this at the end of Draw.
        /// </summary>
        public void DrawTooltip()
        {
            if (_showTooltip && !string.IsNullOrEmpty(_activeTooltip))
            {
                // Use current mouse position (works correctly outside scroll view)
                Vector2 mousePos = Event.current.mousePosition;

                // Calculate tooltip size
                GUIContent content = new GUIContent(_activeTooltip);
                Vector2 size = UIStyles.TooltipStyle.CalcSize(content);
                size.x = Mathf.Min(size.x, 300);
                size.y = UIStyles.TooltipStyle.CalcHeight(content, size.x);

                // Position tooltip near mouse
                Rect tooltipRect = new Rect(mousePos.x + 15, mousePos.y + 10, size.x, size.y);

                // Keep on screen
                if (tooltipRect.xMax > 880)  // Window width ~900
                    tooltipRect.x = mousePos.x - size.x - 10;
                if (tooltipRect.yMax > 620)  // Window height ~650
                    tooltipRect.y = mousePos.y - size.y - 10;

                GUI.Box(tooltipRect, _activeTooltip, UIStyles.TooltipStyle);
            }
        }

        /// <summary>
        /// Draw any open dropdown popups. Call after EndScrollView.
        /// </summary>
        public void DrawDropdowns(Rect windowRect)
        {
            if (!FieldRenderers.IsDropdownOpen || _currentMod == null)
                return;

            string activeId = FieldRenderers.ActiveDropdownId;
            if (string.IsNullOrEmpty(activeId))
                return;

            // Find the field that has the open dropdown and draw popup
            foreach (var section in _currentMod.Sections)
            {
                foreach (var field in section.Fields)
                {
                    string fieldId = $"{_currentMod.ModId}_{field.Name}";

                    if (activeId == fieldId)
                    {
                        var result = FieldRenderers.DrawDropdownPopup(fieldId, windowRect);
                        if (result != null)
                        {
                            _currentMod.SetFieldValue(field, result);
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Apply scroll delta to the tag popup scroll position.
        /// Called from MainWindow when scroll wheel is used while popup is open.
        /// </summary>
        public void ApplyTagScrollDelta(float delta)
        {
            _tagPopupScroll.y += delta;
            if (_tagPopupScroll.y < 0) _tagPopupScroll.y = 0;
        }

        // Backdrop texture for popup
        private static Texture2D _tagBackdropTex;

        /// <summary>
        /// Draw tag popup if open. Call after EndScrollView.
        /// </summary>
        public void DrawTagPopup(Rect windowRect)
        {
            if (!_showTagPopup || _currentMod == null)
                return;

            float scale = UIStyles.ScaleFactor;

            // Create backdrop texture once
            if (_tagBackdropTex == null)
            {
                _tagBackdropTex = new Texture2D(1, 1);
                _tagBackdropTex.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
                _tagBackdropTex.Apply();
            }

            // Draw backdrop (covers entire window, catches clicks outside popup)
            Rect backdropRect = new Rect(0, 0, windowRect.width, windowRect.height);
            GUI.DrawTexture(backdropRect, _tagBackdropTex);

            // Calculate popup position (centered) - scaled
            float popupWidth = 280f * scale;
            float popupHeight = 450f * scale;

            float popupX = (windowRect.width - popupWidth) / 2;
            float popupY = (windowRect.height - popupHeight) / 2;

            Rect popupRect = new Rect(popupX, popupY, popupWidth, popupHeight);

            // Close if clicking outside popup
            if (Event.current.type == EventType.MouseDown && !popupRect.Contains(Event.current.mousePosition))
            {
                CloseTagPopup();
                Event.current.Use();
                return;
            }

            // Draw popup background
            GUI.Box(popupRect, "", UIStyles.PopupStyle);

            // Content area with scaled padding
            float padding = 12f * scale;
            float contentHeight = popupRect.height - padding * 2;

            // Calculate heights for proper layout - all scaled
            bool hasNativeTags = _currentMod.Tags != null && _currentMod.Tags.Length > 0;
            float headerHeight = 35f * scale;  // Title + space
            float nativeTagsHeight = hasNativeTags ? 55f * scale : 0f;  // Native tags section
            float availableTagsLabelHeight = 22f * scale;
            float newTagSectionHeight = 80f * scale;  // Label + input + error
            float footerHeight = 42f * scale;  // Close button
            float scrollHeight = contentHeight - headerHeight - nativeTagsHeight - availableTagsLabelHeight - newTagSectionHeight - footerHeight;

            GUILayout.BeginArea(new Rect(popupRect.x + padding, popupRect.y + padding, popupRect.width - padding * 2, contentHeight));

            // Header
            GUILayout.Label("Manage Tags", UIStyles.TitleStyle);
            GUILayout.Space(5 * scale);

            // ══════════════════════════════════════════════════════════
            // NATIVE TAGS (from mod author, cannot remove)
            // ══════════════════════════════════════════════════════════
            if (hasNativeTags)
            {
                GUILayout.Label("Native tags (from author):", UIStyles.LabelMutedStyle);
                GUILayout.BeginHorizontal();
                foreach (var tag in _currentMod.Tags)
                {
                    GUILayout.Label(tag, UIStyles.TagStyle);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(8 * scale);
            }

            // ══════════════════════════════════════════════════════════
            // SEARCH / CREATE TAG INPUT (moved to top for filtering)
            // ══════════════════════════════════════════════════════════
            GUILayout.Label("Search or create tag:", UIStyles.LabelMutedStyle);
            GUILayout.BeginHorizontal();

            _newTagInput = GUILayout.TextField(_newTagInput, 20, UIStyles.TextFieldStyle, GUILayout.Width(160 * scale));

            // Check if input matches any existing tag
            string searchQuery = _newTagInput?.Trim().ToLower() ?? "";
            bool exactMatch = false;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                foreach (var tag in ModRegistry.SystemTags)
                    if (tag.ToLower() == searchQuery) { exactMatch = true; break; }
                if (!exactMatch)
                    foreach (var tag in ModRegistry.Instance.CustomTags)
                        if (tag.ToLower() == searchQuery) { exactMatch = true; break; }
            }

            // Show "Add" button only if no exact match and input is valid
            bool canCreate = !string.IsNullOrWhiteSpace(_newTagInput) && !exactMatch && _newTagInput.Trim().Length <= 12;
            if (canCreate)
            {
                if (GUILayout.Button("Create", UIStyles.SmallButtonAccentStyle, GUILayout.Width(55 * scale), GUILayout.Height(24 * scale)))
                {
                    string trimmed = _newTagInput.Trim();
                    if (ModRegistry.Instance.CreateCustomTag(trimmed, out string error))
                    {
                        ModRegistry.Instance.AddTagToMod(_currentMod.ModId, trimmed);
                        _newTagInput = "";
                        _tagError = "";
                    }
                    else
                    {
                        _tagError = error;
                    }
                    Event.current.Use();
                }
            }

            GUILayout.EndHorizontal();

            // Show error if any
            if (!string.IsNullOrEmpty(_tagError))
            {
                GUILayout.Label(_tagError, UIStyles.ErrorStyle);
            }

            GUILayout.Space(5 * scale);

            // ══════════════════════════════════════════════════════════
            // AVAILABLE TAGS (filtered by search)
            // ══════════════════════════════════════════════════════════
            int matchCount = 0;
            GUILayout.Label("Available tags:", UIStyles.LabelMutedStyle);

            _tagPopupScroll = GUILayout.BeginScrollView(_tagPopupScroll, GUILayout.Height(scrollHeight));

            // System tags first (filtered, skip Hidden - it's managed automatically)
            foreach (var tag in ModRegistry.SystemTags)
            {
                if (tag.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(searchQuery) || tag.ToLower().Contains(searchQuery))
                {
                    DrawTagRow(tag, isSystem: true, scale);
                    matchCount++;
                }
            }

            // Then custom tags (filtered)
            foreach (var tag in ModRegistry.Instance.CustomTags)
            {
                if (string.IsNullOrEmpty(searchQuery) || tag.ToLower().Contains(searchQuery))
                {
                    DrawTagRow(tag, isSystem: false, scale);
                    matchCount++;
                }
            }

            // Show message if no matches
            if (matchCount == 0 && !string.IsNullOrEmpty(searchQuery))
            {
                GUILayout.Label($"No tags matching \"{_newTagInput.Trim()}\"", UIStyles.LabelMutedStyle);
                if (canCreate)
                {
                    GUILayout.Label("Click \"Create\" to add it", UIStyles.LabelMutedStyle);
                }
            }

            GUILayout.EndScrollView();

            // Show delete confirmation if pending
            DrawTagDeleteConfirmation(scale);

            GUILayout.Space(5 * scale);

            // Close button
            if (GUILayout.Button("Close", UIStyles.ButtonStyle, GUILayout.Height(30 * scale)))
            {
                CloseTagPopup();
                Event.current.Use();
            }

            GUILayout.EndArea();
        }

        private void DrawTagRow(string tag, bool isSystem, float scale)
        {
            // Skip if this is already a native tag from the mod
            if (_currentMod.Tags != null && Array.Exists(_currentMod.Tags, t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                return;

            bool hasTag = ModRegistry.Instance.ModHasTag(_currentMod.ModId, tag);

            GUILayout.BeginHorizontal();

            // Checkbox
            bool newHasTag = GUILayout.Toggle(hasTag, "", GUILayout.Width(22 * scale));
            if (newHasTag != hasTag)
            {
                if (newHasTag)
                    ModRegistry.Instance.AddTagToMod(_currentMod.ModId, tag);
                else
                    ModRegistry.Instance.RemoveTagFromMod(_currentMod.ModId, tag);
                Event.current.Use();
            }

            // Tag label (system tags shown with [S] prefix)
            if (isSystem)
            {
                GUILayout.Label($"[S] {tag}", UIStyles.LabelStyle);
            }
            else
            {
                GUILayout.Label(tag, UIStyles.LabelStyle);

                // Delete button (only for custom tags) - shows confirmation
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("x", UIStyles.SmallButtonStyle, GUILayout.Width(22 * scale), GUILayout.Height(20 * scale)))
                {
                    _tagToDelete = tag;
                    Event.current.Use();
                }
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw delete confirmation if pending.
        /// </summary>
        private void DrawTagDeleteConfirmation(float scale)
        {
            if (string.IsNullOrEmpty(_tagToDelete))
                return;

            GUILayout.Space(5 * scale);
            GUILayout.BeginVertical(UIStyles.BoxStyle);

            GUILayout.Label($"Delete tag \"{_tagToDelete}\"?", UIStyles.LabelStyle);
            GUILayout.Label("It will be removed from all mods.", UIStyles.LabelMutedStyle);

            GUILayout.Space(5 * scale);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Yes, delete", UIStyles.SmallButtonAccentStyle, GUILayout.Height(24 * scale)))
            {
                ModRegistry.Instance.DeleteCustomTag(_tagToDelete);
                _tagToDelete = null;
                Event.current.Use();
            }

            GUILayout.Space(5 * scale);

            if (GUILayout.Button("Cancel", UIStyles.SmallButtonStyle, GUILayout.Height(24 * scale)))
            {
                _tagToDelete = null;
                Event.current.Use();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Check if tag popup is currently open.
        /// </summary>
        public bool IsTagPopupOpen => _showTagPopup;

        /// <summary>
        /// Close the tag popup.
        /// </summary>
        public void CloseTagPopup()
        {
            _showTagPopup = false;
            _newTagInput = "";
            _tagError = "";
            _tagToDelete = null;
        }

        /// <summary>
        /// Draw Vector2 field (X, Y).
        /// </summary>
        private Vector2 DrawVector2Field(Vector2 value, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            float labelWidth = 22 * scale;

            GUILayout.BeginHorizontal();

            GUILayout.Label("X:", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
            value.x = FieldRenderers.DrawFloatField(value.x, readOnly);

            GUILayout.Label("Y:", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
            value.y = FieldRenderers.DrawFloatField(value.y, readOnly);

            GUILayout.EndHorizontal();

            return value;
        }
    }
}
