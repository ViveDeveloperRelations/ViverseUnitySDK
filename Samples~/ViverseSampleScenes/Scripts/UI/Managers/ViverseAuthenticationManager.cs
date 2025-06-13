using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseUI.Infrastructure;
using ViverseWebGLAPI;

namespace ViverseUI.Managers
{
    /// <summary>
    /// Manages authentication, login/logout operations, and core SDK initialization
    /// </summary>
    public class ViverseAuthenticationManager : ViverseManagerBase
    {
        // UI Elements
        private TextField _stateParameterInput;
        private Button _checkAuthButton;
        private TextField _authStatusResult;
        private Button _loginButton;
        private TextField _loginResult;
        private Button _logoutButton;
        private TextField _tokenResult;
        
        // State
        private ViverseCore _core;
        private ViverseConfigData _currentConfig;
        private HostConfig _hostConfig;
        private bool _isAuthenticated;
        private string _lastInitializedClientId; // Track the last client ID used for initialization
        
        // Events
        public event Action<bool> OnAuthenticationStateChanged;
        public event Action<ViverseCore> OnCoreInitialized;
        public event Action OnLogoutCompleted;
        
        /// <summary>
        /// Whether user is currently authenticated
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;
        
        /// <summary>
        /// Current ViverseCore instance
        /// </summary>
        public ViverseCore CoreInstance => _core;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Service context</param>
        /// <param name="root">Root UI element</param>
        public ViverseAuthenticationManager(IViverseServiceContext context, VisualElement root) 
            : base(context, root)
        {
        }
        
        /// <summary>
        /// Initialize UI elements
        /// </summary>
        protected override void InitializeUIElements()
        {
            _stateParameterInput = Root.Q<TextField>("state-parameter-input");
            _checkAuthButton = Root.Q<Button>("check-auth-button");
            _authStatusResult = Root.Q<TextField>("auth-status-result");
            _loginButton = Root.Q<Button>("login-button");
            _loginResult = Root.Q<TextField>("login-result");
            _logoutButton = Root.Q<Button>("logout-button");
            _tokenResult = Root.Q<TextField>("token-result");
            
            // Validate required UI elements
            if (_loginButton == null || _logoutButton == null)
            {
                Debug.LogError("Authentication UI elements not found. Check UXML structure.");
            }
        }
        
        /// <summary>
        /// Setup event handlers
        /// </summary>
        protected override void SetupEventHandlers()
        {
            if (_checkAuthButton != null)
                _checkAuthButton.clicked += async () => await CheckAuthStatus();
                
            if (_loginButton != null)
                _loginButton.clicked += async () => await StartLogin();
                
            if (_logoutButton != null)
                _logoutButton.clicked += async () => await LogoutUser();
        }
        
        /// <summary>
        /// Load initial state
        /// </summary>
        protected override void LoadInitialState()
        {
            // Initially disable logout button until authenticated
            _logoutButton?.SetEnabled(false);
            
            // Auto-initialize SDK if saved configuration exists
            _ = Task.Run(async () => await TryAutoInitializeOnStart());
        }
        
        /// <summary>
        /// Attempt to auto-initialize SDK on start if saved client ID exists
        /// </summary>
        private async Task TryAutoInitializeOnStart()
        {
            try
            {
                // Try to load saved client ID from local storage
                var savedConfig = ViverseConfigData.LoadFromPrefs();
                if (savedConfig != null && !string.IsNullOrEmpty(savedConfig.ClientId) && 
                    savedConfig.ClientId != "YOUR_CLIENT_ID")
                {
                    Debug.Log($"Auto-initializing SDK with saved client ID: {savedConfig.ClientId}");
                    
                    // Simple configuration setup - just use saved client ID with default host config
                    _currentConfig = savedConfig;
                    _hostConfig = GetEnvironmentConfig();
                    
                    // Initialize SDK automatically
                    bool initSuccess = await InitializeCore(handleOAuthCallback: true);
                    if (initSuccess)
                    {
                        Debug.Log("✅ SDK auto-initialized successfully on start");
                        
                        // Automatically check auth status after initialization
                        await CheckAuthStatus();
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ SDK auto-initialization failed on start");
                    }
                }
                else
                {
                    Debug.Log("No valid saved client ID found - manual initialization required");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Auto-initialization on start failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Try to auto-initialize SDK for operations if saved client ID exists
        /// </summary>
        private async Task<bool> TryAutoInitializeForOperation()
        {
            try
            {
                // Check if we have current configuration
                if (_currentConfig != null && _hostConfig != null)
                {
                    Debug.Log("Using existing configuration for auto-initialization");
                    return await InitializeCore(handleOAuthCallback: false);
                }
                
                // Try to load saved client ID from local storage
                var savedConfig = ViverseConfigData.LoadFromPrefs();
                if (savedConfig != null && !string.IsNullOrEmpty(savedConfig.ClientId) && 
                    savedConfig.ClientId != "YOUR_CLIENT_ID")
                {
                    Debug.Log($"Auto-initializing SDK for operation with saved client ID: {savedConfig.ClientId}");
                    
                    // Simple setup - just use saved client ID with default host config
                    _currentConfig = savedConfig;
                    _hostConfig = GetEnvironmentConfig();
                    
                    return await InitializeCore(handleOAuthCallback: false);
                }
                
                Debug.LogWarning("No valid saved client ID available for auto-initialization");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"Auto-initialization for operation failed: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Cleanup event handlers
        /// </summary>
        protected override void CleanupEventHandlers()
        {
            if (_checkAuthButton != null)
                _checkAuthButton.clicked -= async () => await CheckAuthStatus();
                
            if (_loginButton != null)
                _loginButton.clicked -= async () => await StartLogin();
                
            if (_logoutButton != null)
                _logoutButton.clicked -= async () => await LogoutUser();
        }
        
        /// <summary>
        /// Get environment configuration based on current URL
        /// </summary>
        private HostConfig GetEnvironmentConfig()
        {
            try
            {
                HostConfigUtil.HostType hostType =
                    new HostConfigUtil().GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
                return HostConfigLookup.HostTypeToDefaultHostConfig.TryGetValue(hostType, out var config)
                    ? config
                    : HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to get environment config: {e.Message}. Using PROD config.");
                return HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
            }
        }
        
        /// <summary>
        /// Update configuration for authentication
        /// </summary>
        /// <param name="config">Configuration data</param>
        /// <param name="hostConfig">Host configuration</param>
        public void UpdateConfiguration(ViverseConfigData config, HostConfig hostConfig)
        {
            bool clientIdChanged = _currentConfig != null && 
                                 !string.IsNullOrEmpty(_lastInitializedClientId) && 
                                 _lastInitializedClientId != config?.ClientId;
            
            _currentConfig = config;
            _hostConfig = hostConfig;
            
            Debug.Log($"Authentication manager configuration updated - Client ID: {config?.ClientId}");
            
            // If client ID changed and we have an initialized core, trigger reinitialization
            if (clientIdChanged && _core != null)
            {
                Debug.Log($"Client ID changed from '{_lastInitializedClientId}' to '{config?.ClientId}' - will reinitialize on next operation");
                // Don't reinitialize immediately, wait for next operation to trigger it
                // This avoids interrupting current operations
            }
        }
        
        /// <summary>
        /// Initialize the Viverse SDK core with automatic OAuth callback handling
        /// </summary>
        /// <param name="handleOAuthCallback">Whether to automatically detect and handle OAuth callbacks (default: true)</param>
        /// <param name="forceReinit">Force reinitialization even if core already exists (for client ID changes)</param>
        /// <returns>True if initialization successful</returns>
        public async Task<bool> InitializeCore(bool handleOAuthCallback = true, bool forceReinit = false)
        {
            if (_currentConfig == null || _hostConfig == null)
            {
                UIState.ShowError("Configuration not ready. Please save configuration first.");
                return false;
            }
            
            // Check if we need to reinitialize due to client ID change
            bool needsReinitForClientIdChange = _core != null && 
                                              !string.IsNullOrEmpty(_lastInitializedClientId) && 
                                              _lastInitializedClientId != _currentConfig.ClientId;
            
            if (_core != null && !forceReinit && !needsReinitForClientIdChange)
            {
                Debug.Log("ViverseCore already initialized");
                return true;
            }
            
            // Force reinit if client ID changed
            if (needsReinitForClientIdChange)
            {
                Debug.Log($"Client ID changed from '{_lastInitializedClientId}' to '{_currentConfig.ClientId}' - forcing reinitialization");
                forceReinit = true;
            }
            
            // If force reinit, dispose existing core first
            if (_core != null && forceReinit)
            {
                Debug.Log("Force reinitializing ViverseCore due to configuration change");
                try
                {
                    // ViverseCore doesn't have Dispose method, just clear the reference
                    _core = null;
                    SetAuthenticationState(false); // Reset auth state when reinitializing
                    Debug.Log("Cleared existing ViverseCore instance for reinitialization");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error clearing existing core during reinit: {e.Message}");
                    _core = null;
                    SetAuthenticationState(false);
                }
            }
            
            UIState.SetLoading(true, "Initializing Viverse SDK...");
            
            try
            {
                _core = new ViverseCore();
                var initResult = await _core.Initialize(_hostConfig, CancellationToken.None);
                
                if (!initResult.IsSuccess)
                {
                    UIState.ShowError($"SDK initialization failed: {initResult.ErrorMessage}");
                    return false;
                }
                
                // Initialize SSO service
                bool ssoInitSuccess = await _core.SSOService.Initialize(_currentConfig.ClientId);
                if (!ssoInitSuccess)
                {
                    UIState.ShowError("Failed to initialize SSO service");
                    return false;
                }
                
                // Automatically handle OAuth callback unless disabled
                if (handleOAuthCallback)
                {
                    var oauthResult = await _core.SSOService.DetectAndHandleOAuthCallback(_currentConfig.ClientId);
                    if (oauthResult.IsSuccess && oauthResult.Data.detected)
                    {
                        Debug.Log($"OAuth callback detected and handled: code={oauthResult.Data.code}, state={oauthResult.Data.state}");
                        UIState.ShowMessage("OAuth callback processed successfully");
                        
                        // Immediately check auth status after OAuth callback
                        await CheckAuthStatus();
                    }
                }
                
                Debug.Log("ViverseCore initialized successfully");
                
                // Track the client ID used for this initialization
                _lastInitializedClientId = _currentConfig.ClientId;
                
                OnCoreInitialized?.Invoke(_core);
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error initializing Viverse SDK: {e.Message}");
                UIState.ShowError($"Error initializing Viverse SDK: {e.Message}");
                return false;
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        /// <summary>
        /// Check current authentication status
        /// </summary>
        private async Task CheckAuthStatus()
        {
            // Auto-initialize SDK if not already initialized
            if (_core == null)
            {
                Debug.Log("SDK not initialized - attempting auto-initialization for auth check");
                bool initSuccess = await TryAutoInitializeForOperation();
                if (!initSuccess)
                {
                    UIState.ShowError("SDK not initialized. Please save configuration and try again.");
                    return;
                }
            }
            
            UIState.SetLoading(true, "Checking authentication status...");
            
            try
            {
                var authResult = await _core.SSOService.CheckAuth();
                
                if (authResult.IsSuccess && authResult.Data != null)
                {
                    var data = authResult.Data;
                    
                    if (_authStatusResult != null)
                    {
                        _authStatusResult.value = $"✓ Authenticated\n" +
                                                $"Account ID: {data.account_id}\n" +
                                                $"Expires in: {data.expires_in}s\n" +
                                                $"State: {data.state ?? "None"}";
                    }
                    
                    if (_tokenResult != null)
                    {
                        _tokenResult.value = $"Access Token: {data.access_token}\n" +
                                           $"Account ID: {data.account_id}\n" +
                                           $"Expires in: {data.expires_in} seconds";
                    }
                    
                    SetAuthenticationState(true);
                    UIState.ShowMessage("Authentication check successful");
                }
                else
                {
                    // ✅ Use safe logging extension for comprehensive error reporting
                    authResult.LogError("Check Auth Status");
                    
                    if (_authStatusResult != null)
                        _authStatusResult.value = "✗ Not authenticated or invalid token";
                        
                    if (_tokenResult != null)
                        _tokenResult.value = "No valid token";
                        
                    SetAuthenticationState(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Check auth failed: {e.Message}");
                UIState.ShowError($"Check auth failed: {e.Message}");
                
                if (_authStatusResult != null)
                    _authStatusResult.value = $"✗ Error: {e.Message}";
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        /// <summary>
        /// Start login process
        /// </summary>
        private async Task StartLogin()
        {
            if (_core == null)
            {
                bool initSuccess = await InitializeCore();
                if (!initSuccess) return;
            }
            
            UIState.SetLoading(true, "Starting login...");
            
            try
            {
                string stateParam = _stateParameterInput?.value?.Trim();
                if (string.IsNullOrEmpty(stateParam))
                {
                    stateParam = null;
                }
                
                var loginResult = await _core.SSOService.LoginWithWorlds(stateParam);
                
                if (loginResult.IsSuccess)
                {
                    // Enhanced LoginWithWorlds now returns auth tokens directly
                    if (!string.IsNullOrEmpty(loginResult.Data?.access_token))
                    {
                        if (_loginResult != null)
                            _loginResult.value = $"✅ Login completed successfully!\nToken length: {loginResult.Data.access_token.Length} characters";
                        
                        UIState.ShowMessage("Login completed - auth token received!");
                        
                        // Immediately update auth status to show the user is now authenticated
                        await CheckAuthStatus();
                    }
                    else
                    {
                        if (_loginResult != null)
                            _loginResult.value = "Login completed but no auth token received";
                            
                        UIState.ShowMessage("Login completed - checking auth status...");
                        
                        // Check auth status after a brief delay
                        await Task.Delay(1000);
                        await CheckAuthStatus();
                    }
                }
                else
                {
                    // ✅ Use safe logging extension for comprehensive error reporting
                    loginResult.LogError("Login With Worlds");
                    UIState.ShowError($"Login failed: {loginResult.ErrorMessage}");
                    
                    if (_loginResult != null)
                        _loginResult.value = $"✗ Login failed: {loginResult.ErrorMessage}";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during login: {e.Message}");
                UIState.ShowError($"Error during login: {e.Message}");
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        /// <summary>
        /// Logout current user
        /// </summary>
        private async Task LogoutUser()
        {
            if (_core == null)
            {
                UIState.ShowError("No active session to logout");
                return;
            }
            
            UIState.SetLoading(true, "Logging out...");
            
            try
            {
                var urlParts = URLUtils.ParseURL(Application.absoluteURL);
                var logoutResult = await _core.SSOService.Logout(urlParts);
                
                if (logoutResult.IsSuccess)
                {
                    // Clear UI state
                    ClearAuthenticationUI();
                    
                    // Reset authentication state
                    SetAuthenticationState(false);
                    
                    // Reset core reference
                    _core = null;
                    
                    UIState.ShowMessage("Logged out successfully");
                    OnLogoutCompleted?.Invoke();
                }
                else
                {
                    UIState.ShowError($"Logout failed: {logoutResult.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during logout: {e.Message}");
                UIState.ShowError($"Error during logout: {e.Message}");
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        
        /// <summary>
        /// Set authentication state and update UI accordingly
        /// </summary>
        /// <param name="isAuthenticated">Authentication state</param>
        private void SetAuthenticationState(bool isAuthenticated)
        {
            _isAuthenticated = isAuthenticated;
            
            // Update button states
            _loginButton?.SetEnabled(!isAuthenticated);
            _logoutButton?.SetEnabled(isAuthenticated);
            
            // Notify listeners
            OnAuthenticationStateChanged?.Invoke(isAuthenticated);
            
            Debug.Log($"Authentication state changed: {isAuthenticated}");
        }
        
        /// <summary>
        /// Force reinitialize client (recovery method for authentication issues)
        /// </summary>
        public async Task<bool> ForceReinitializeClient()
        {
            if (_currentConfig == null || _core == null)
            {
                UIState.ShowError("Configuration or core not ready");
                return false;
            }
            
            UIState.SetLoading(true, "Reinitializing client...");
            
            try
            {
                var result = await _core.SSOService.ForceReinitializeClient(_currentConfig.ClientId);
                if (result.IsSuccess)
                {
                    UIState.ShowMessage("Client reinitialized successfully");
                    // Check auth status after reinitialization
                    await CheckAuthStatus();
                    return true;
                }
                else
                {
                    UIState.ShowError($"Client reinitialization failed: {result.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reinitializing client: {e.Message}");
                UIState.ShowError($"Error reinitializing client: {e.Message}");
                return false;
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        /// <summary>
        /// Clear authentication-related UI elements
        /// </summary>
        private void ClearAuthenticationUI()
        {
            if (_loginResult != null)
                _loginResult.value = "";
                
            if (_tokenResult != null)
                _tokenResult.value = "";
                
            if (_authStatusResult != null)
                _authStatusResult.value = "";
        }
    }
}