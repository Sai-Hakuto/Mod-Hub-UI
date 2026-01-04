using UIFramework.Core;
using UIFramework.UI.Styles;
using UnityEngine;

namespace UIFramework.UI.Pages
{
    /// <summary>
    /// Main page showing grid of registered mods (like Steam library).
    /// </summary>
    public class ModListPage
    {
        // Base sizes (at scale 1x)
        private const float BASE_CARD_WIDTH = 160f;
        private const float BASE_CARD_HEIGHT = 200f;
        private const float BASE_ICON_SIZE = 128f;
        private const float BASE_CARD_SPACING = 10f;
        private const float BASE_CONTENT_WIDTH = 850f;

        // Scaled sizes (computed from ScaleFactor)
        private float CardWidth => BASE_CARD_WIDTH * UIStyles.ScaleFactor;
        private float CardHeight => BASE_CARD_HEIGHT * UIStyles.ScaleFactor;
        private float IconSize => BASE_ICON_SIZE * UIStyles.ScaleFactor;
        private float CardSpacing => BASE_CARD_SPACING * UIStyles.ScaleFactor;
        private float ContentWidth => BASE_CONTENT_WIDTH * UIStyles.ScaleFactor;

        private string _hoverModId;
        private Rect _hoveredFavRect;
        private bool _showFavTooltip;
        private string _favTooltipText;
        private bool _showHiddenMods;

        // Card description tooltip
        private float _hoverStartTime;
        private string _lastHoverModId;
        private bool _showCardTooltip;
        private string _cardTooltipText;
        private Rect _cardTooltipRect;
        private const float TOOLTIP_DELAY = 1.0f;

        public void Draw()
        {
            // Reset tooltip state
            _showFavTooltip = false;
            _showCardTooltip = false;

            if (ModRegistry.Instance.ModCount == 0)
            {
                DrawEmptyState();
                return;
            }

            // Get mods sorted with favorites first
            var sortedMods = ModRegistry.Instance.GetModsSortedByFavorites();

            // Calculate grid layout based on scaled dimensions
            int columns = Mathf.Max(1, Mathf.FloorToInt(ContentWidth / (CardWidth + CardSpacing)));

            int col = 0;
            GUILayout.BeginHorizontal();

            foreach (var mod in sortedMods)
            {
                DrawModCard(mod);

                col++;
                if (col >= columns)
                {
                    col = 0;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(CardSpacing);
                    GUILayout.BeginHorizontal();
                }
            }

            // Fill remaining space
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Hidden mods section
            var hiddenMods = ModRegistry.Instance.GetHiddenMods();
            int hiddenCount = 0;
            foreach (var _ in hiddenMods) hiddenCount++;

            if (hiddenCount > 0)
            {
                GUILayout.Space(20 * UIStyles.ScaleFactor);

                // Collapsible header
                string headerText = _showHiddenMods ? "v Hidden (" + hiddenCount + ")" : "> Hidden (" + hiddenCount + ")";
                if (GUILayout.Button(headerText, UIStyles.SectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(28 * UIStyles.ScaleFactor)))
                {
                    _showHiddenMods = !_showHiddenMods;
                }

                if (_showHiddenMods)
                {
                    GUILayout.Space(CardSpacing);

                    col = 0;
                    GUILayout.BeginHorizontal();

                    foreach (var mod in ModRegistry.Instance.GetHiddenMods())
                    {
                        DrawModCard(mod);

                        col++;
                        if (col >= columns)
                        {
                            col = 0;
                            GUILayout.EndHorizontal();
                            GUILayout.Space(CardSpacing);
                            GUILayout.BeginHorizontal();
                        }
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawModCard(ModInfo mod)
        {
            float scale = UIStyles.ScaleFactor;
            bool isHovered = _hoverModId == mod.ModId;
            bool isFavorite = ModRegistry.Instance.IsFavorite(mod.ModId);
            GUIStyle cardStyle = isHovered ? UIStyles.CardHoverStyle : UIStyles.CardStyle;

            GUILayout.BeginVertical(cardStyle, GUILayout.Width(CardWidth), GUILayout.Height(CardHeight));

            // Icon with favorite button overlay
            Rect iconRect = GUILayoutUtility.GetRect(IconSize, IconSize);
            Texture2D icon = mod.IconTexture ?? UIStyles.TexDefaultIcon;

            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(iconRect, "", UIStyles.BoxStyle);
            }

            // Favorite button in top-right corner of icon
            float btnSize = 20 * scale;
            float btnOffset = 24 * scale;
            Rect favRect = new Rect(iconRect.xMax - btnOffset, iconRect.y + 4 * scale, btnSize, btnSize);
            string favText = isFavorite ? "<3" : "..";
            GUIStyle favStyle = isFavorite ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;

            if (GUI.Button(favRect, favText, favStyle))
            {
                ModRegistry.Instance.ToggleFavorite(mod.ModId);
                Event.current.Use();
            }

            // Track hover for tooltip
            if (favRect.Contains(Event.current.mousePosition))
            {
                _showFavTooltip = true;
                _hoveredFavRect = favRect;
                _favTooltipText = isFavorite ? "Remove from favorites" : "Add to favorites";
            }

            // Hide button below favorite button
            bool isHidden = ModRegistry.Instance.IsHidden(mod.ModId);
            Rect hideRect = new Rect(iconRect.xMax - btnOffset, iconRect.y + 28 * scale, btnSize, btnSize);
            string hideText = isHidden ? "+" : "x";
            GUIStyle hideStyle = isHidden ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;

            if (GUI.Button(hideRect, hideText, hideStyle))
            {
                ModRegistry.Instance.ToggleHidden(mod.ModId);
                Event.current.Use();
            }

            // Track hover for hide tooltip
            if (hideRect.Contains(Event.current.mousePosition))
            {
                _showFavTooltip = true;
                _hoveredFavRect = hideRect;
                _favTooltipText = isHidden ? "Show in main list" : "Hide from main list";
            }

            GUILayout.Space(5 * scale);

            // Mod name
            GUILayout.Label(mod.ModName, UIStyles.LabelStyle);

            // Version & Author
            GUILayout.Label($"v{mod.ModVersion} • {mod.ModAuthor}", UIStyles.LabelMutedStyle);

            // Tags - smart fitting within icon width
            float tagHeight = 16 * scale;
            float tagRowWidth = IconSize; // Match icon width
            float tagSpacing = 4 * scale;
            float plusLabelWidth = 25 * scale; // Reserve space for "+X"

            // Collect all valid tags (native + custom), respecting max length
            const int MAX_TAG_LENGTH = 12;
            var allTags = new System.Collections.Generic.List<(string tag, bool isCustom)>();

            if (mod.Tags != null)
            {
                foreach (var tag in mod.Tags)
                {
                    if (!string.IsNullOrEmpty(tag) && tag.Length <= MAX_TAG_LENGTH)
                        allTags.Add((tag, false));
                }
            }
            foreach (var tag in ModRegistry.Instance.GetModCustomTags(mod.ModId))
            {
                if (!string.IsNullOrEmpty(tag) && tag.Length <= MAX_TAG_LENGTH)
                    allTags.Add((tag, true));
            }

            GUILayout.BeginHorizontal(GUILayout.Width(tagRowWidth), GUILayout.Height(tagHeight));

            if (allTags.Count > 0)
            {
                // Calculate which tags fit
                float availableWidth = tagRowWidth;
                int tagsToShow = 0;
                float usedWidth = 0f;

                foreach (var (tag, _) in allTags)
                {
                    GUIContent content = new GUIContent(tag);
                    float tagWidth = UIStyles.TagStyle.CalcSize(content).x + tagSpacing;

                    // Check if this tag fits (reserve space for +X if there are more)
                    float neededSpace = tagWidth;
                    if (tagsToShow < allTags.Count - 1) // Not the last tag
                        neededSpace += plusLabelWidth;

                    if (usedWidth + neededSpace <= availableWidth)
                    {
                        usedWidth += tagWidth;
                        tagsToShow++;
                    }
                    else
                    {
                        break;
                    }
                }

                // Ensure at least we try to show something if possible
                if (tagsToShow == 0 && allTags.Count > 0)
                    tagsToShow = 1; // Force show at least one, it will be clipped

                // Draw fitting tags
                for (int i = 0; i < tagsToShow && i < allTags.Count; i++)
                {
                    var (tag, isCustom) = allTags[i];
                    GUIStyle style = isCustom ? UIStyles.TagAccentStyle : UIStyles.TagStyle;
                    GUILayout.Label(tag, style, GUILayout.Height(tagHeight));
                }

                // Show "+N" if there are hidden tags
                int hiddenCount = allTags.Count - tagsToShow;
                if (hiddenCount > 0)
                {
                    GUILayout.Label($"+{hiddenCount}", UIStyles.LabelMutedStyle, GUILayout.Height(tagHeight));
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // Check hover and click
            Rect cardRect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.Repaint)
            {
                bool isCardHovered = cardRect.Contains(Event.current.mousePosition);
                _hoverModId = isCardHovered ? mod.ModId : _hoverModId;

                // Track hover time for card tooltip
                if (isCardHovered && !favRect.Contains(Event.current.mousePosition) && !hideRect.Contains(Event.current.mousePosition))
                {
                    if (_lastHoverModId != mod.ModId)
                    {
                        _lastHoverModId = mod.ModId;
                        _hoverStartTime = Time.realtimeSinceStartup;
                    }

                    // Show tooltip after delay
                    if (Time.realtimeSinceStartup - _hoverStartTime >= TOOLTIP_DELAY)
                    {
                        // Build tooltip text
                        string desc = mod.Description;
                        string author = mod.ModAuthor;
                        bool hasDesc = !string.IsNullOrEmpty(desc) && desc != "BepInEx plugin";
                        bool hasAuthor = !string.IsNullOrEmpty(author) && author != "Unknown";

                        if (hasDesc || hasAuthor)
                        {
                            _showCardTooltip = true;
                            _cardTooltipRect = cardRect;

                            if (hasDesc && hasAuthor)
                                _cardTooltipText = $"{desc}\n\nby {author}";
                            else if (hasDesc)
                                _cardTooltipText = desc;
                            else
                                _cardTooltipText = $"by {author}";
                        }
                    }
                }
            }

            // Click on card (but not on favorite/hide buttons)
            if (Event.current.type == EventType.MouseDown &&
                cardRect.Contains(Event.current.mousePosition) &&
                !favRect.Contains(Event.current.mousePosition) &&
                !hideRect.Contains(Event.current.mousePosition))
            {
                WindowManager.Instance.OpenModSettings(mod.ModId);
                Event.current.Use();
            }

            GUILayout.Space(CardSpacing);
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical(UIStyles.BoxStyle, GUILayout.Width(400));

            GUILayout.Space(20);

            var centeredLabel = new GUIStyle(UIStyles.LabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };

            GUILayout.Label("No mods registered", centeredLabel);
            GUILayout.Space(10);

            var mutedCentered = new GUIStyle(UIStyles.LabelMutedStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            GUILayout.Label(
                "Install Mod Hub-compatible mods or wait for mods to register.\n\n" +
                "Developers: Use UIFApi.Register() to add your mod.",
                mutedCentered
            );

            GUILayout.Space(20);

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        public void DrawTooltip()
        {
            // Button tooltips (fav/hide) - use mouse position
            if (_showFavTooltip && !string.IsNullOrEmpty(_favTooltipText))
            {
                Vector2 mousePos = Event.current.mousePosition;
                GUIContent content = new GUIContent(_favTooltipText);
                Vector2 size = UIStyles.TooltipStyle.CalcSize(content);

                // Position tooltip below the mouse
                float tooltipX = mousePos.x - size.x / 2;
                float tooltipY = mousePos.y + 20;

                // Keep on screen
                if (tooltipX < 10) tooltipX = 10;
                if (tooltipX + size.x + 10 > 880) tooltipX = 880 - size.x - 10;
                if (tooltipY + size.y + 6 > 620) tooltipY = mousePos.y - size.y - 10;

                Rect tooltipRect = new Rect(tooltipX, tooltipY, size.x + 10, size.y + 6);
                GUI.Label(tooltipRect, _favTooltipText, UIStyles.TooltipStyle);
            }

            // Card description tooltip (after hover delay) - use mouse position
            if (_showCardTooltip && !string.IsNullOrEmpty(_cardTooltipText))
            {
                Vector2 mousePos = Event.current.mousePosition;
                float maxWidth = 280f * UIStyles.ScaleFactor;
                GUIContent content = new GUIContent(_cardTooltipText);

                // Calculate height with word wrap
                float height = UIStyles.TooltipStyle.CalcHeight(content, maxWidth);
                float width = Mathf.Min(UIStyles.TooltipStyle.CalcSize(content).x, maxWidth);

                // Position tooltip near mouse
                float tooltipX = mousePos.x + 15;
                float tooltipY = mousePos.y + 15;

                // Keep on screen
                if (tooltipX + width + 20 > 880)
                    tooltipX = mousePos.x - width - 20;
                if (tooltipY + height + 12 > 620)
                    tooltipY = mousePos.y - height - 15;

                Rect tooltipRect = new Rect(tooltipX, tooltipY, width + 20, height + 12);
                GUI.Box(tooltipRect, "", UIStyles.TooltipStyle);
                GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 6, width, height), _cardTooltipText, UIStyles.TooltipStyle);
            }
        }
    }
}
