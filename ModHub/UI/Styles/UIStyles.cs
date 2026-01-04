using UnityEngine;

namespace UIFramework.UI.Styles
{
    /// <summary>
    /// Central styling for UIFramework.
    /// Dark theme inspired by Tarkov/Steam.
    /// </summary>
    public static class UIStyles
    {
        private static bool _initialized;

        // UI Scale
        public static float ScaleFactor { get; private set; } = 1f;
        private static int _lastUIScale = 1;

        // Colors
        public static readonly Color BackgroundDark = new Color(0.12f, 0.12f, 0.14f, 0.98f);
        public static readonly Color BackgroundMid = new Color(0.18f, 0.18f, 0.20f, 1f);
        public static readonly Color BackgroundLight = new Color(0.25f, 0.25f, 0.28f, 1f);
        public static readonly Color BorderColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        public static Color AccentColor { get; private set; } = new Color(0.91f, 0.30f, 0.24f, 1f); // Red (default)
        public static readonly Color TextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color TextMuted = new Color(0.6f, 0.6f, 0.6f, 1f);
        public static readonly Color TagColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        public static readonly Color HoverColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        public static readonly Color SelectedColor = new Color(0.4f, 0.35f, 0.2f, 1f);

        // Accent color presets
        private static readonly System.Collections.Generic.Dictionary<string, Color> AccentColorPresets =
            new System.Collections.Generic.Dictionary<string, Color>
            {
                { "Gold", new Color(0.8f, 0.6f, 0.2f, 1f) },
                { "Blue", new Color(0.23f, 0.51f, 0.96f, 1f) },
                { "Green", new Color(0.18f, 0.80f, 0.44f, 1f) },
                { "Red", new Color(0.91f, 0.30f, 0.24f, 1f) },
                { "Purple", new Color(0.55f, 0.27f, 0.68f, 1f) },
                { "Teal", new Color(0.10f, 0.74f, 0.61f, 1f) },
                { "Orange", new Color(0.95f, 0.61f, 0.07f, 1f) }
            };

        private static string _lastAccentColorName = "";

        // Textures
        public static Texture2D TexBackgroundDark { get; private set; }
        public static Texture2D TexBackgroundMid { get; private set; }
        public static Texture2D TexBackgroundLight { get; private set; }
        public static Texture2D TexBorder { get; private set; }
        public static Texture2D TexAccent { get; private set; }
        public static Texture2D TexHover { get; private set; }
        public static Texture2D TexSelected { get; private set; }
        public static Texture2D TexSliderBg { get; private set; }
        public static Texture2D TexSliderFill { get; private set; }
        public static Texture2D TexDefaultIcon { get; private set; }

        // Styles
        public static GUIStyle WindowStyle { get; private set; }
        public static GUIStyle HeaderStyle { get; private set; }
        public static GUIStyle TitleStyle { get; private set; }
        public static GUIStyle LabelStyle { get; private set; }
        public static GUIStyle LabelMutedStyle { get; private set; }
        public static GUIStyle ButtonStyle { get; private set; }
        public static GUIStyle ButtonAccentStyle { get; private set; }
        public static GUIStyle CardStyle { get; private set; }
        public static GUIStyle CardHoverStyle { get; private set; }
        public static GUIStyle SectionStyle { get; private set; }
        public static GUIStyle ToggleStyle { get; private set; }
        public static GUIStyle SliderStyle { get; private set; }
        public static GUIStyle SliderThumbStyle { get; private set; }
        public static GUIStyle TextFieldStyle { get; private set; }
        public static GUIStyle TagStyle { get; private set; }
        public static GUIStyle TagAccentStyle { get; private set; }
        public static GUIStyle BoxStyle { get; private set; }
        public static GUIStyle PopupStyle { get; private set; }
        public static GUIStyle ScrollViewStyle { get; private set; }
        public static GUIStyle TooltipStyle { get; private set; }
        public static GUIStyle SmallButtonStyle { get; private set; }
        public static GUIStyle SmallButtonAccentStyle { get; private set; }
        public static GUIStyle DropdownButtonStyle { get; private set; }
        public static GUIStyle ErrorStyle { get; private set; }

        public static void Initialize()
        {
            if (_initialized) return;

            // Load accent color and scale from config
            UpdateAccentColor();
            UpdateScale();

            CreateTextures();
            CreateStyles();

            _initialized = true;
        }

        /// <summary>
        /// Update scale factor from config.
        /// </summary>
        private static void UpdateScale()
        {
            int uiScale = Plugin.UIScaleConfig?.Value ?? 1;
            _lastUIScale = uiScale;

            // Scale factors: 1x = 1.0, 2x = 1.25, 3x = 1.5
            ScaleFactor = uiScale switch
            {
                2 => 1.25f,
                3 => 1.5f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Check and apply scale changes. Call from OnGUI.
        /// </summary>
        public static void CheckScaleChange()
        {
            int currentScale = Plugin.UIScaleConfig?.Value ?? 1;
            if (currentScale != _lastUIScale)
            {
                UpdateScale();
                // Recreate all styles with new scale
                CreateStyles();
            }
        }

        /// <summary>
        /// Update accent color from config. Call this to refresh UI after color change.
        /// </summary>
        public static void UpdateAccentColor()
        {
            string colorName = Plugin.AccentColorConfig?.Value ?? "Red";

            // Only recreate if color actually changed
            if (colorName == _lastAccentColorName)
                return;

            _lastAccentColorName = colorName;

            if (AccentColorPresets.TryGetValue(colorName, out Color newColor))
            {
                AccentColor = newColor;
            }
            else
            {
                AccentColor = AccentColorPresets["Red"];
            }

            // If already initialized, recreate accent-dependent textures and styles
            if (_initialized)
            {
                RefreshAccentStyles();
            }
        }

        /// <summary>
        /// Refresh styles that depend on accent color.
        /// </summary>
        private static void RefreshAccentStyles()
        {
            // Recreate accent textures
            if (TexAccent != null) Object.Destroy(TexAccent);
            if (TexSliderFill != null) Object.Destroy(TexSliderFill);

            TexAccent = MakeTex(2, 2, AccentColor);
            TexSliderFill = MakeTex(2, 2, AccentColor);

            // Update styles that use accent
            if (ButtonAccentStyle != null)
            {
                ButtonAccentStyle.normal.background = TexAccent;
                ButtonAccentStyle.hover.background = MakeTex(2, 2, AccentColor * 1.2f);
            }

            if (SmallButtonAccentStyle != null)
            {
                SmallButtonAccentStyle.normal.background = TexAccent;
                SmallButtonAccentStyle.hover.background = MakeTex(2, 2, AccentColor * 1.2f);
            }

            if (TagAccentStyle != null)
            {
                TagAccentStyle.normal.background = MakeTex(2, 2, AccentColor);
            }

            if (SectionStyle != null)
            {
                SectionStyle.normal.textColor = AccentColor;
            }

            if (ToggleStyle != null)
            {
                ToggleStyle.onNormal.textColor = AccentColor;
            }

            if (SliderThumbStyle != null)
            {
                int thumbSize = S(16);
                int thumbRadius = thumbSize / 4;
                SliderThumbStyle.normal.background = MakeRoundedTex(thumbSize, thumbSize, AccentColor, thumbRadius);
                SliderThumbStyle.hover.background = MakeRoundedTex(thumbSize, thumbSize, AccentColor * 1.2f, thumbRadius);
                SliderThumbStyle.active.background = MakeRoundedTex(thumbSize, thumbSize, AccentColor * 0.8f, thumbRadius);
                SliderThumbStyle.focused.background = MakeRoundedTex(thumbSize, thumbSize, AccentColor, thumbRadius);
            }
        }

        /// <summary>
        /// Check and apply accent color changes. Call from OnGUI.
        /// </summary>
        public static void CheckAccentColorChange()
        {
            string currentColor = Plugin.AccentColorConfig?.Value ?? "Red";
            if (currentColor != _lastAccentColorName)
            {
                UpdateAccentColor();
            }
        }

        private static void CreateTextures()
        {
            TexBackgroundDark = MakeTex(2, 2, BackgroundDark);
            TexBackgroundMid = MakeTex(2, 2, BackgroundMid);
            TexBackgroundLight = MakeTex(2, 2, BackgroundLight);
            TexBorder = MakeTex(2, 2, BorderColor);
            TexAccent = MakeTex(2, 2, AccentColor);
            TexHover = MakeTex(2, 2, HoverColor);
            TexSelected = MakeTex(2, 2, SelectedColor);
            TexSliderBg = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.18f, 1f));
            TexSliderFill = MakeTex(2, 2, AccentColor);

            // Default mod icon (simple colored square)
            TexDefaultIcon = MakeTex(128, 128, new Color(0.3f, 0.3f, 0.35f, 1f));
        }

        // Helper to scale integer values
        private static int S(int value) => Mathf.RoundToInt(value * ScaleFactor);

        // Helper to create scaled RectOffset
        private static RectOffset ScaledOffset(int left, int right, int top, int bottom) =>
            new RectOffset(S(left), S(right), S(top), S(bottom));

        private static void CreateStyles()
        {
            // Window
            WindowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = TexBackgroundDark, textColor = TextColor },
                onNormal = { background = TexBackgroundDark, textColor = TextColor },
                border = new RectOffset(8, 8, 8, 8),
                padding = ScaledOffset(10, 10, 10, 10)
            };

            // Header (top bar)
            HeaderStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = TexBackgroundMid, textColor = TextColor },
                padding = ScaledOffset(10, 10, 8, 8),
                margin = ScaledOffset(0, 0, 0, 5),
                fontSize = S(16),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            // Title
            TitleStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = TextColor },
                fontSize = S(18),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                stretchWidth = false
            };

            // Label
            LabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = TextColor },
                fontSize = S(13),
                alignment = TextAnchor.MiddleLeft,
                padding = ScaledOffset(5, 5, 2, 2),
                stretchWidth = false
            };

            // Label muted
            LabelMutedStyle = new GUIStyle(LabelStyle)
            {
                normal = { textColor = TextMuted },
                fontSize = S(11)
            };

            // Button
            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = TexBackgroundLight, textColor = TextColor },
                hover = { background = TexHover, textColor = TextColor },
                active = { background = TexAccent, textColor = Color.white },
                border = new RectOffset(4, 4, 4, 4),
                padding = ScaledOffset(12, 12, 6, 6),
                fontSize = S(13)
            };

            // Button accent
            ButtonAccentStyle = new GUIStyle(ButtonStyle)
            {
                normal = { background = TexAccent, textColor = Color.white },
                hover = { background = MakeTex(2, 2, AccentColor * 1.2f), textColor = Color.white },
                active = { background = MakeTex(2, 2, AccentColor * 0.6f), textColor = Color.white }
            };

            // Card (for mod list)
            CardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = TexBackgroundMid, textColor = TextColor },
                hover = { background = TexHover, textColor = TextColor },
                border = new RectOffset(4, 4, 4, 4),
                padding = ScaledOffset(8, 8, 8, 8),
                margin = ScaledOffset(5, 5, 5, 5)
            };

            CardHoverStyle = new GUIStyle(CardStyle)
            {
                normal = { background = TexHover }
            };

            // Section header
            SectionStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = TexBackgroundLight, textColor = AccentColor },
                fontSize = S(14),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = ScaledOffset(10, 10, 6, 6),
                margin = ScaledOffset(0, 0, 10, 5),
                stretchWidth = false
            };

            // Toggle
            ToggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                normal = { textColor = TextColor },
                onNormal = { textColor = AccentColor },
                fontSize = S(13),
                padding = ScaledOffset(20, 5, 3, 3)
            };

            // Slider
            SliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
            {
                normal = { background = TexSliderBg },
                fixedHeight = S(8)
            };

            // Slider thumb - rounded accent style for all states
            int thumbSize = S(16);
            int thumbRadius = thumbSize / 4; // Rounded corners
            var thumbNormalTex = MakeRoundedTex(thumbSize, thumbSize, AccentColor, thumbRadius);
            var thumbHoverTex = MakeRoundedTex(thumbSize, thumbSize, AccentColor * 1.2f, thumbRadius);
            var thumbActiveTex = MakeRoundedTex(thumbSize, thumbSize, AccentColor * 0.8f, thumbRadius);
            SliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                normal = { background = thumbNormalTex },
                hover = { background = thumbHoverTex },
                active = { background = thumbActiveTex },
                focused = { background = thumbNormalTex },
                fixedWidth = thumbSize,
                fixedHeight = thumbSize
            };

            // TextField
            TextFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                normal = { background = TexBackgroundLight, textColor = TextColor },
                focused = { background = TexHover, textColor = TextColor },
                border = new RectOffset(4, 4, 4, 4),
                padding = ScaledOffset(8, 8, 4, 4),
                fontSize = S(13)
            };

            // Tag
            TagStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, TagColor), textColor = Color.white },
                fontSize = S(11),
                alignment = TextAnchor.MiddleCenter,
                padding = ScaledOffset(8, 8, 3, 3),
                margin = ScaledOffset(2, 2, 2, 2)
            };

            // Tag accent (for user-defined tags)
            TagAccentStyle = new GUIStyle(TagStyle)
            {
                normal = { background = MakeTex(2, 2, AccentColor), textColor = Color.white }
            };

            // Popup (modal dialog background)
            PopupStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.18f, 0.98f)), textColor = TextColor },
                border = new RectOffset(6, 6, 6, 6),
                padding = ScaledOffset(10, 10, 10, 10)
            };

            // Box
            BoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = TexBackgroundMid, textColor = TextColor },
                border = new RectOffset(4, 4, 4, 4),
                padding = ScaledOffset(10, 10, 10, 10)
            };

            // ScrollView
            ScrollViewStyle = new GUIStyle(GUI.skin.scrollView)
            {
                normal = { background = TexBackgroundDark }
            };

            // Tooltip
            TooltipStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.12f, 0.95f)), textColor = TextColor },
                fontSize = S(12),
                alignment = TextAnchor.UpperLeft,
                padding = ScaledOffset(10, 10, 8, 8),
                wordWrap = true
            };

            // Small button (minimal padding for compact UI)
            SmallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = TexBackgroundLight, textColor = TextColor },
                hover = { background = TexHover, textColor = TextColor },
                active = { background = TexAccent, textColor = Color.white },
                border = new RectOffset(2, 2, 2, 2),
                padding = ScaledOffset(4, 4, 2, 2),
                margin = ScaledOffset(2, 2, 2, 2),
                fontSize = S(11),
                alignment = TextAnchor.MiddleCenter
            };

            // Small button accent
            SmallButtonAccentStyle = new GUIStyle(SmallButtonStyle)
            {
                normal = { background = TexAccent, textColor = Color.white },
                hover = { background = MakeTex(2, 2, AccentColor * 1.2f), textColor = Color.white },
                active = { background = MakeTex(2, 2, AccentColor * 0.6f), textColor = Color.white }
            };

            // Dropdown button (left-aligned text for long values)
            DropdownButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = TexBackgroundLight, textColor = TextColor },
                hover = { background = TexHover, textColor = TextColor },
                active = { background = TexAccent, textColor = Color.white },
                border = new RectOffset(4, 4, 4, 4),
                padding = ScaledOffset(8, 8, 6, 6),
                fontSize = S(13),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };

            // Error style
            ErrorStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.4f, 1f) },
                fontSize = S(10),
                wordWrap = true,
                stretchWidth = false
            };
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Create a rounded rectangle texture.
        /// </summary>
        private static Texture2D MakeRoundedTex(int width, int height, Color col, int radius)
        {
            Texture2D result = new Texture2D(width, height);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Check corners
                    bool inCorner = false;

                    // Top-left corner
                    if (x < radius && y < radius)
                    {
                        int dx = radius - x - 1;
                        int dy = radius - y - 1;
                        inCorner = (dx * dx + dy * dy) > (radius * radius);
                    }
                    // Top-right corner
                    else if (x >= width - radius && y < radius)
                    {
                        int dx = x - (width - radius);
                        int dy = radius - y - 1;
                        inCorner = (dx * dx + dy * dy) > (radius * radius);
                    }
                    // Bottom-left corner
                    else if (x < radius && y >= height - radius)
                    {
                        int dx = radius - x - 1;
                        int dy = y - (height - radius);
                        inCorner = (dx * dx + dy * dy) > (radius * radius);
                    }
                    // Bottom-right corner
                    else if (x >= width - radius && y >= height - radius)
                    {
                        int dx = x - (width - radius);
                        int dy = y - (height - radius);
                        inCorner = (dx * dx + dy * dy) > (radius * radius);
                    }

                    result.SetPixel(x, y, inCorner ? transparent : col);
                }
            }

            result.Apply();
            return result;
        }

        public static void Cleanup()
        {
            if (TexBackgroundDark != null) Object.Destroy(TexBackgroundDark);
            if (TexBackgroundMid != null) Object.Destroy(TexBackgroundMid);
            if (TexBackgroundLight != null) Object.Destroy(TexBackgroundLight);
            if (TexBorder != null) Object.Destroy(TexBorder);
            if (TexAccent != null) Object.Destroy(TexAccent);
            if (TexHover != null) Object.Destroy(TexHover);
            if (TexSelected != null) Object.Destroy(TexSelected);
            if (TexSliderBg != null) Object.Destroy(TexSliderBg);
            if (TexSliderFill != null) Object.Destroy(TexSliderFill);
            if (TexDefaultIcon != null) Object.Destroy(TexDefaultIcon);

            _initialized = false;
        }
    }
}
