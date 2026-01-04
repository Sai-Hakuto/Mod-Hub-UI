using System;

namespace UIFramework.Attributes
{
    // ═══════════════════════════════════════════════════════════════
    // MOD-LEVEL ATTRIBUTES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks a class as a UIFramework mod settings container.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIFModAttribute : Attribute
    {
        public string ModId { get; }
        public string ModName { get; }
        public string ModVersion { get; }

        public UIFModAttribute(string modId, string modName, string modVersion)
        {
            ModId = modId;
            ModName = modName;
            ModVersion = modVersion;
        }
    }

    /// <summary>
    /// Specifies the mod author.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIFAuthorAttribute : Attribute
    {
        public string Author { get; }

        public UIFAuthorAttribute(string author)
        {
            Author = author;
        }
    }

    /// <summary>
    /// Specifies tags for the mod (for search/filtering).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIFTagsAttribute : Attribute
    {
        public string[] Tags { get; }

        public UIFTagsAttribute(params string[] tags)
        {
            Tags = tags;
        }
    }

    /// <summary>
    /// Specifies the mod icon path (128x128 recommended).
    /// Path is relative to BepInEx/plugins/[ModFolder]/
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIFIconAttribute : Attribute
    {
        public string IconPath { get; }

        public UIFIconAttribute(string iconPath)
        {
            IconPath = iconPath;
        }
    }

    /// <summary>
    /// Specifies cover images for carousel (460x215 recommended).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
    public class UIFImagesAttribute : Attribute
    {
        public string[] ImagePaths { get; }

        public UIFImagesAttribute(params string[] imagePaths)
        {
            ImagePaths = imagePaths;
        }
    }

    /// <summary>
    /// Specifies mod description shown on the mod page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIFDescriptionAttribute : Attribute
    {
        public string Description { get; }

        public UIFDescriptionAttribute(string description)
        {
            Description = description;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SECTION ATTRIBUTES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Groups fields into a named section.
    /// Can be applied to class (default section) or fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFSectionAttribute : Attribute
    {
        public string SectionName { get; }
        public int Order { get; }

        public UIFSectionAttribute(string sectionName, int order = 0)
        {
            SectionName = sectionName;
            Order = order;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FIELD ATTRIBUTES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Display name for a field in the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFNameAttribute : Attribute
    {
        public string Name { get; }

        public UIFNameAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Tooltip/description for a field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFTooltipAttribute : Attribute
    {
        public string Tooltip { get; }

        public UIFTooltipAttribute(string tooltip)
        {
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Min/Max range for numeric fields. Creates a slider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFRangeAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }
        public float Step { get; }

        public UIFRangeAttribute(float min, float max, float step = 0.01f)
        {
            Min = min;
            Max = max;
            Step = step;
        }
    }

    /// <summary>
    /// Marks a field as percentage (0-100 or 0-1).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFPercentageAttribute : Attribute
    {
        public bool Normalized { get; } // true = 0-1, false = 0-100

        public UIFPercentageAttribute(bool normalized = true)
        {
            Normalized = normalized;
        }
    }

    /// <summary>
    /// Marks a field as advanced (hidden by default).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFAdvancedAttribute : Attribute { }

    /// <summary>
    /// Hides a field from the UI completely.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFHiddenAttribute : Attribute { }

    /// <summary>
    /// Makes a field read-only in the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFReadOnlyAttribute : Attribute { }

    /// <summary>
    /// Specifies display order within a section.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFOrderAttribute : Attribute
    {
        public int Order { get; }

        public UIFOrderAttribute(int order)
        {
            Order = order;
        }
    }

    /// <summary>
    /// Custom format string for displaying values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UIFFormatAttribute : Attribute
    {
        public string Format { get; }

        public UIFFormatAttribute(string format)
        {
            Format = format;
        }
    }
}
