using System.Collections.Generic;
using System.Linq;
using UIFramework.Core;
using UIFramework.UI.Styles;
using UnityEngine;

namespace UIFramework.UI.Pages
{
    /// <summary>
    /// Search and filter page with tags.
    /// </summary>
    public class SearchPage
    {
        private string _searchQuery = "";
        private HashSet<string> _selectedTags = new HashSet<string>();
        private List<ModInfo> _filteredMods = new List<ModInfo>();
        private bool _needsRefresh = true;

        public void Draw()
        {
            float scale = UIStyles.ScaleFactor;

            // ═══════════════════════════════════════════════════════════════
            // SEARCH BAR
            // ═══════════════════════════════════════════════════════════════
            GUILayout.BeginHorizontal();

            GUILayout.Label("Search:", UIStyles.LabelStyle, GUILayout.Width(60 * scale));

            string newQuery = GUILayout.TextField(_searchQuery, UIStyles.TextFieldStyle, GUILayout.Width(400 * scale));
            if (newQuery != _searchQuery)
            {
                _searchQuery = newQuery;
                _needsRefresh = true;
            }

            GUILayout.Space(10 * scale);

            if (GUILayout.Button("Clear", UIStyles.ButtonStyle, GUILayout.Width(70 * scale), GUILayout.Height(26 * scale)))
            {
                _searchQuery = "";
                _selectedTags.Clear();
                _needsRefresh = true;
            }

            GUILayout.Space(10 * scale);
            GUILayout.Label("(auto-search)", UIStyles.LabelMutedStyle);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(15 * scale);

            // ═══════════════════════════════════════════════════════════════
            // ALL TAGS (wrapping flow layout)
            // ═══════════════════════════════════════════════════════════════
            GUILayout.Label("Available Tags:", UIStyles.LabelStyle);
            GUILayout.Space(5 * scale);

            var allTags = ModRegistry.Instance.GetAllTags();
            var userCustomTags = ModRegistry.Instance.CustomTags;

            if (allTags.Count > 0)
            {
                float availableWidth = 850f * scale;
                float currentRowWidth = 0f;
                float tagPadding = 8f * scale;

                GUILayout.BeginHorizontal();

                foreach (var kvp in allTags.OrderByDescending(x => x.Value))
                {
                    string tag = kvp.Key;
                    int count = kvp.Value;

                    bool isSelected = _selectedTags.Contains(tag);
                    bool isUserCustomTag = userCustomTags.Contains(tag);

                    // Style: selected = accent, user custom = accent, system/native = standard
                    GUIStyle tagStyle;
                    if (isSelected)
                        tagStyle = UIStyles.TagAccentStyle;
                    else if (isUserCustomTag)
                        tagStyle = UIStyles.TagAccentStyle;
                    else
                        tagStyle = UIStyles.TagStyle;

                    // Calculate tag width
                    GUIContent tagContent = new GUIContent($"{tag} ({count})");
                    float tagWidth = tagStyle.CalcSize(tagContent).x + tagPadding;

                    // Wrap to new line if needed
                    if (currentRowWidth + tagWidth > availableWidth && currentRowWidth > 0)
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        currentRowWidth = 0f;
                    }

                    if (GUILayout.Button(tagContent, tagStyle))
                    {
                        if (isSelected)
                            _selectedTags.Remove(tag);
                        else
                            _selectedTags.Add(tag);

                        _needsRefresh = true;
                    }

                    currentRowWidth += tagWidth;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No tags found", UIStyles.LabelMutedStyle);
            }

            GUILayout.Space(15 * scale);

            // ═══════════════════════════════════════════════════════════════
            // SELECTED TAGS
            // ═══════════════════════════════════════════════════════════════
            if (_selectedTags.Count > 0)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label("Selected:", UIStyles.LabelStyle, GUILayout.Width(80 * scale));

                foreach (var tag in _selectedTags.ToList())
                {
                    if (GUILayout.Button($"{tag} [x]", UIStyles.TagAccentStyle))
                    {
                        _selectedTags.Remove(tag);
                        _needsRefresh = true;
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear All", UIStyles.ButtonStyle, GUILayout.Width(90 * scale), GUILayout.Height(26 * scale)))
                {
                    _selectedTags.Clear();
                    _needsRefresh = true;
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(15 * scale);
            }

            // ═══════════════════════════════════════════════════════════════
            // DIVIDER
            // ═══════════════════════════════════════════════════════════════
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(10 * scale);

            // ═══════════════════════════════════════════════════════════════
            // RESULTS
            // ═══════════════════════════════════════════════════════════════
            if (_needsRefresh)
            {
                RefreshResults();
                _needsRefresh = false;
            }

            GUILayout.Label($"Results: {_filteredMods.Count} mod(s)", UIStyles.LabelStyle);
            GUILayout.Space(10 * scale);

            if (_filteredMods.Count == 0)
            {
                GUILayout.Label("No mods match your search criteria.", UIStyles.LabelMutedStyle);
            }
            else
            {
                DrawModGrid(scale);
            }
        }

        private void RefreshResults()
        {
            _filteredMods.Clear();

            var allMods = ModRegistry.Instance.Mods.Values;

            foreach (var mod in allMods)
            {
                // Get all tags for this mod (native + custom)
                var customTags = ModRegistry.Instance.GetModCustomTags(mod.ModId).ToList();
                var allModTags = mod.Tags.Concat(customTags).ToList();

                // Check search query
                if (!string.IsNullOrEmpty(_searchQuery))
                {
                    string query = _searchQuery.ToLower();
                    bool matchesQuery =
                        mod.ModName.ToLower().Contains(query) ||
                        mod.ModId.ToLower().Contains(query) ||
                        mod.Description.ToLower().Contains(query) ||
                        allModTags.Any(t => t.ToLower().Contains(query));

                    if (!matchesQuery)
                        continue;
                }

                // Check selected tags (mod must have ALL selected tags)
                if (_selectedTags.Count > 0)
                {
                    bool hasTags = _selectedTags.All(tag =>
                        allModTags.Contains(tag, System.StringComparer.OrdinalIgnoreCase));

                    if (!hasTags)
                        continue;
                }

                _filteredMods.Add(mod);
            }
        }

        private void DrawModGrid(float scale)
        {
            // Use same base values as ModListPage for consistency
            const float BASE_CARD_WIDTH = 160f;
            const float BASE_CARD_HEIGHT = 80f;
            const float BASE_CARD_SPACING = 10f;
            const float BASE_CONTENT_WIDTH = 850f;

            float cardWidth = BASE_CARD_WIDTH * scale;
            float cardHeight = BASE_CARD_HEIGHT * scale;
            float cardSpacing = BASE_CARD_SPACING * scale;
            float contentWidth = BASE_CONTENT_WIDTH * scale;

            int columns = Mathf.Max(1, Mathf.FloorToInt(contentWidth / (cardWidth + cardSpacing)));

            int col = 0;
            GUILayout.BeginHorizontal();

            foreach (var mod in _filteredMods)
            {
                DrawCompactModCard(mod, cardWidth, cardHeight, scale);

                col++;
                if (col >= columns)
                {
                    col = 0;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(cardSpacing);
                    GUILayout.BeginHorizontal();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawCompactModCard(ModInfo mod, float width, float height, float scale)
        {
            float iconSize = 50f * scale;

            GUILayout.BeginHorizontal(UIStyles.CardStyle, GUILayout.Width(width), GUILayout.Height(height));

            // Small icon
            Texture2D icon = mod.IconTexture ?? UIStyles.TexDefaultIcon;
            if (icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUILayout.Space(5 * scale);
            }

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(mod.ModName, UIStyles.LabelStyle);
            GUILayout.Label($"v{mod.ModVersion}", UIStyles.LabelMutedStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Click to open
            Rect cardRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
            {
                WindowManager.Instance.OpenModSettings(mod.ModId);
                Event.current.Use();
            }

            GUILayout.Space(10 * scale);
        }
    }
}
