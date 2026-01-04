using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UIFramework.UI.Styles;
using UnityEngine;

namespace UIFramework.UI.Components
{
    /// <summary>
    /// Renders individual settings fields (sliders, toggles, text fields, etc.)
    /// </summary>
    public static class FieldRenderers
    {
        // ═══════════════════════════════════════════════════════════════
        // DROPDOWN STATE
        // ═══════════════════════════════════════════════════════════════

        private static string _activeDropdownId;
        private static DropdownType _activeDropdownType;
        private static Vector2 _dropdownScroll;

        // Store field name for popup title
        private static string _dropdownTitle;

        private enum DropdownType
        {
            None,
            Enum,
            KeyboardShortcut,
            KeyCode,
            Color,
            ValueList
        }

        public static bool IsDropdownOpen => _activeDropdownId != null;
        public static string ActiveDropdownId => _activeDropdownId;

        public static void CloseDropdown()
        {
            _activeDropdownId = null;
            _activeDropdownType = DropdownType.None;
        }

        /// <summary>
        /// Apply scroll delta to the dropdown scroll position.
        /// Called from MainWindow when scroll wheel is used while dropdown is open.
        /// </summary>
        public static void ApplyScrollDelta(float delta)
        {
            _dropdownScroll.y += delta;
            if (_dropdownScroll.y < 0) _dropdownScroll.y = 0;
        }

        // ═══════════════════════════════════════════════════════════════
        // BASIC FIELDS
        // ═══════════════════════════════════════════════════════════════

        public static bool DrawToggle(bool value, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            if (readOnly)
            {
                GUILayout.Label(value ? "[x] On" : "[ ] Off", UIStyles.LabelStyle);
                return value;
            }

            string label = value ? "On" : "Off";
            return GUILayout.Toggle(value, label, UIStyles.ToggleStyle, GUILayout.Width(70 * scale));
        }

        public static float DrawSlider(float value, float min, float max, float step = 0.01f, bool readOnly = false, float totalWidth = 250f)
        {
            float scale = UIStyles.ScaleFactor;
            float inputWidth = 70f * scale;
            float sliderWidth = totalWidth - inputWidth - 10f * scale;

            if (readOnly)
            {
                GUILayout.HorizontalSlider(value, min, max, UIStyles.SliderStyle, UIStyles.SliderThumbStyle, GUILayout.Width(sliderWidth));
            }
            else
            {
                value = GUILayout.HorizontalSlider(value, min, max, UIStyles.SliderStyle, UIStyles.SliderThumbStyle, GUILayout.Width(sliderWidth));
            }

            if (step > 0)
            {
                value = Mathf.Round(value / step) * step;
            }

            GUILayout.Space(5 * scale);

            string valueStr = value.ToString("F2");
            string newStr = GUILayout.TextField(valueStr, UIStyles.TextFieldStyle, GUILayout.Width(inputWidth));

            if (newStr != valueStr && float.TryParse(newStr, out float parsed))
            {
                value = Mathf.Clamp(parsed, min, max);
            }

            return value;
        }

        public static float DrawFloatField(float value, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            float fieldWidth = 100 * scale;
            string valueStr = value.ToString("F2");

            if (readOnly)
            {
                GUILayout.Label(valueStr, UIStyles.LabelStyle, GUILayout.Width(fieldWidth));
                return value;
            }

            string newStr = GUILayout.TextField(valueStr, UIStyles.TextFieldStyle, GUILayout.Width(fieldWidth));

            if (newStr != valueStr && float.TryParse(newStr, out float parsed))
            {
                return parsed;
            }

            return value;
        }

        public static int DrawIntField(int value, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            float fieldWidth = 100 * scale;
            string valueStr = value.ToString();

            if (readOnly)
            {
                GUILayout.Label(valueStr, UIStyles.LabelStyle, GUILayout.Width(fieldWidth));
                return value;
            }

            string newStr = GUILayout.TextField(valueStr, UIStyles.TextFieldStyle, GUILayout.Width(fieldWidth));

            if (newStr != valueStr && int.TryParse(newStr, out int parsed))
            {
                return parsed;
            }

            return value;
        }

        public static string DrawTextField(string value, bool readOnly = false, float width = 250f)
        {
            if (readOnly)
            {
                GUILayout.Label(value, UIStyles.LabelStyle, GUILayout.Width(width));
                return value;
            }

            return GUILayout.TextField(value ?? "", UIStyles.TextFieldStyle, GUILayout.Width(width));
        }

        public static Vector3 DrawVector3Field(Vector3 value, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            float labelWidth = 22 * scale;

            GUILayout.BeginHorizontal();

            GUILayout.Label("X:", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
            value.x = DrawFloatField(value.x, readOnly);

            GUILayout.Label("Y:", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
            value.y = DrawFloatField(value.y, readOnly);

            GUILayout.Label("Z:", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
            value.z = DrawFloatField(value.z, readOnly);

            GUILayout.EndHorizontal();

            return value;
        }

        // ═══════════════════════════════════════════════════════════════
        // ENUM DROPDOWN
        // ═══════════════════════════════════════════════════════════════

        private static Type _enumType;
        private static object _enumValue;

        // ValueList dropdown state
        private static object[] _valueListOptions;
        private static object _valueListCurrent;

        public static object DrawEnumDropdown(object value, Type enumType, string fieldId, bool readOnly = false)
        {
            if (value == null) return null;

            float scale = UIStyles.ScaleFactor;
            float btnWidth = 180 * scale;

            if (readOnly)
            {
                GUILayout.Label(value.ToString(), UIStyles.LabelStyle, GUILayout.Width(btnWidth));
                return value;
            }

            if (GUILayout.Button(value.ToString(), UIStyles.DropdownButtonStyle, GUILayout.Width(btnWidth)))
            {
                if (_activeDropdownId == fieldId)
                {
                    CloseDropdown();
                }
                else
                {
                    OpenDropdown(fieldId, DropdownType.Enum, enumType.Name);
                    _enumType = enumType;
                    _enumValue = value;
                }
            }

            return value;
        }

        /// <summary>
        /// Check if enum type has [Flags] attribute.
        /// </summary>
        public static bool IsFlagsEnum(Type enumType)
        {
            return enumType != null && enumType.IsEnum &&
                   enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
        }

        /// <summary>
        /// Draw a flags enum with checkboxes for multi-select.
        /// </summary>
        public static object DrawFlagsEnumField(object value, Type enumType, string fieldId, bool readOnly = false)
        {
            if (value == null || enumType == null) return value;

            float scale = UIStyles.ScaleFactor;
            int currentValue = Convert.ToInt32(value);

            string[] names = Enum.GetNames(enumType);
            Array values = Enum.GetValues(enumType);

            GUILayout.BeginVertical();

            for (int i = 0; i < names.Length; i++)
            {
                int flagValue = Convert.ToInt32(values.GetValue(i));

                // Skip "None" or zero values in display (but still allow unchecking all)
                if (flagValue == 0) continue;

                bool isSet = (currentValue & flagValue) == flagValue;
                bool wasEnabled = GUI.enabled;
                if (readOnly) GUI.enabled = false;

                bool newIsSet = GUILayout.Toggle(isSet, names[i], UIStyles.ToggleStyle);

                if (readOnly) GUI.enabled = wasEnabled;

                if (newIsSet != isSet)
                {
                    if (newIsSet)
                        currentValue |= flagValue;
                    else
                        currentValue &= ~flagValue;
                }
            }

            GUILayout.EndVertical();

            return Enum.ToObject(enumType, currentValue);
        }

        // ═══════════════════════════════════════════════════════════════
        // VALUE LIST DROPDOWN (AcceptableValueList)
        // ═══════════════════════════════════════════════════════════════

        public static object DrawValueListDropdown(object value, object[] options, string fieldId, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            float btnWidth = 180 * scale;

            if (options == null || options.Length == 0)
            {
                GUILayout.Label(value?.ToString() ?? "null", UIStyles.LabelStyle, GUILayout.Width(btnWidth));
                return value;
            }

            if (readOnly)
            {
                GUILayout.Label(value?.ToString() ?? "null", UIStyles.LabelStyle, GUILayout.Width(btnWidth));
                return value;
            }

            if (GUILayout.Button(value?.ToString() ?? "null", UIStyles.DropdownButtonStyle, GUILayout.Width(btnWidth)))
            {
                if (_activeDropdownId == fieldId)
                {
                    CloseDropdown();
                }
                else
                {
                    OpenDropdown(fieldId, DropdownType.ValueList, "Select Value");
                    _valueListOptions = options;
                    _valueListCurrent = value;
                }
            }

            return value;
        }

        // ═══════════════════════════════════════════════════════════════
        // KEYCODE (simple single key)
        // ═══════════════════════════════════════════════════════════════

        private static KeyCode _keyCodeValue;

        private static readonly KeyCode[] CommonKeys = new[]
        {
            KeyCode.None,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5,
            KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10,
            KeyCode.F11, KeyCode.F12,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7,
            KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F,
            KeyCode.G, KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L,
            KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R,
            KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
            KeyCode.Y, KeyCode.Z,
            KeyCode.Space, KeyCode.Return, KeyCode.Escape, KeyCode.Tab,
            KeyCode.Backspace, KeyCode.Delete, KeyCode.Insert, KeyCode.Home,
            KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
            KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7,
            KeyCode.Keypad8, KeyCode.Keypad9,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4
        };

        public static KeyCode DrawKeyCode(KeyCode value, string fieldId, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            if (readOnly)
            {
                GUILayout.Label(value.ToString(), UIStyles.LabelStyle, GUILayout.Width(120 * scale));
                return value;
            }

            // Show current key and Set button
            GUILayout.Label(value.ToString(), UIStyles.LabelStyle, GUILayout.Width(100 * scale));

            if (GUILayout.Button("Set", UIStyles.SmallButtonStyle, GUILayout.Width(50 * scale), GUILayout.Height(22 * scale)))
            {
                if (_activeDropdownId == fieldId)
                {
                    CloseDropdown();
                }
                else
                {
                    OpenDropdown(fieldId, DropdownType.KeyCode, "Select Key");
                    _keyCodeValue = value;
                }
            }

            return value;
        }

        // ═══════════════════════════════════════════════════════════════
        // KEYBOARD SHORTCUT (key + modifiers)
        // ═══════════════════════════════════════════════════════════════

        private static KeyboardShortcut _shortcutValue;

        public static KeyboardShortcut DrawKeyboardShortcut(KeyboardShortcut value, string fieldId, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;
            if (readOnly)
            {
                GUILayout.Label(FormatShortcut(value), UIStyles.LabelStyle, GUILayout.Width(180 * scale));
                return value;
            }

            GUILayout.Label(FormatShortcut(value), UIStyles.LabelStyle, GUILayout.Width(120 * scale));

            if (GUILayout.Button("Set", UIStyles.SmallButtonStyle, GUILayout.Width(50 * scale), GUILayout.Height(22 * scale)))
            {
                if (_activeDropdownId == fieldId)
                {
                    CloseDropdown();
                }
                else
                {
                    OpenDropdown(fieldId, DropdownType.KeyboardShortcut, "Set Shortcut");
                    _shortcutValue = value;
                }
            }

            return value;
        }

        private static string FormatShortcut(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return "None";

            var parts = new List<string>();

            if (shortcut.Modifiers.Contains(KeyCode.LeftControl) || shortcut.Modifiers.Contains(KeyCode.RightControl))
                parts.Add("Ctrl");
            if (shortcut.Modifiers.Contains(KeyCode.LeftAlt) || shortcut.Modifiers.Contains(KeyCode.RightAlt))
                parts.Add("Alt");
            if (shortcut.Modifiers.Contains(KeyCode.LeftShift) || shortcut.Modifiers.Contains(KeyCode.RightShift))
                parts.Add("Shift");

            parts.Add(shortcut.MainKey.ToString());

            return string.Join("+", parts);
        }

        // ═══════════════════════════════════════════════════════════════
        // COLOR PICKER
        // ═══════════════════════════════════════════════════════════════

        private static Color _colorValue;
        private static Texture2D _colorPreviewTex;

        private static readonly Color[] PresetColors = new[]
        {
            Color.white, Color.black, Color.gray,
            Color.red, new Color(1f, 0.5f, 0f), Color.yellow,
            Color.green, Color.cyan, Color.blue,
            Color.magenta, new Color(0.5f, 0f, 1f), new Color(1f, 0.75f, 0.8f)
        };

        public static Color DrawColorField(Color value, string fieldId, bool readOnly = false)
        {
            float scale = UIStyles.ScaleFactor;

            if (_colorPreviewTex == null)
            {
                _colorPreviewTex = new Texture2D(1, 1);
            }

            GUILayout.BeginHorizontal();

            _colorPreviewTex.SetPixel(0, 0, value);
            _colorPreviewTex.Apply();

            GUIStyle previewStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _colorPreviewTex }
            };
            GUILayout.Box("", previewStyle, GUILayout.Width(30 * scale), GUILayout.Height(20 * scale));

            if (!readOnly)
            {
                GUILayout.Space(5 * scale);

                float labelWidth = 20 * scale; // Enough for "R"/"G"/"B" with padding
                float sliderWidth = 80 * scale; // Increased from 50 for better precision

                GUILayout.Label("R", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
                value.r = GUILayout.HorizontalSlider(value.r, 0f, 1f, UIStyles.SliderStyle, UIStyles.SliderThumbStyle, GUILayout.Width(sliderWidth));

                GUILayout.Label("G", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
                value.g = GUILayout.HorizontalSlider(value.g, 0f, 1f, UIStyles.SliderStyle, UIStyles.SliderThumbStyle, GUILayout.Width(sliderWidth));

                GUILayout.Label("B", UIStyles.LabelMutedStyle, GUILayout.Width(labelWidth));
                value.b = GUILayout.HorizontalSlider(value.b, 0f, 1f, UIStyles.SliderStyle, UIStyles.SliderThumbStyle, GUILayout.Width(sliderWidth));

                if (GUILayout.Button("...", UIStyles.SmallButtonStyle, GUILayout.Width(25 * scale), GUILayout.Height(20 * scale)))
                {
                    if (_activeDropdownId == fieldId)
                    {
                        CloseDropdown();
                    }
                    else
                    {
                        OpenDropdown(fieldId, DropdownType.Color, "Select Color");
                        _colorValue = value;
                    }
                }
            }

            GUILayout.EndHorizontal();

            return value;
        }

        // ═══════════════════════════════════════════════════════════════
        // DROPDOWN HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void OpenDropdown(string fieldId, DropdownType type, string title = "Select")
        {
            _activeDropdownId = fieldId;
            _activeDropdownType = type;
            _dropdownScroll = Vector2.zero;
            _dropdownTitle = title;
        }

        // ═══════════════════════════════════════════════════════════════
        // POPUP DRAWING (call after scroll view ends)
        // ═══════════════════════════════════════════════════════════════

        private static Texture2D _backdropTex;

        public static object DrawDropdownPopup(string fieldId, Rect windowRect)
        {
            if (_activeDropdownId != fieldId)
                return null;

            // Create semi-transparent backdrop texture
            if (_backdropTex == null)
            {
                _backdropTex = new Texture2D(1, 1);
                _backdropTex.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
                _backdropTex.Apply();
            }

            // Draw backdrop (click outside closes dropdown)
            Rect backdropRect = new Rect(0, 0, windowRect.width, windowRect.height);
            GUI.DrawTexture(backdropRect, _backdropTex);

            if (Event.current.type == EventType.MouseDown &&
                !GetPopupRect(windowRect).Contains(Event.current.mousePosition))
            {
                CloseDropdown();
                Event.current.Use();
                return null;
            }

            object result = null;

            switch (_activeDropdownType)
            {
                case DropdownType.Enum:
                    result = DrawEnumPopup(windowRect);
                    break;
                case DropdownType.KeyCode:
                    result = DrawKeyCodePopup(windowRect);
                    break;
                case DropdownType.KeyboardShortcut:
                    result = DrawKeyboardShortcutPopup(windowRect);
                    break;
                case DropdownType.Color:
                    result = DrawColorPopup(windowRect);
                    break;
                case DropdownType.ValueList:
                    result = DrawValueListPopup(windowRect);
                    break;
            }

            return result;
        }

        private static Rect GetPopupRect(Rect windowRect)
        {
            float scale = UIStyles.ScaleFactor;

            // Base sizes - wider for better text display
            float popupWidth = 260f * scale;
            float popupHeight = 350f * scale;

            if (_activeDropdownType == DropdownType.Color)
            {
                popupWidth = 200f * scale;
                popupHeight = 220f * scale;
            }
            else if (_activeDropdownType == DropdownType.KeyboardShortcut)
            {
                popupWidth = 260f * scale;
                popupHeight = 400f * scale;
            }

            float x = (windowRect.width - popupWidth) / 2;
            float y = (windowRect.height - popupHeight) / 2;

            return new Rect(x, y, popupWidth, popupHeight);
        }

        // Helper for scaled popup internal dimensions
        private static float ScaledPadding => 12f * UIStyles.ScaleFactor;
        private static float ScaledHeaderHeight => 35f * UIStyles.ScaleFactor;
        private static float ScaledFooterHeight => 42f * UIStyles.ScaleFactor;
        private static float ScaledButtonHeight => 28f * UIStyles.ScaleFactor;
        private static float ScaledSmallButtonHeight => 26f * UIStyles.ScaleFactor;

        private static object DrawEnumPopup(Rect windowRect)
        {
            if (_enumType == null) return null;

            string[] names = Enum.GetNames(_enumType);
            Array values = Enum.GetValues(_enumType);

            Rect popupRect = GetPopupRect(windowRect);

            // Draw popup background
            GUI.Box(popupRect, "", UIStyles.BoxStyle);

            object selected = null;

            // Content area with scaled padding
            float padding = ScaledPadding;
            float contentHeight = popupRect.height - padding * 2;
            float scrollHeight = contentHeight - ScaledHeaderHeight - ScaledFooterHeight;

            GUILayout.BeginArea(new Rect(popupRect.x + padding, popupRect.y + padding, popupRect.width - padding * 2, contentHeight));

            // Title
            GUILayout.Label(_dropdownTitle, UIStyles.TitleStyle);
            GUILayout.Space(5 * UIStyles.ScaleFactor);

            // Scrollable list
            _dropdownScroll = GUILayout.BeginScrollView(_dropdownScroll, GUILayout.Height(scrollHeight));

            for (int i = 0; i < names.Length; i++)
            {
                bool isSelected = values.GetValue(i).Equals(_enumValue);
                GUIStyle style = isSelected ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;

                if (GUILayout.Button(names[i], style, GUILayout.Height(ScaledSmallButtonHeight)))
                {
                    selected = values.GetValue(i);
                    CloseDropdown();
                    Event.current.Use();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5 * UIStyles.ScaleFactor);

            // Cancel button
            if (GUILayout.Button("Cancel", UIStyles.ButtonStyle, GUILayout.Height(ScaledButtonHeight)))
            {
                CloseDropdown();
                Event.current.Use();
            }

            GUILayout.EndArea();

            return selected;
        }

        private static object DrawKeyCodePopup(Rect windowRect)
        {
            Rect popupRect = GetPopupRect(windowRect);

            GUI.Box(popupRect, "", UIStyles.BoxStyle);

            KeyCode? selected = null;

            // Content area with scaled padding
            float padding = ScaledPadding;
            float contentHeight = popupRect.height - padding * 2;
            float scrollHeight = contentHeight - ScaledHeaderHeight - ScaledFooterHeight;

            GUILayout.BeginArea(new Rect(popupRect.x + padding, popupRect.y + padding, popupRect.width - padding * 2, contentHeight));

            // Title
            GUILayout.Label(_dropdownTitle, UIStyles.TitleStyle);
            GUILayout.Space(5 * UIStyles.ScaleFactor);

            // Scrollable list
            _dropdownScroll = GUILayout.BeginScrollView(_dropdownScroll, GUILayout.Height(scrollHeight));

            foreach (var key in CommonKeys)
            {
                string keyName = key == KeyCode.None ? "None" : key.ToString();
                bool isSelected = key == _keyCodeValue;
                GUIStyle style = isSelected ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;

                if (GUILayout.Button(keyName, style, GUILayout.Height(ScaledSmallButtonHeight)))
                {
                    selected = key;
                    CloseDropdown();
                    Event.current.Use();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5 * UIStyles.ScaleFactor);

            // Cancel button
            if (GUILayout.Button("Cancel", UIStyles.ButtonStyle, GUILayout.Height(ScaledButtonHeight)))
            {
                CloseDropdown();
                Event.current.Use();
            }

            GUILayout.EndArea();

            return selected;
        }

        private static object DrawKeyboardShortcutPopup(Rect windowRect)
        {
            float scale = UIStyles.ScaleFactor;
            Rect popupRect = GetPopupRect(windowRect);

            GUI.Box(popupRect, "", UIStyles.BoxStyle);

            KeyboardShortcut? result = null;

            // Content area with scaled padding
            float padding = ScaledPadding;
            float contentHeight = popupRect.height - padding * 2;
            float headerHeight = 100f * scale;  // Title + modifiers + "Select Key" label
            float scrollHeight = contentHeight - headerHeight - ScaledFooterHeight;

            GUILayout.BeginArea(new Rect(popupRect.x + padding, popupRect.y + padding, popupRect.width - padding * 2, contentHeight));

            // Title
            GUILayout.Label(_dropdownTitle, UIStyles.TitleStyle);
            GUILayout.Space(5 * scale);

            // Modifier checkboxes
            GUILayout.Label("Modifiers:", UIStyles.LabelMutedStyle);
            bool ctrl = _shortcutValue.Modifiers.Contains(KeyCode.LeftControl);
            bool alt = _shortcutValue.Modifiers.Contains(KeyCode.LeftAlt);
            bool shift = _shortcutValue.Modifiers.Contains(KeyCode.LeftShift);

            GUILayout.BeginHorizontal();
            bool newCtrl = GUILayout.Toggle(ctrl, "Ctrl", UIStyles.ToggleStyle);
            bool newAlt = GUILayout.Toggle(alt, "Alt", UIStyles.ToggleStyle);
            bool newShift = GUILayout.Toggle(shift, "Shift", UIStyles.ToggleStyle);
            GUILayout.EndHorizontal();

            if (newCtrl != ctrl || newAlt != alt || newShift != shift)
            {
                _shortcutValue = BuildShortcut(_shortcutValue.MainKey, newCtrl, newAlt, newShift);
                Event.current.Use();
            }

            GUILayout.Space(5 * scale);
            GUILayout.Label("Select Key:", UIStyles.LabelMutedStyle);

            _dropdownScroll = GUILayout.BeginScrollView(_dropdownScroll, GUILayout.Height(scrollHeight));

            foreach (var key in CommonKeys)
            {
                string keyName = key == KeyCode.None ? "None" : key.ToString();
                bool isSelected = key == _shortcutValue.MainKey;
                GUIStyle style = isSelected ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;

                if (GUILayout.Button(keyName, style, GUILayout.Height(ScaledSmallButtonHeight)))
                {
                    result = BuildShortcut(key, newCtrl, newAlt, newShift);
                    CloseDropdown();
                    Event.current.Use();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5 * scale);

            // Cancel button
            if (GUILayout.Button("Cancel", UIStyles.ButtonStyle, GUILayout.Height(ScaledButtonHeight)))
            {
                CloseDropdown();
                Event.current.Use();
            }

            GUILayout.EndArea();

            return result;
        }

        private static object DrawColorPopup(Rect windowRect)
        {
            float scale = UIStyles.ScaleFactor;
            Rect popupRect = GetPopupRect(windowRect);

            GUI.Box(popupRect, "", UIStyles.BoxStyle);

            Color? selected = null;

            // Content area with scaled padding
            float padding = ScaledPadding;
            float contentHeight = popupRect.height - padding * 2;

            GUILayout.BeginArea(new Rect(popupRect.x + padding, popupRect.y + padding, popupRect.width - padding * 2, contentHeight));

            // Title
            GUILayout.Label(_dropdownTitle, UIStyles.TitleStyle);
            GUILayout.Space(5 * scale);

            GUILayout.Label("Presets:", UIStyles.LabelMutedStyle);
            GUILayout.Space(2 * scale);

            float colorBtnSize = 32f * scale;
            int col = 0;
            GUILayout.BeginHorizontal();

            foreach (var preset in PresetColors)
            {
                if (_colorPreviewTex == null)
                    _colorPreviewTex = new Texture2D(1, 1);

                _colorPreviewTex.SetPixel(0, 0, preset);
                _colorPreviewTex.Apply();

                GUIStyle colorBtn = new GUIStyle(GUI.skin.button)
                {
                    normal = { background = _colorPreviewTex },
                    hover = { background = _colorPreviewTex },
                    active = { background = _colorPreviewTex }
                };

                if (GUILayout.Button("", colorBtn, GUILayout.Width(colorBtnSize), GUILayout.Height(colorBtnSize)))
                {
                    selected = preset;
                    CloseDropdown();
                    Event.current.Use();
                }

                col++;
                if (col >= 4)
                {
                    col = 0;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Cancel button
            if (GUILayout.Button("Cancel", UIStyles.ButtonStyle, GUILayout.Height(ScaledButtonHeight)))
            {
                CloseDropdown();
                Event.current.Use();
            }

            GUILayout.EndArea();

            return selected;
        }

        private static object DrawValueListPopup(Rect windowRect)
        {
            if (_valueListOptions == null || _valueListOptions.Length == 0) return null;

            Rect popupRect = GetPopupRect(windowRect);

            GUI.Box(popupRect, "", UIStyles.BoxStyle);

            object selected = null;

            // Content area with scaled padding
            float padding = ScaledPadding;
            float contentHeight = popupRect.height - padding * 2;
            float scrollHeight = contentHeight - ScaledHeaderHeight - ScaledFooterHeight;

            GUILayout.BeginArea(new Rect(popupRect.x + padding, popupRect.y + padding, popupRect.width - padding * 2, contentHeight));

            // Title
            GUILayout.Label(_dropdownTitle, UIStyles.TitleStyle);
            GUILayout.Space(5 * UIStyles.ScaleFactor);

            // Scrollable list
            _dropdownScroll = GUILayout.BeginScrollView(_dropdownScroll, GUILayout.Height(scrollHeight));

            foreach (var option in _valueListOptions)
            {
                string optionName = option?.ToString() ?? "null";
                bool isSelected = option?.Equals(_valueListCurrent) ?? (_valueListCurrent == null);
                GUIStyle style = isSelected ? UIStyles.SmallButtonAccentStyle : UIStyles.SmallButtonStyle;

                if (GUILayout.Button(optionName, style, GUILayout.Height(ScaledSmallButtonHeight)))
                {
                    selected = option;
                    CloseDropdown();
                    Event.current.Use();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5 * UIStyles.ScaleFactor);

            // Cancel button
            if (GUILayout.Button("Cancel", UIStyles.ButtonStyle, GUILayout.Height(ScaledButtonHeight)))
            {
                CloseDropdown();
                Event.current.Use();
            }

            GUILayout.EndArea();

            return selected;
        }

        private static KeyboardShortcut BuildShortcut(KeyCode mainKey, bool ctrl, bool alt, bool shift)
        {
            var modifiers = new List<KeyCode>();
            if (ctrl) modifiers.Add(KeyCode.LeftControl);
            if (alt) modifiers.Add(KeyCode.LeftAlt);
            if (shift) modifiers.Add(KeyCode.LeftShift);

            return new KeyboardShortcut(mainKey, modifiers.ToArray());
        }

        // Legacy compatibility
        public static object DrawEnumField(object value, Type enumType, bool readOnly = false)
        {
            return DrawEnumDropdown(value, enumType, $"enum_{enumType.Name}_{value?.GetHashCode()}", readOnly);
        }
    }
}
