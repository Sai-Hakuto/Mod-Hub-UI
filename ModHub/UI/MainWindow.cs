using UIFramework.Core;
using UIFramework.UI.Components;
using UIFramework.UI.Pages;
using UIFramework.UI.Styles;
using UnityEngine;

namespace UIFramework.UI
{
    /// <summary>
    /// Main UIFramework window.
    /// </summary>
    public class MainWindow
    {
        private Rect _windowRect;
        private bool _initialized;

        // Pages
        private ModListPage _modListPage;
        private ModSettingsPage _modSettingsPage;
        private SearchPage _searchPage;

        // Layout - base sizes (scale 1x)
        private const float BASE_WINDOW_WIDTH = 900f;
        private const float BASE_WINDOW_HEIGHT = 650f;
        private const float HEADER_HEIGHT = 40f;
        private const float TITLE_WIDTH = 400f;

        // Current scale and margin (for detecting changes)
        private int _currentScale = 1;
        private int _currentMargin = 30;
        private float _scaledWidth;
        private float _scaledHeight;

        private Vector2 _scrollPosition;

        public MainWindow()
        {
            UpdateWindowScale();

            _modListPage = new ModListPage();
            _modSettingsPage = new ModSettingsPage();
            _searchPage = new SearchPage();
        }

        /// <summary>
        /// Update window size based on UI Scale setting.
        /// </summary>
        private void UpdateWindowScale()
        {
            int newScale = Plugin.UIScaleConfig?.Value ?? 1;

            // Scale factors: 1x = 1.0, 2x = 1.25, 3x = 1.5
            float scaleFactor = newScale switch
            {
                2 => 1.25f,
                3 => 1.5f,
                _ => 1.0f
            };

            // Add right margin to window width so grid doesn't lose columns
            float rightMargin = Plugin.RightMarginConfig?.Value ?? 30;
            _scaledWidth = (BASE_WINDOW_WIDTH + rightMargin) * scaleFactor;
            _scaledHeight = BASE_WINDOW_HEIGHT * scaleFactor;

            // Center window on screen
            _windowRect = new Rect(
                (Screen.width - _scaledWidth) / 2,
                (Screen.height - _scaledHeight) / 2,
                _scaledWidth,
                _scaledHeight
            );

            _currentScale = newScale;
            _currentMargin = (int)rightMargin;
        }

        public void Draw()
        {
            if (!_initialized)
            {
                UIStyles.Initialize();
                _initialized = true;
            }

            // Check for accent color changes
            UIStyles.CheckAccentColorChange();

            // Check for UI scale or margin changes
            UIStyles.CheckScaleChange();
            int configScale = Plugin.UIScaleConfig?.Value ?? 1;
            int configMargin = Plugin.RightMarginConfig?.Value ?? 30;
            if (configScale != _currentScale || configMargin != _currentMargin)
            {
                UpdateWindowScale();
            }

            // Handle toggle key and ESC via Event.current (works even when mouse is over window)
            if (Event.current.type == EventType.KeyDown)
            {
                var toggleKey = Plugin.ToggleKey?.Value.MainKey ?? KeyCode.F10;
                if (Event.current.keyCode == toggleKey)
                {
                    WindowManager.Instance.Close();
                    Event.current.Use();
                    return;
                }

                // ESC on main page closes window (other pages handled inside DrawWindowContent)
                if (Event.current.keyCode == KeyCode.Escape && WindowManager.Instance.CurrentPage == EUIPage.ModList)
                {
                    // Only close if no popups are open
                    if (!FieldRenderers.IsDropdownOpen && !_modSettingsPage.IsTagPopupOpen && !_modSettingsPage.IsImagePopupOpen)
                    {
                        WindowManager.Instance.Close();
                        Event.current.Use();
                        return;
                    }
                }
            }

            // Draw window
            _windowRect = GUI.Window(
                "UIFramework".GetHashCode(),
                _windowRect,
                DrawWindowContent,
                "",
                UIStyles.WindowStyle
            );

            // Reset input to prevent game from receiving it
            if (_windowRect.Contains(Event.current.mousePosition))
            {
                Input.ResetInputAxes();
            }
        }

        private void DrawWindowContent(int windowId)
        {
            // ═══════════════════════════════════════════════════════════════
            // ESC KEY HANDLING
            // ═══════════════════════════════════════════════════════════════
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                // Priority: close popups first, then navigate back, then close window
                if (_modSettingsPage.IsImagePopupOpen)
                {
                    // Image popup handles its own ESC in DrawImagePopup
                }
                else if (FieldRenderers.IsDropdownOpen)
                {
                    FieldRenderers.CloseDropdown();
                    Event.current.Use();
                }
                else if (_modSettingsPage.IsTagPopupOpen)
                {
                    _modSettingsPage.CloseTagPopup();
                    Event.current.Use();
                }
                else if (WindowManager.Instance.CurrentPage != EUIPage.ModList)
                {
                    // Go back to mod list (restore snapshot if leaving settings)
                    if (WindowManager.Instance.CurrentPage == EUIPage.ModSettings)
                    {
                        var mod = ModRegistry.Instance.GetMod(WindowManager.Instance.CurrentModId);
                        mod?.RestoreSnapshot();
                    }
                    WindowManager.Instance.BackToModList();
                    Event.current.Use();
                }
                else
                {
                    // On main page - close window
                    WindowManager.Instance.Close();
                    Event.current.Use();
                }
            }

            // Check if any popup is open - disable all content below popups
            bool popupOpen = FieldRenderers.IsDropdownOpen || _modSettingsPage.IsTagPopupOpen || _modSettingsPage.IsImagePopupOpen;
            bool wasEnabled = GUI.enabled;
            if (popupOpen)
            {
                GUI.enabled = false;
            }

            // ═══════════════════════════════════════════════════════════════
            // HEADER
            // ═══════════════════════════════════════════════════════════════
            float scale = UIStyles.ScaleFactor;
            float headerBtnHeight = 30 * scale;
            float headerHeight = 45 * scale;

            GUILayout.BeginHorizontal(UIStyles.HeaderStyle, GUILayout.Height(headerHeight));

            // Back button (if not on main page)
            if (WindowManager.Instance.CurrentPage != EUIPage.ModList)
            {
                if (GUILayout.Button("< Back", UIStyles.ButtonStyle, GUILayout.Width(80 * scale), GUILayout.Height(headerBtnHeight)))
                {
                    // Restore snapshot (undo unsaved changes) when leaving mod settings
                    if (WindowManager.Instance.CurrentPage == EUIPage.ModSettings)
                    {
                        var mod = ModRegistry.Instance.GetMod(WindowManager.Instance.CurrentModId);
                        mod?.RestoreSnapshot();
                    }
                    WindowManager.Instance.BackToModList();
                }
                GUILayout.Space(10 * scale);
            }

            // Title
            string title = GetPageTitle();
            GUILayout.Label(title, UIStyles.TitleStyle, GUILayout.Width(TITLE_WIDTH * scale), GUILayout.Height(headerBtnHeight));

            GUILayout.FlexibleSpace();

            // Mod count
            int modCount = ModRegistry.Instance.ModCount;
            GUILayout.Label($"{modCount} mod(s)", UIStyles.LabelMutedStyle, GUILayout.Height(headerBtnHeight));

            GUILayout.Space(20 * scale);

            // Header buttons with uniform spacing and size
            float btnSpacing = 6 * scale;
            float btnWidth = 45 * scale;

            // Search button
            if (GUILayout.Button("Find", UIStyles.SmallButtonStyle, GUILayout.Width(btnWidth), GUILayout.Height(headerBtnHeight)))
            {
                WindowManager.Instance.NavigateTo(EUIPage.Search);
            }

            GUILayout.Space(btnSpacing);

            // Advanced toggle button
            GUIStyle advStyle = Plugin.ShowAdvanced.Value ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;
            if (GUILayout.Button("Adv", advStyle, GUILayout.Width(btnWidth), GUILayout.Height(headerBtnHeight)))
            {
                Plugin.ShowAdvanced.Value = !Plugin.ShowAdvanced.Value;
            }

            GUILayout.Space(btnSpacing);

            // Settings button (opens Mod Hub settings)
            if (GUILayout.Button("Cfg", UIStyles.SmallButtonStyle, GUILayout.Width(btnWidth), GUILayout.Height(headerBtnHeight)))
            {
                WindowManager.Instance.OpenModSettings(ModRegistry.ModHubModId);
            }

            GUILayout.Space(btnSpacing);

            // Close button
            if (GUILayout.Button("X", UIStyles.SmallButtonStyle, GUILayout.Width(btnWidth), GUILayout.Height(headerBtnHeight)))
            {
                WindowManager.Instance.Close();
            }

            GUILayout.EndHorizontal();

            // ═══════════════════════════════════════════════════════════════
            // CONTENT
            // ═══════════════════════════════════════════════════════════════
            GUILayout.Space(5);

            // Handle scroll position and snapshots when page changes
            if (WindowManager.Instance.PageChanged)
            {
                // If going back to ModList, restore saved position
                if (WindowManager.Instance.CurrentPage == EUIPage.ModList)
                {
                    _scrollPosition = WindowManager.Instance.LastModListScrollPosition;
                }
                else
                {
                    _scrollPosition = Vector2.zero;
                }

                // Capture snapshot when entering mod settings
                if (WindowManager.Instance.CurrentPage == EUIPage.ModSettings)
                {
                    var mod = ModRegistry.Instance.GetMod(WindowManager.Instance.CurrentModId);
                    mod?.CaptureSnapshot();
                }

                WindowManager.Instance.ClearPageChanged();
            }

            // Save scroll position when on ModList
            if (WindowManager.Instance.CurrentPage == EUIPage.ModList)
            {
                WindowManager.Instance.LastModListScrollPosition = _scrollPosition;
            }

            // Capture scroll wheel for dropdown or tag popup when open
            if (popupOpen && Event.current.type == EventType.ScrollWheel)
            {
                if (FieldRenderers.IsDropdownOpen)
                {
                    FieldRenderers.ApplyScrollDelta(Event.current.delta.y * 20f);
                }
                else if (_modSettingsPage.IsTagPopupOpen)
                {
                    _modSettingsPage.ApplyTagScrollDelta(Event.current.delta.y * 20f);
                }
                Event.current.Use();
            }

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                UIStyles.ScrollViewStyle
            );

            // Add horizontal padding for content
            // Right margin setting only applies to grid pages (ModList, Search), not settings page
            float padScale = UIStyles.ScaleFactor;
            float leftPadding = 15 * padScale;
            float baseRightPadding = 20 * padScale; // Base scrollbar space
            bool isGridPage = WindowManager.Instance.CurrentPage == EUIPage.ModList ||
                              WindowManager.Instance.CurrentPage == EUIPage.Search;
            float configMargin = isGridPage ? (Plugin.RightMarginConfig?.Value ?? 30) * padScale : 0;
            float rightPadding = baseRightPadding + configMargin;
            GUILayout.BeginHorizontal();
            GUILayout.Space(leftPadding);

            GUILayout.BeginVertical();

            switch (WindowManager.Instance.CurrentPage)
            {
                case EUIPage.ModList:
                    _modListPage.Draw();
                    break;

                case EUIPage.ModSettings:
                    string modId = WindowManager.Instance.CurrentModId;
                    var mod = ModRegistry.Instance.GetMod(modId);
                    if (mod != null)
                    {
                        _modSettingsPage.Draw(mod);
                    }
                    else
                    {
                        GUILayout.Label($"Mod '{modId}' not found", UIStyles.LabelStyle);
                    }
                    break;

                case EUIPage.Search:
                    _searchPage.Draw();
                    break;

                default:
                    GUILayout.Label("Page not implemented", UIStyles.LabelStyle);
                    break;
            }

            GUILayout.EndVertical();

            GUILayout.Space(rightPadding);
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();

            // Restore GUI.enabled before drawing popups
            GUI.enabled = wasEnabled;

            // Draw tooltips, dropdowns and popups outside scroll view (so they don't get clipped)
            if (WindowManager.Instance.CurrentPage == EUIPage.ModSettings)
            {
                _modSettingsPage.DrawDropdowns(_windowRect);
                _modSettingsPage.DrawTagPopup(_windowRect);
                _modSettingsPage.DrawTooltip();
                _modSettingsPage.DrawImagePopup(_windowRect);
            }
            else if (WindowManager.Instance.CurrentPage == EUIPage.ModList)
            {
                _modListPage.DrawTooltip();
            }

            // ═══════════════════════════════════════════════════════════════
            // FOOTER
            // ═══════════════════════════════════════════════════════════════
            GUILayout.FlexibleSpace();

            // Action buttons for mod settings page
            if (WindowManager.Instance.CurrentPage == EUIPage.ModSettings)
            {
                string modId = WindowManager.Instance.CurrentModId;
                var mod = ModRegistry.Instance.GetMod(modId);

                if (mod != null)
                {
                    float footerBtnWidth = 100 * scale;
                    float footerBtnHeight = 28 * scale;
                    float footerHeight = 40 * scale;

                    GUILayout.BeginHorizontal(UIStyles.BoxStyle, GUILayout.Height(footerHeight));

                    GUILayout.Label($"Mod Hub v{AssemblyInfo.ModVersion}", UIStyles.LabelMutedStyle);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Reset All", UIStyles.ButtonStyle, GUILayout.Width(footerBtnWidth), GUILayout.Height(footerBtnHeight)))
                    {
                        mod.ResetAllFields();
                        mod.ModInterface?.OnSettingsReset();
                    }

                    GUILayout.Space(10 * scale);

                    if (GUILayout.Button("Save", UIStyles.ButtonAccentStyle, GUILayout.Width(footerBtnWidth), GUILayout.Height(footerBtnHeight)))
                    {
                        mod.CommitChanges();
                        mod.ModInterface?.OnSettingsSaved();
                    }

                    GUILayout.Space(10 * scale);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Mod Hub v{AssemblyInfo.ModVersion} by Hakusai", UIStyles.LabelMutedStyle);
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }

            // Make window draggable by header area (must be at the end to not interfere with buttons)
            // Draggable area: from title to before the right-side buttons
            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 250 * scale, HEADER_HEIGHT * scale));
        }

        private string GetPageTitle()
        {
            switch (WindowManager.Instance.CurrentPage)
            {
                case EUIPage.ModList:
                    return "MOD HUB";
                case EUIPage.ModSettings:
                    var mod = ModRegistry.Instance.GetMod(WindowManager.Instance.CurrentModId);
                    return mod?.ModName ?? "Settings";
                case EUIPage.Search:
                    return "Search & Filter";
                default:
                    return "Mod Hub";
            }
        }

        public void Cleanup()
        {
            UIStyles.Cleanup();
        }
    }
}
