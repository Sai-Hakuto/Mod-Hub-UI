using System;
using UIFramework.Core;

namespace UIFramework.API
{
    /// <summary>
    /// Static API for registering mods with UIFramework.
    /// </summary>
    public static class UIFApi
    {
        /// <summary>
        /// Check if UIFramework is loaded and ready.
        /// </summary>
        public static bool IsLoaded => ModRegistry.Instance != null;

        /// <summary>
        /// Register a mod using IUIFMod interface.
        /// </summary>
        public static void Register(IUIFMod mod)
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("UIFramework is not loaded. Check IsLoaded before registering.");
            }

            ModRegistry.Instance.RegisterMod(mod);
        }

        /// <summary>
        /// Register a mod using just the settings object (requires UIFMod attribute).
        /// </summary>
        public static void Register(object settingsObject, string pluginFolder = null)
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("UIFramework is not loaded. Check IsLoaded before registering.");
            }

            ModRegistry.Instance.RegisterModFromAttributes(settingsObject, pluginFolder);
        }

        /// <summary>
        /// Unregister a mod.
        /// </summary>
        public static void Unregister(string modId)
        {
            ModRegistry.Instance?.UnregisterMod(modId);
        }

        /// <summary>
        /// Open UIFramework window.
        /// </summary>
        public static void Open()
        {
            WindowManager.Instance?.Open();
        }

        /// <summary>
        /// Close UIFramework window.
        /// </summary>
        public static void Close()
        {
            WindowManager.Instance?.Close();
        }

        /// <summary>
        /// Toggle UIFramework window.
        /// </summary>
        public static void Toggle()
        {
            WindowManager.Instance?.Toggle();
        }

        /// <summary>
        /// Open settings page for a specific mod.
        /// </summary>
        public static void OpenMod(string modId)
        {
            WindowManager.Instance?.OpenModSettings(modId);
        }
    }
}
