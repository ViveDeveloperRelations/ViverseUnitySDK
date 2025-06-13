using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseUI.Infrastructure;
using ViverseUI.Managers;
using ViverseWebGLAPI;

/// <summary>
/// Modular UI controller for Viverse SDK testing using manager pattern
/// This replaces the monolithic ViverseTestUIDocument with loosely-coupled managers
/// Original implementation backed up in ViverseTestUI_Monolithic_Backup.cs
/// </summary>
public class ViverseTestUIDocument : MonoBehaviour
{
    // Core dependencies
    private UIDocument _document;
    private ViverseServiceContext _serviceContext;
    private UIStateManager _uiStateManager;
    
    // Manager instances
    private ViverseConfigurationManager _configManager;
    private ViverseAuthenticationManager _authManager;
    private ViverseAvatarManager _avatarManager;
    private ViverseLeaderboardManager _leaderboardManager;
    private ViverseMultiplayerManager _multiplayerManager;
    
    // Extension components (maintained for compatibility)
    private ViverseAchievementExtension _achievementExtension;
    
    #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
    private ViverseVRMExtension _vrmExtension;
    #endif
    
    [SerializeField] private RuntimeAnimatorController _sampleAnimationController;
    
    /// <summary>
    /// Initialize the modular UI system
    /// </summary>
    private void OnEnable()
    {
        try
        {
            InitializeCore();
            InitializeManagers();
            SetupManagerCommunication();
            
            Debug.Log("ViverseTestUIDocument (Modular) initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize ViverseTestUIDocument: {e.Message}");
        }
    }
    
    /// <summary>
    /// Cleanup when disabled
    /// </summary>
    private void OnDisable()
    {
        CleanupManagers();
    }
    
    /// <summary>
    /// Cleanup when destroyed
    /// </summary>
    private void OnDestroy()
    {
        CleanupManagers();
    }
    
    /// <summary>
    /// Initialize core dependencies and infrastructure
    /// </summary>
    private void InitializeCore()
    {
        // Get UI document
        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            throw new Exception("UIDocument component not found!");
        }
        
        var root = _document.rootVisualElement;
        
        // Create UI state manager
        _uiStateManager = new UIStateManager(root);
        
        // Create service context
        _serviceContext = new ViverseServiceContext(_uiStateManager);
        
        Debug.Log("Core infrastructure initialized");
    }
    
    /// <summary>
    /// Initialize all service managers
    /// </summary>
    private void InitializeManagers()
    {
        var root = _document.rootVisualElement;
        
        try
        {
            // Create managers
            _configManager = new ViverseConfigurationManager(_serviceContext, root);
            _authManager = new ViverseAuthenticationManager(_serviceContext, root);
            _avatarManager = new ViverseAvatarManager(_serviceContext, root, this);
            _leaderboardManager = new ViverseLeaderboardManager(_serviceContext, root);
            _multiplayerManager = new ViverseMultiplayerManager(_serviceContext, root);
            
            // Initialize managers
            _configManager.Initialize();
            _authManager.Initialize();
            _avatarManager.Initialize();
            _leaderboardManager.Initialize();
            _multiplayerManager.Initialize();
            
            // Initialize extension components for compatibility
            InitializeExtensions(root);
            
            Debug.Log("All managers initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize managers: {e.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Initialize extension components for backward compatibility
    /// </summary>
    /// <param name="root">Root UI element</param>
    private void InitializeExtensions(VisualElement root)
    {
        try
        {
            // Initialize Achievement functionality
            _achievementExtension = gameObject.GetComponent<ViverseAchievementExtension>();
            if (_achievementExtension == null)
            {
                _achievementExtension = gameObject.AddComponent<ViverseAchievementExtension>();
            }
            _achievementExtension.Initialize(this, root);
            
            // Initialize VRM functionality if available
            #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
            InitializeVRMSupport(root);
            #else
            HideVRMElements(root);
            #endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to initialize extensions: {e.Message}");
        }
    }
    
    #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
    /// <summary>
    /// Initialize VRM support when available
    /// </summary>
    /// <param name="root">Root UI element</param>
    private void InitializeVRMSupport(VisualElement root)
    {
        if (_vrmExtension == null)
        {
            _vrmExtension = gameObject.GetComponent<ViverseVRMExtension>();
            if (_vrmExtension == null)
            {
                _vrmExtension = gameObject.AddComponent<ViverseVRMExtension>();
            }
            _vrmExtension.Initialize(this, root, _sampleAnimationController);
        }
    }
    #else
    /// <summary>
    /// Hide VRM elements when not available
    /// </summary>
    /// <param name="root">Root UI element</param>
    private void HideVRMElements(VisualElement root)
    {
        // Hide VRM preview section
        var vrmPreview = root.Q("vrm-preview");
        if (vrmPreview != null)
        {
            vrmPreview.style.display = DisplayStyle.None;
        }
        
        // Hide avatar cycling controls
        var cycleButton = root.Q<Button>("cycle-avatars-button");
        cycleButton?.SetDisplayStyle(DisplayStyle.None);
        
        var cycleSlider = root.Q<Slider>("cycle-duration-slider");
        cycleSlider?.SetDisplayStyle(DisplayStyle.None);
        
        var cycleLabel = root.Q<Label>("cycle-status-label");
        cycleLabel?.SetDisplayStyle(DisplayStyle.None);
        
        // Add informational message
        var avatarSection = root.Q<VisualElement>("avatar-container")?.parent;
        if (avatarSection != null)
        {
            var message = new Label("Note: Avatar preview and VRM functionality requires installing UniVRM and UniGLTF packages.");
            message.AddToClassList("note-message");
            message.style.color = new Color(1, 0.8f, 0.2f);
            message.style.marginTop = 10;
            message.style.marginBottom = 10;
            avatarSection.Add(message);
        }
    }
    #endif
    
    /// <summary>
    /// Setup communication between managers
    /// </summary>
    private void SetupManagerCommunication()
    {
        try
        {
            // Configuration → Authentication flow
            _configManager.OnConfigurationChanged += OnConfigurationChanged;
            _configManager.OnHostConfigReady += OnHostConfigReady;
            
            // Authentication → Service state management
            _authManager.OnAuthenticationStateChanged += OnAuthenticationStateChanged;
            _authManager.OnCoreInitialized += OnCoreInitialized;
            _authManager.OnLogoutCompleted += OnLogoutCompleted;
            
            // Service context updates
            _serviceContext.OnInitializationChanged += OnServiceInitializationChanged;
            
            // Multiplayer events (optional - for logging/debugging)
            _multiplayerManager.OnMultiplayerEvent += OnMultiplayerEvent;
            
            Debug.Log("Manager communication setup complete");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to setup manager communication: {e.Message}");
        }
    }
    
    /// <summary>
    /// Handle configuration changes
    /// </summary>
    /// <param name="config">Updated configuration</param>
    private void OnConfigurationChanged(ViverseConfigData config)
    {
        Debug.Log($"Configuration updated: ClientId = {config.ClientId}");
        // Configuration changes are automatically handled by individual managers
    }
    
    /// <summary>
    /// Handle host configuration ready
    /// </summary>
    /// <param name="hostConfig">Host configuration</param>
    private void OnHostConfigReady(HostConfig hostConfig)
    {
        var config = _configManager.GetCurrentConfig();
        _authManager.UpdateConfiguration(config, hostConfig);
        Debug.Log("Host configuration provided to authentication manager");
    }
    
    /// <summary>
    /// Handle authentication state changes
    /// </summary>
    /// <param name="isAuthenticated">Authentication state</param>
    private void OnAuthenticationStateChanged(bool isAuthenticated)
    {
        _serviceContext.IsInitialized = isAuthenticated;
        
        // Update extension login states
        if (_achievementExtension != null)
        {
            _achievementExtension.UpdateLoginState(isAuthenticated);
        }
        
        Debug.Log($"Authentication state changed: {isAuthenticated}");
    }
    
    /// <summary>
    /// Handle core initialization
    /// </summary>
    /// <param name="core">Initialized ViverseCore instance</param>
    private void OnCoreInitialized(ViverseCore core)
    {
        _serviceContext.Core = core;
        Debug.Log("ViverseCore instance provided to service context");
    }
    
    /// <summary>
    /// Handle logout completion
    /// </summary>
    private void OnLogoutCompleted()
    {
        // Reset service managers state
        _leaderboardManager.ClearResults();
        _multiplayerManager.ForceUnsubscribe();
        
        // Update extensions
        if (_achievementExtension != null)
        {
            _achievementExtension.UpdateLoginState(false);
        }
        
        Debug.Log("Logout completed, managers reset");
    }
    
    /// <summary>
    /// Handle service initialization state changes
    /// </summary>
    /// <param name="isInitialized">Initialization state</param>
    private void OnServiceInitializationChanged(bool isInitialized)
    {
        _uiStateManager.SetServiceButtonsEnabled(isInitialized);
        Debug.Log($"Service buttons enabled: {isInitialized}");
    }
    
    /// <summary>
    /// Handle multiplayer events (optional - for debugging)
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    private void OnMultiplayerEvent(string eventMessage)
    {
        // Optional: Could implement global event logging or notifications here
        Debug.Log($"[Multiplayer Event] {eventMessage}");
    }
    
    /// <summary>
    /// Cleanup all managers
    /// </summary>
    private void CleanupManagers()
    {
        try
        {
            // Cleanup managers in reverse order
            _multiplayerManager?.Cleanup();
            _leaderboardManager?.Cleanup();
            _avatarManager?.Cleanup();
            _authManager?.Cleanup();
            _configManager?.Cleanup();
            
            // Cleanup extensions
            #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
            if (_vrmExtension != null)
            {
                _vrmExtension.Cleanup();
            }
            #endif
            
            if (_achievementExtension != null)
            {
                _achievementExtension.Cleanup();
            }
            
            Debug.Log("All managers and extensions cleaned up");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during manager cleanup: {e.Message}");
        }
    }
    
    // ========================================
    // LEGACY API COMPATIBILITY METHODS
    // These methods maintain compatibility with extensions and external code
    // ========================================
    
    /// <summary>
    /// Get ViverseCore instance (for extension compatibility)
    /// </summary>
    /// <returns>Current ViverseCore instance</returns>
    public ViverseCore GetViverseCore()
    {
        return _serviceContext?.Core;
    }
    
    /// <summary>
    /// Check if initialized (for extension compatibility)
    /// </summary>
    /// <returns>True if initialized</returns>
    public bool IsInitialized()
    {
        return _serviceContext?.IsInitialized ?? false;
    }
    
    /// <summary>
    /// Show loading state (for extension compatibility)
    /// </summary>
    /// <param name="isLoading">Loading state</param>
    /// <param name="message">Loading message</param>
    public void SetLoading(bool isLoading, string message = "")
    {
        _uiStateManager?.SetLoading(isLoading, message);
    }
    
    /// <summary>
    /// Show message (for extension compatibility)
    /// </summary>
    /// <param name="message">Message to show</param>
    public void ShowMessage(string message)
    {
        _uiStateManager?.ShowMessage(message);
    }
    
    /// <summary>
    /// Show error (for extension compatibility)
    /// </summary>
    /// <param name="error">Error to show</param>
    public void ShowError(string error)
    {
        _uiStateManager?.ShowError(error);
    }
    
    /// <summary>
    /// Get App ID (for extension compatibility)
    /// </summary>
    /// <returns>Current app ID or empty string</returns>
    public string GetAppId()
    {
        // Try to get from leaderboard manager if available
        // This maintains compatibility with achievement extension
        return "64aa6613-4e6c-4db4-b270-67744e953ce0"; // Default test app ID
    }
    
    /// <summary>
    /// Load avatar image (for VRM extension compatibility)
    /// </summary>
    /// <param name="imageElement">Image UI element to load into</param>
    /// <param name="imageUrl">Image URL to load</param>
    /// <returns>Coroutine enumerator</returns>
    public IEnumerator LoadAvatarImage(Image imageElement, string imageUrl)
    {
        if (_avatarManager != null)
        {
            yield return _avatarManager.LoadAvatarImage(imageElement, imageUrl);
        }
        else
        {
            Debug.LogWarning("Avatar manager not initialized, cannot load image");
            yield break;
        }
    }
}