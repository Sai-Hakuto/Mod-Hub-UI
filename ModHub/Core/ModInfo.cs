using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UIFramework.API;
using UIFramework.Attributes;
using UnityEngine;

namespace UIFramework.Core
{
    /// <summary>
    /// Contains all information about a registered mod.
    /// </summary>
    public class ModInfo
    {
        public string ModId { get; set; }
        public string ModName { get; set; }
        public string ModVersion { get; set; }
        public string ModAuthor { get; set; }
        public string Description { get; set; }
        public string[] Tags { get; set; }
        public string IconPath { get; set; }
        public string[] ImagePaths { get; set; }
        public string PluginFolder { get; set; }

        public object SettingsObject { get; set; }
        public IUIFMod ModInterface { get; set; }

        public Texture2D IconTexture { get; set; }
        public List<Texture2D> ImageTextures { get; set; } = new List<Texture2D>();

        public List<SectionInfo> Sections { get; set; } = new List<SectionInfo>();

        // BepInEx auto-discovery properties
        public bool IsAutoDiscovered { get; set; }
        public BaseUnityPlugin BepInExPlugin { get; set; }
        public List<ConfigEntryBase> ConfigEntries { get; set; }

        // Pending changes tracking (for undo on exit without save)
        private Dictionary<string, object> _pendingChanges = new Dictionary<string, object>();
        public bool HasPendingChanges => _pendingChanges.Count > 0;

        /// <summary>
        /// Parse settings object and build section/field info.
        /// </summary>
        public void ParseSettings()
        {
            Sections.Clear();

            if (SettingsObject == null) return;

            var type = SettingsObject.GetType();

            // Group fields by section
            var sectionDict = new Dictionary<string, SectionInfo>();
            string defaultSection = "General";

            // Check if class has default section
            var classSectionAttr = type.GetCustomAttribute<UIFSectionAttribute>();
            if (classSectionAttr != null)
            {
                defaultSection = classSectionAttr.SectionName;
            }

            // Create a temporary instance to get default values for fields
            object defaultInstance = null;
            try
            {
                defaultInstance = Activator.CreateInstance(type);
            }
            catch { }

            // Parse fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                // Skip hidden fields
                if (field.GetCustomAttribute<UIFHiddenAttribute>() != null)
                    continue;

                var fieldInfo = CreateFieldInfoFromField(field, defaultInstance, defaultSection, sectionDict);
                if (fieldInfo != null)
                {
                    AddFieldToSection(fieldInfo, field, defaultSection, sectionDict);
                }
            }

            // Parse properties (especially for ConfigEntry<T> properties)
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                // Skip hidden properties
                if (prop.GetCustomAttribute<UIFHiddenAttribute>() != null)
                    continue;

                // Skip properties without UIFSection or UIFName attributes (not meant for UI)
                if (prop.GetCustomAttribute<UIFSectionAttribute>() == null &&
                    prop.GetCustomAttribute<UIFNameAttribute>() == null)
                    continue;

                var fieldInfo = CreateFieldInfoFromProperty(prop, defaultSection, sectionDict);
                if (fieldInfo != null)
                {
                    AddFieldToSection(fieldInfo, prop, defaultSection, sectionDict);
                }
            }

            // Sort sections and fields
            foreach (var section in sectionDict.Values)
            {
                section.Fields.Sort((a, b) => a.Order.CompareTo(b.Order));
                Sections.Add(section);
            }

            Sections.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        private FieldInfo_UIF CreateFieldInfoFromField(FieldInfo field, object defaultInstance, string defaultSection, Dictionary<string, SectionInfo> sectionDict)
        {
            var fieldInfo = new FieldInfo_UIF
            {
                Field = field,
                FieldName = field.Name,
                FieldType = field.FieldType
            };

            // Get display name
            var nameAttr = field.GetCustomAttribute<UIFNameAttribute>();
            fieldInfo.DisplayName = nameAttr?.Name ?? field.Name;

            // Get tooltip
            var tooltipAttr = field.GetCustomAttribute<UIFTooltipAttribute>();
            fieldInfo.Tooltip = tooltipAttr?.Tooltip ?? "";

            // Get range
            var rangeAttr = field.GetCustomAttribute<UIFRangeAttribute>();
            if (rangeAttr != null)
            {
                fieldInfo.HasRange = true;
                fieldInfo.Min = rangeAttr.Min;
                fieldInfo.Max = rangeAttr.Max;
                fieldInfo.Step = rangeAttr.Step;
            }

            // Check if advanced
            fieldInfo.IsAdvanced = field.GetCustomAttribute<UIFAdvancedAttribute>() != null;

            // Check if read-only
            fieldInfo.IsReadOnly = field.GetCustomAttribute<UIFReadOnlyAttribute>() != null;

            // Get order
            var orderAttr = field.GetCustomAttribute<UIFOrderAttribute>();
            fieldInfo.Order = orderAttr?.Order ?? 0;

            // Capture initial value (current value at load time)
            try
            {
                fieldInfo.InitialValue = field.GetValue(SettingsObject);
            }
            catch { }

            // Capture default value for reset (from fresh instance)
            if (defaultInstance != null)
            {
                try
                {
                    fieldInfo.DefaultValue = field.GetValue(defaultInstance);
                }
                catch { }
            }

            return fieldInfo;
        }

        private FieldInfo_UIF CreateFieldInfoFromProperty(PropertyInfo prop, string defaultSection, Dictionary<string, SectionInfo> sectionDict)
        {
            // Check if this is a ConfigEntry<T> property
            var propType = prop.PropertyType;
            bool isConfigEntry = propType.IsGenericType &&
                                 propType.GetGenericTypeDefinition() == typeof(ConfigEntry<>);

            if (!isConfigEntry)
            {
                // For now, only support ConfigEntry properties
                return null;
            }

            // Get the ConfigEntry instance
            ConfigEntryBase configEntry = null;
            Type valueType = null;
            try
            {
                var entryObj = prop.GetValue(SettingsObject);
                configEntry = entryObj as ConfigEntryBase;
                if (configEntry == null) return null;

                valueType = propType.GetGenericArguments()[0];
            }
            catch
            {
                return null;
            }

            var fieldInfo = new FieldInfo_UIF
            {
                Property = prop,
                FieldName = prop.Name,
                FieldType = valueType,
                IsConfigEntry = true,
                ConfigEntry = configEntry
            };

            // Get display name
            var nameAttr = prop.GetCustomAttribute<UIFNameAttribute>();
            fieldInfo.DisplayName = nameAttr?.Name ?? prop.Name;

            // Get tooltip (prefer attribute, fallback to ConfigEntry description)
            var tooltipAttr = prop.GetCustomAttribute<UIFTooltipAttribute>();
            fieldInfo.Tooltip = tooltipAttr?.Tooltip ?? configEntry.Description?.Description ?? "";

            // Get range from attribute or ConfigEntry's AcceptableValueRange
            var rangeAttr = prop.GetCustomAttribute<UIFRangeAttribute>();
            if (rangeAttr != null)
            {
                fieldInfo.HasRange = true;
                fieldInfo.Min = rangeAttr.Min;
                fieldInfo.Max = rangeAttr.Max;
                fieldInfo.Step = rangeAttr.Step;
            }
            else if (configEntry.Description?.AcceptableValues != null)
            {
                ExtractAcceptableValues(fieldInfo, configEntry);
            }

            // Check if advanced
            fieldInfo.IsAdvanced = prop.GetCustomAttribute<UIFAdvancedAttribute>() != null;

            // Check if read-only
            fieldInfo.IsReadOnly = prop.GetCustomAttribute<UIFReadOnlyAttribute>() != null;

            // Get order
            var orderAttr = prop.GetCustomAttribute<UIFOrderAttribute>();
            fieldInfo.Order = orderAttr?.Order ?? 0;

            return fieldInfo;
        }

        private void ExtractAcceptableValues(FieldInfo_UIF fieldInfo, ConfigEntryBase configEntry)
        {
            var acceptable = configEntry.Description?.AcceptableValues;
            if (acceptable == null) return;

            var acceptableType = acceptable.GetType();
            if (!acceptableType.IsGenericType) return;

            var genericDef = acceptableType.GetGenericTypeDefinition();

            // AcceptableValueRange<T> - slider
            if (genericDef == typeof(AcceptableValueRange<>))
            {
                var minProp = acceptableType.GetProperty("MinValue");
                var maxProp = acceptableType.GetProperty("MaxValue");

                if (minProp != null && maxProp != null)
                {
                    fieldInfo.HasRange = true;
                    fieldInfo.MinValue = Convert.ToSingle(minProp.GetValue(acceptable));
                    fieldInfo.MaxValue = Convert.ToSingle(maxProp.GetValue(acceptable));
                }
            }
            // AcceptableValueList<T> - dropdown
            else if (genericDef == typeof(AcceptableValueList<>))
            {
                var valuesProp = acceptableType.GetProperty("AcceptableValues");
                if (valuesProp != null)
                {
                    var valuesArray = valuesProp.GetValue(acceptable) as Array;
                    if (valuesArray != null && valuesArray.Length > 0)
                    {
                        fieldInfo.HasAcceptableValues = true;
                        fieldInfo.AcceptableValues = new object[valuesArray.Length];
                        for (int i = 0; i < valuesArray.Length; i++)
                        {
                            fieldInfo.AcceptableValues[i] = valuesArray.GetValue(i);
                        }
                    }
                }
            }
        }

        private void AddFieldToSection(FieldInfo_UIF fieldInfo, MemberInfo member, string defaultSection, Dictionary<string, SectionInfo> sectionDict)
        {
            var sectionAttr = member.GetCustomAttribute<UIFSectionAttribute>();
            string sectionName = sectionAttr?.SectionName ?? defaultSection;

            if (!sectionDict.TryGetValue(sectionName, out var section))
            {
                section = new SectionInfo
                {
                    SectionName = sectionName,
                    Order = sectionAttr?.Order ?? 0
                };
                sectionDict[sectionName] = section;
            }

            section.Fields.Add(fieldInfo);
        }

        /// <summary>
        /// Get current value of a field (works for both attributes and ConfigEntry).
        /// </summary>
        public object GetFieldValue(FieldInfo_UIF fieldInfo)
        {
            return fieldInfo.GetValue(SettingsObject);
        }

        /// <summary>
        /// Set value of a field (works for both attributes and ConfigEntry).
        /// </summary>
        public void SetFieldValue(FieldInfo_UIF fieldInfo, object value)
        {
            fieldInfo.SetValue(SettingsObject, value);
        }

        /// <summary>
        /// Reset a single field to its default/initial value.
        /// </summary>
        public void ResetField(FieldInfo_UIF fieldInfo)
        {
            var resetValue = fieldInfo.GetResetValue();
            if (resetValue != null)
            {
                fieldInfo.SetValue(SettingsObject, resetValue);
            }
        }

        /// <summary>
        /// Reset all fields to their default/initial values.
        /// </summary>
        public void ResetAllFields()
        {
            foreach (var section in Sections)
            {
                foreach (var field in section.Fields)
                {
                    ResetField(field);
                }
            }
        }

        /// <summary>
        /// Capture current values as snapshot for potential undo.
        /// Call this when entering mod settings page.
        /// </summary>
        public void CaptureSnapshot()
        {
            _pendingChanges.Clear();
            foreach (var section in Sections)
            {
                foreach (var field in section.Fields)
                {
                    string key = $"{section.SectionName}_{field.FieldName}";
                    _pendingChanges[key] = field.GetValue(SettingsObject);
                }
            }
        }

        /// <summary>
        /// Restore values to snapshot state (undo changes).
        /// Call this when exiting mod settings without saving.
        /// </summary>
        public void RestoreSnapshot()
        {
            foreach (var section in Sections)
            {
                foreach (var field in section.Fields)
                {
                    string key = $"{section.SectionName}_{field.FieldName}";
                    if (_pendingChanges.TryGetValue(key, out var savedValue))
                    {
                        field.SetValue(SettingsObject, savedValue);
                    }
                }
            }
            _pendingChanges.Clear();
        }

        /// <summary>
        /// Commit changes (clear snapshot, keep current values).
        /// Call this when saving mod settings.
        /// </summary>
        public void CommitChanges()
        {
            _pendingChanges.Clear();
        }
    }

    /// <summary>
    /// Information about a settings section.
    /// </summary>
    public class SectionInfo
    {
        public string SectionName { get; set; }
        // Alias for compatibility
        public string Name
        {
            get => SectionName;
            set => SectionName = value;
        }
        public int Order { get; set; }
        public string[] ImagePaths { get; set; }
        public List<FieldInfo_UIF> Fields { get; set; } = new List<FieldInfo_UIF>();
    }

    /// <summary>
    /// Information about a single settings field.
    /// Supports both UIFramework attributes and BepInEx ConfigEntry.
    /// </summary>
    public class FieldInfo_UIF
    {
        public FieldInfo Field { get; set; }
        public PropertyInfo Property { get; set; }
        public string FieldName { get; set; }
        // Alias for compatibility
        public string Name
        {
            get => FieldName;
            set => FieldName = value;
        }
        public string DisplayName { get; set; }
        public string Tooltip { get; set; }
        public Type FieldType { get; set; }

        public bool HasRange { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public float Step { get; set; }
        // Aliases for compatibility
        public float MinValue
        {
            get => Min;
            set => Min = value;
        }
        public float MaxValue
        {
            get => Max;
            set => Max = value;
        }

        public bool IsAdvanced { get; set; }
        public bool IsReadOnly { get; set; }
        public int Order { get; set; }

        // Default value for reset functionality
        public object DefaultValue { get; set; }
        // Initial value when first loaded (fallback for reset)
        public object InitialValue { get; set; }

        // BepInEx ConfigEntry support
        public bool IsConfigEntry { get; set; }
        public ConfigEntryBase ConfigEntry { get; set; }

        // AcceptableValueList support
        public bool HasAcceptableValues { get; set; }
        public object[] AcceptableValues { get; set; }

        /// <summary>
        /// Get the value to reset to (DefaultValue if available, otherwise InitialValue).
        /// </summary>
        public object GetResetValue()
        {
            if (IsConfigEntry && ConfigEntry != null)
            {
                return ConfigEntry.DefaultValue;
            }
            // Prefer explicit default, fallback to initial
            return DefaultValue ?? InitialValue;
        }

        /// <summary>
        /// Get current value (works for both attribute fields and ConfigEntry).
        /// </summary>
        public object GetValue(object settingsObject)
        {
            if (IsConfigEntry && ConfigEntry != null)
            {
                return ConfigEntry.BoxedValue;
            }
            return Field?.GetValue(settingsObject);
        }

        /// <summary>
        /// Set value (works for both attribute fields and ConfigEntry).
        /// </summary>
        public void SetValue(object settingsObject, object value)
        {
            if (IsConfigEntry && ConfigEntry != null)
            {
                ConfigEntry.BoxedValue = value;
            }
            else if (Field != null)
            {
                Field.SetValue(settingsObject, value);
            }
        }
    }
}
