using System;

namespace UIFramework.API
{
    /// <summary>
    /// Interface for mods that want to register with UIFramework.
    /// Implement this interface OR use UIFMod attribute on settings class.
    /// </summary>
    public interface IUIFMod
    {
        /// <summary>
        /// Unique identifier for the mod (e.g., "maskirovka").
        /// </summary>
        string ModId { get; }

        /// <summary>
        /// Display name of the mod.
        /// </summary>
        string ModName { get; }

        /// <summary>
        /// Version string (e.g., "1.0.0").
        /// </summary>
        string ModVersion { get; }

        /// <summary>
        /// Author name.
        /// </summary>
        string ModAuthor { get; }

        /// <summary>
        /// Short description of the mod.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Tags for search/filtering.
        /// </summary>
        string[] Tags { get; }

        /// <summary>
        /// Path to mod icon (128x128). Relative to plugin folder.
        /// </summary>
        string IconPath { get; }

        /// <summary>
        /// Paths to carousel/cover images. Relative to plugin folder.
        /// </summary>
        string[] ImagePaths { get; }

        /// <summary>
        /// The settings object containing configurable fields.
        /// </summary>
        object GetSettings();

        /// <summary>
        /// Called when settings are saved.
        /// </summary>
        void OnSettingsSaved();

        /// <summary>
        /// Called when settings are reset to defaults.
        /// </summary>
        void OnSettingsReset();
    }

    /// <summary>
    /// Base class for easy IUIFMod implementation.
    /// Inherit from this and override what you need.
    /// </summary>
    public abstract class UIFModBase : IUIFMod
    {
        public abstract string ModId { get; }
        public abstract string ModName { get; }
        public virtual string ModVersion => "1.0.0";
        public virtual string ModAuthor => "Unknown";
        public virtual string Description => "";
        public virtual string[] Tags => Array.Empty<string>();
        public virtual string IconPath => null;
        public virtual string[] ImagePaths => Array.Empty<string>();

        public abstract object GetSettings();

        public virtual void OnSettingsSaved() { }
        public virtual void OnSettingsReset() { }
    }
}
