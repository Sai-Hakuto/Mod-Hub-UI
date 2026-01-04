using System;
using System.Collections.Generic;
using UIFramework.UI;
using UIFramework.UI.Components;
using UnityEngine;

namespace UIFramework.Core
{
    /// <summary>
    /// Manages the main UIFramework window state.
    /// </summary>
    public class WindowManager
    {
        public static WindowManager Instance { get; private set; }

        public bool IsOpen { get; private set; }
        public string CurrentModId { get; private set; }
        public EUIPage CurrentPage { get; private set; } = EUIPage.ModList;
        public bool PageChanged { get; private set; }

        // Scroll position memory per page
        private Dictionary<string, Vector2> _scrollPositions = new Dictionary<string, Vector2>();
        public Vector2 LastModListScrollPosition { get; set; }

        public event Action OnWindowOpened;
        public event Action OnWindowClosed;

        private bool _cursorWasVisible;
        private CursorLockMode _previousLockMode;

        public WindowManager()
        {
            Instance = this;
        }

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;

            // Save cursor state
            _cursorWasVisible = Cursor.visible;
            _previousLockMode = Cursor.lockState;

            // Unlock cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            OnWindowOpened?.Invoke();

            Plugin.Log.LogInfo("[UIFramework] Window opened");
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;

            // Close any open dropdowns
            FieldRenderers.CloseDropdown();

            // Reset to main page on close
            CurrentPage = EUIPage.ModList;
            CurrentModId = null;
            PageChanged = true;

            // Restore cursor state
            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _previousLockMode;

            OnWindowClosed?.Invoke();

            Plugin.Log.LogInfo("[UIFramework] Window closed");
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void NavigateTo(EUIPage page, string modId = null)
        {
            if (CurrentPage != page || CurrentModId != modId)
            {
                PageChanged = true;
                // Close any open dropdowns when changing pages
                FieldRenderers.CloseDropdown();
            }
            CurrentPage = page;
            CurrentModId = modId;
        }

        public void ClearPageChanged()
        {
            PageChanged = false;
        }

        public void OpenModSettings(string modId)
        {
            NavigateTo(EUIPage.ModSettings, modId);
            Open();
        }

        public void BackToModList()
        {
            NavigateTo(EUIPage.ModList);
        }

        public void Update()
        {
            // Keep cursor unlocked while window is open
            if (IsOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }

    public enum EUIPage
    {
        ModList,
        ModSettings,
        Search,
        About
    }
}
