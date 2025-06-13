using System;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseUI.Infrastructure;
using ViverseWebGLAPI;

namespace ViverseUI.Managers
{
    /// <summary>
    /// Manages configuration UI and persistence for Viverse SDK settings
    /// </summary>
    public class ViverseConfigurationManager : ViverseManagerBase
    {
        // UI Elements
        private TextField _clientIdInput;
        private Button _saveConfigButton;
        private Label _configStatus;
        
        // State
        private ViverseConfigData _config;
        
        // Events
        public event Action<ViverseConfigData> OnConfigurationChanged;
        public event Action<HostConfig> OnHostConfigReady;
        
        /// <summary>
        /// Current configuration data
        /// </summary>
        public ViverseConfigData CurrentConfig => _config;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Service context</param>
        /// <param name="root">Root UI element</param>
        public ViverseConfigurationManager(IViverseServiceContext context, VisualElement root) 
            : base(context, root)
        {
        }
        
        /// <summary>
        /// Initialize UI elements
        /// </summary>
        protected override void InitializeUIElements()
        {
            _clientIdInput = Root.Q<TextField>("client-id-input");
            _saveConfigButton = Root.Q<Button>("save-config-button");
            _configStatus = Root.Q<Label>("config-status");
            
            if (_clientIdInput == null || _saveConfigButton == null || _configStatus == null)
            {
                Debug.LogError("Configuration UI elements not found. Check UXML structure.");
            }
        }
        
        /// <summary>
        /// Setup event handlers
        /// </summary>
        protected override void SetupEventHandlers()
        {
            if (_saveConfigButton != null)
            {
                _saveConfigButton.clicked += OnSaveConfigClicked;
            }
        }
        
        /// <summary>
        /// Load initial configuration state
        /// </summary>
        protected override void LoadInitialState()
        {
            LoadConfiguration();
        }
        
        /// <summary>
        /// Cleanup event handlers
        /// </summary>
        protected override void CleanupEventHandlers()
        {
            if (_saveConfigButton != null)
            {
                _saveConfigButton.clicked -= OnSaveConfigClicked;
            }
        }
        
        /// <summary>
        /// Load configuration from persistent storage
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                _config = ViverseConfigData.LoadFromPrefs();
                
                if (_clientIdInput != null)
                {
                    _clientIdInput.value = _config.ClientId;
                }
                
                UpdateConfigStatus();
                
                Debug.Log("Configuration loaded successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load configuration: {e.Message}");
                UIState.ShowError($"Failed to load configuration: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle save configuration button click
        /// </summary>
        private void OnSaveConfigClicked()
        {
            SaveConfiguration();
        }
        
        /// <summary>
        /// Save configuration to persistent storage
        /// </summary>
        private void SaveConfiguration()
        {
            try
            {
                if (_clientIdInput == null)
                {
                    UIState.ShowError("Configuration UI not properly initialized");
                    return;
                }
                
                string clientId = _clientIdInput.value?.Trim();
                
                if (string.IsNullOrEmpty(clientId))
                {
                    UIState.ShowError("Please enter a valid Client ID");
                    return;
                }
                
                // Update configuration
                _config.ClientId = clientId;
                _config.SaveToPrefs();
                
                UpdateConfigStatus();
                
                // Notify listeners that configuration has changed
                OnConfigurationChanged?.Invoke(_config);
                
                // Generate and provide host config
                var hostConfig = GetEnvironmentConfig();
                OnHostConfigReady?.Invoke(hostConfig);
                
                UIState.ShowMessage("Configuration saved successfully");
                Debug.Log($"Configuration saved: ClientId = {clientId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save configuration: {e.Message}");
                UIState.ShowError($"Failed to save configuration: {e.Message}");
            }
        }
        
        /// <summary>
        /// Update configuration status display
        /// </summary>
        private void UpdateConfigStatus()
        {
            if (_configStatus == null) return;
            
            try
            {
                bool isValid = !string.IsNullOrEmpty(_config?.ClientId);
                
                _configStatus.text = isValid 
                    ? $"✓ Configuration valid (Client ID: {_config.ClientId})"
                    : "⚠ Please configure Client ID";
                    
                // Update status styling based on validity
                _configStatus.RemoveFromClassList("config-valid");
                _configStatus.RemoveFromClassList("config-invalid");
                _configStatus.AddToClassList(isValid ? "config-valid" : "config-invalid");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update config status: {e.Message}");
                _configStatus.text = "❌ Configuration error";
            }
        }
        
        /// <summary>
        /// Get environment configuration based on current URL
        /// </summary>
        /// <returns>Host configuration for current environment</returns>
        public HostConfig GetEnvironmentConfig()
        {
            try
            {
                var hostConfigUtil = new HostConfigUtil();
                HostConfigUtil.HostType hostType = hostConfigUtil.GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
                
                if (HostConfigLookup.HostTypeToDefaultHostConfig.TryGetValue(hostType, out var config))
                {
                    Debug.Log($"Using host config for environment: {hostType}");
                    return config;
                }
                
                Debug.LogWarning($"Unknown host type {hostType}, using PROD configuration");
                return HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get environment config: {e.Message}");
                // Fallback to PROD configuration
                return HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
            }
        }
        
        /// <summary>
        /// Validate current configuration
        /// </summary>
        /// <returns>True if configuration is valid</returns>
        public bool ValidateConfiguration()
        {
            bool isValid = !string.IsNullOrEmpty(_config?.ClientId);
            
            if (!isValid)
            {
                UIState.ShowError("Please configure a valid Client ID before proceeding");
            }
            
            return isValid;
        }
        
        /// <summary>
        /// Get current configuration data
        /// </summary>
        /// <returns>Current configuration or null if not loaded</returns>
        public ViverseConfigData GetCurrentConfig()
        {
            return _config;
        }
    }
}