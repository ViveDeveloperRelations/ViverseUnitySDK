using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using UnityEngine.Networking;
using ViverseWebGLAPI;
using Avatar = ViverseWebGLAPI.Avatar;

public class ViverseTestUIDocument : MonoBehaviour
{
    private UIDocument _document;
    private ViverseCore _core;
    private ViverseConfigData _config;
    private bool _isInitialized;

    // UI Elements
    private TextField _clientIdInput;
    private Button _saveConfigButton;
    private Label _configStatus;
    private Button _loginButton;
    private TextField _loginResult;
    private Button _logoutButton;
    private TextField _tokenResult;
    private Button _loadProfileButton;
    private Button _loadAvatarsButton;
    private Button _loadPublicAvatarsButton;
    private TextField _profileResult;
    private VisualElement _avatarContainer;
    private TextField _appIdInput;
    private TextField _leaderboardNameInput;
    private TextField _scoreInput;
    private Button _uploadScoreButton;
    private Button _getLeaderboardButton;
    private TextField _leaderboardResult;
    private VisualElement _loadingOverlay;
    private Label _loadingText;

    private ViverseAchievementExtension _achievementExtension;

    // Avatar-related elements that are conditionally available
    [SerializeField] private RuntimeAnimatorController _sampleAnimationController;
#if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
    private ViverseVRMExtension _vrmExtension;
#endif

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            Debug.LogError("UIDocument component not found!");
            return;
        }

        InitializeUIElements();
        SetupEventHandlers();
        LoadConfiguration();
        CheckForCallback();
    }

    private void InitializeUIElements()
    {
        var root = _document.rootVisualElement;

        // Get references to all UI elements
        _clientIdInput = root.Q<TextField>("client-id-input");
        _saveConfigButton = root.Q<Button>("save-config-button");
        _configStatus = root.Q<Label>("config-status");
        _loginButton = root.Q<Button>("login-button");
        _loginResult = root.Q<TextField>("login-result");
        _logoutButton = root.Q<Button>("logout-button");
        _tokenResult = root.Q<TextField>("token-result");
        _loadProfileButton = root.Q<Button>("load-profile-button");
        _loadAvatarsButton = root.Q<Button>("load-avatars-button");
        _loadPublicAvatarsButton = root.Q<Button>("load-public-avatars-button");
        _profileResult = root.Q<TextField>("profile-result");
        _avatarContainer = root.Q<VisualElement>("avatar-container");
        _appIdInput = root.Q<TextField>("app-id-input");
        _leaderboardNameInput = root.Q<TextField>("leaderboard-name-input");
        _scoreInput = root.Q<TextField>("score-input");
        _uploadScoreButton = root.Q<Button>("upload-score-button");
        _getLeaderboardButton = root.Q<Button>("get-leaderboard-button");
        _leaderboardResult = root.Q<TextField>("leaderboard-result");
        _loadingOverlay = root.Q<VisualElement>("loading-overlay");
        _loadingText = _loadingOverlay.Q<Label>("loading-text");

        // Initialize Achievement functionality
        _achievementExtension = gameObject.AddComponent<ViverseAchievementExtension>();
        _achievementExtension.Initialize(this, _document.rootVisualElement);

        // Initially disable logout button
        _logoutButton.SetEnabled(false);
        SetServiceButtonsEnabled(false);

        // Initialize VRM functionality if available
        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
        InitializeVRMSupport(root);
        #else
        // Hide VRM-specific UI elements when UniVRM is not available
        HideVRMElements(root);
        #endif
    }

    #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
    private void InitializeVRMSupport(VisualElement root)
    {
        // Create the VRM extension component
        if (_vrmExtension == null)
        {
            _vrmExtension = gameObject.AddComponent<ViverseVRMExtension>();
            _vrmExtension.Initialize(this, root, _sampleAnimationController);
        }
    }
    #else
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
        if (cycleButton != null)
        {
            cycleButton.style.display = DisplayStyle.None;
        }

        var cycleSlider = root.Q<Slider>("cycle-duration-slider");
        if (cycleSlider != null)
        {
            cycleSlider.style.display = DisplayStyle.None;
        }

        var cycleLabel = root.Q<Label>("cycle-status-label");
        if (cycleLabel != null)
        {
            cycleLabel.style.display = DisplayStyle.None;
        }

        // Add a message explaining that VRM support requires additional packages
        var avatarSection = root.Q<VisualElement>("avatar-container").parent;
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

    private void SetupEventHandlers()
    {
        _saveConfigButton.clicked += SaveConfiguration;
        _loginButton.clicked += StartLogin;
        _logoutButton.clicked += LogoutUser;
        _loadProfileButton.clicked += async () => await LoadProfile();
        _loadAvatarsButton.clicked += async () => await LoadMyAvatars();
        _loadPublicAvatarsButton.clicked += async () => await LoadPublicAvatars();
        _uploadScoreButton.clicked += async () => await UploadScore();
        _getLeaderboardButton.clicked += async () => await GetLeaderboard();
    }

    private void LoadConfiguration()
    {
        _config = ViverseConfigData.LoadFromPrefs();
        _clientIdInput.value = _config.ClientId;
        UpdateConfigStatus();
    }

    private void SaveConfiguration()
    {
        if (string.IsNullOrEmpty(_clientIdInput.value))
        {
            ShowError("Please enter a Client ID");
            return;
        }

        _config.ClientId = _clientIdInput.value;
        _config.SaveToPrefs();
        UpdateConfigStatus();
        ShowMessage("Configuration saved successfully!");
    }

    private void UpdateConfigStatus()
    {
        _configStatus.text = string.IsNullOrEmpty(_config.ClientId)
            ? "No Client ID configured"
            : $"Current Client ID: {_config.ClientId}";
    }

    private async void StartLogin()
    {
        if (string.IsNullOrEmpty(_config.ClientId))
        {
            ShowError("Please configure your Client ID first");
            return;
        }

        SetLoading(true, "Initializing SDK...");

        try
        {
            _core = new ViverseCore();
            HostConfig hostConfig = GetEnvironmentConfig();
            ViverseResult<bool> initResult = await _core.Initialize(hostConfig, destroyCancellationToken);

            if (!initResult.IsSuccess)
            {
                ShowError($"Failed to initialize SDK: {initResult.ErrorMessage}");
                return;
            }

            bool ssoInitSuccess = _core.SSOService.Initialize(_config.ClientId);
            if (!ssoInitSuccess)
            {
                ShowError("Failed to initialize SSO service");
                return;
            }

            URLUtils.URLParts urlParts = URLUtils.ParseURL(Application.absoluteURL);
            ViverseResult<LoginResult> loginResult = await _core.SSOService.LoginWithRedirect(urlParts);

            if (loginResult.IsSuccess)
            {
                _loginResult.value = "Login initiated - page will refresh...";
            }
            else
            {
                ShowError($"Login failed: {loginResult.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error during login: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void LogoutUser()
    {
        if (!CheckInitialization()) return;

        SetLoading(true, "Logging out...");
        try
        {
            URLUtils.URLParts urlParts = URLUtils.ParseURL(Application.absoluteURL);
            ViverseResult<bool> logoutResult = await _core.SSOService.Logout(urlParts);

            if (logoutResult.IsSuccess)
            {
                // Clear UI state
                _loginResult.value = "";
                _tokenResult.value = "";
                _profileResult.value = "";
                _avatarContainer.Clear();
                _leaderboardResult.value = "";

                // Reset service buttons
                SetServiceButtonsEnabled(false);

                // Enable login button, disable logout button
                _loginButton.SetEnabled(true);
                _logoutButton.SetEnabled(false);

                // Reset initialization flag
                _isInitialized = false;

                // Clear core reference
                _core = null;

                ShowMessage("Logged out successfully");
            }
            else
            {
                ShowError($"Logout failed: {logoutResult.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error during logout: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadProfile()
    {
        if (!CheckInitialization()) return;

        SetLoading(true, "Loading profile...");
        try
        {
            ViverseResult<UserProfile> profileResult = await _core.AvatarService.GetProfile();
            if (profileResult.IsSuccess)
            {
                _profileResult.value = JsonUtility.ToJson(profileResult.Data, true);
            }
            else
            {
                ShowError($"Failed to load profile: {profileResult.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error loading profile: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadMyAvatars()
    {
        if (!CheckInitialization()) return;

        SetLoading(true, "Loading avatars...");
        try
        {
            ViverseResult<AvatarListWrapper> avatarResult = await _core.AvatarService.GetAvatarList();
            if (avatarResult.IsSuccess)
            {
                DisplayAvatars(avatarResult.Data.avatars);
            }
            else
            {
                ShowError($"Failed to load avatars: {avatarResult.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error loading avatars: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadPublicAvatars()
    {
        if (!CheckInitialization()) return;

        SetLoading(true, "Loading public avatars...");
        try
        {
            ViverseResult<AvatarListWrapper> avatarResult = await _core.AvatarService.GetPublicAvatarList();
            if (avatarResult.IsSuccess)
            {
                DisplayAvatars(avatarResult.Data.avatars);
            }
            else
            {
                ShowError($"Failed to load public avatars: {avatarResult.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error loading public avatars: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void DisplayAvatars(Avatar[] avatars)
    {
        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
        // Use the VRM extension to display avatars with preview capabilities
        if (_vrmExtension != null)
        {
            _vrmExtension.DisplayAvatars(avatars);
            return;
        }
        #endif

        // Fallback display without VRM functionality
        _avatarContainer.Clear();

        if (avatars == null || avatars.Length == 0)
        {
            ShowMessage("No avatars found");
            return;
        }

        foreach (Avatar avatar in avatars)
        {
            if (string.IsNullOrEmpty(avatar.vrmUrl)) continue;

            var card = new VisualElement();
            card.AddToClassList("avatar-card");

            var image = new Image();
            image.AddToClassList("avatar-image");
            if (!string.IsNullOrEmpty(avatar.snapshot))
            {
                StartCoroutine(LoadAvatarImage(image, avatar.snapshot));
            }

            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("avatar-info");

            var avatarIdLabel = new Label($"ID: {avatar.id}");
            avatarIdLabel.AddToClassList("avatar-id");

            infoContainer.Add(avatarIdLabel);
            card.Add(image);
            card.Add(infoContainer);
            _avatarContainer.Add(card);
        }
    }

    public IEnumerator LoadAvatarImage(Image imageElement, string imageUrl)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success) yield break;
            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            imageElement.image = texture;
        }
    }

    private async Task UploadScore()
    {
        if (!CheckInitialization()) return;

        SetLoading(true, "Uploading score...");
        try
        {
            string appId = _appIdInput.value;
            string leaderboardName = _leaderboardNameInput.value;
            string score = _scoreInput.value;

            ViverseResult<LeaderboardResult> result = await _core.LeaderboardService.UploadScore(appId, leaderboardName, score);
            if (result.IsSuccess)
            {
                _leaderboardResult.value = $"Score uploaded successfully\n{JsonUtility.ToJson(result.Data, true)}";
            }
            else
            {
                ShowError($"Failed to upload score: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error uploading score: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task GetLeaderboard()
    {
        if (!CheckInitialization()) return;

        SetLoading(true, "Loading leaderboard...");
        try
        {
            LeaderboardConfig config = LeaderboardConfig.CreateDefault(_leaderboardNameInput.value);
            ViverseResult<LeaderboardResult> result = await _core.LeaderboardService.GetLeaderboardScores(_appIdInput.value, config);

            if (result.IsSuccess)
            {
                _leaderboardResult.value = JsonUtility.ToJson(result.Data, true);
            }
            else
            {
                ShowError($"Failed to get leaderboard: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error getting leaderboard: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }
    public string GetAppId()
    {
	    return _appIdInput?.value ?? string.Empty;
    }

    private async void CheckForCallback()
    {
        string absoluteUrl = Application.absoluteURL;
        if (string.IsNullOrEmpty(absoluteUrl)) return;

        URLUtils.URLParts urlParts = URLUtils.ParseURL(absoluteUrl);
        if (urlParts?.Parameters != null &&
            urlParts.Parameters.ContainsKey("code") &&
            urlParts.Parameters.ContainsKey("state"))
        {
            await HandleRedirectCallback();
        }
    }

    private async Task HandleRedirectCallback()
    {
        SetLoading(true, "Processing login...");
        try
        {
            if (_core == null)
            {
                _core = new ViverseCore();
                HostConfig hostConfig = GetEnvironmentConfig();
                ViverseResult<bool> initResult = await _core.Initialize(hostConfig, destroyCancellationToken);
                if (!initResult.IsSuccess)
                {
                    ShowError($"Failed to initialize SDK during callback: {initResult.ErrorMessage}");
                    return;
                }
                _core.SSOService.Initialize(_config.ClientId);
            }

            ViverseResult<LoginResult> callbackResult = await _core.SSOService.HandleCallback();

            if (callbackResult.IsSuccess && callbackResult.Data?.access_token != null)
            {
                string loginResultStr = JsonUtility.ToJson(callbackResult.Data, true);
                _loginResult.value = loginResultStr;
                ViverseResult<AccessTokenResult> tokenResult = await _core.SSOService.GetAccessToken();
                if (tokenResult.IsSuccess)
                {
                    // For AccessTokenResult, explicitly format all fields
                    string tokenResultStr = JsonUtility.ToJson(tokenResult.Data, true);
                    _tokenResult.value = tokenResultStr;
                    await InitializeServices();
                    _isInitialized = true;
                    SetServiceButtonsEnabled(true);
                    _logoutButton.SetEnabled(true);
                    ShowMessage("Login successful!");
                }
            }
            else
            {
                ShowError($"Login callback failed: {callbackResult.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            ShowError($"Error handling callback: {e.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task InitializeServices()
    {
        try
        {
            ViverseResult<bool> avatarInit = await _core.AvatarService.Initialize();
            if (!avatarInit.IsSuccess)
            {
                Debug.LogWarning($"Failed to initialize Avatar service: {avatarInit.ErrorMessage}");
            }

            ViverseResult<bool> leaderboardInit = await _core.LeaderboardService.Initialize();
            if (!leaderboardInit.IsSuccess)
            {
	            Debug.LogWarning($"Failed to initialize Leaderboard service: {leaderboardInit.ErrorMessage}");
            }
            else
            {
	            // If leaderboard service init succeeded, fetch achievements
	            _achievementExtension?.UpdateLoginState(true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing services: {e.Message}");
            throw;
        }
    }

    private void SetServiceButtonsEnabled(bool enabled)
    {
        _loadProfileButton.SetEnabled(enabled);
        _loadAvatarsButton.SetEnabled(enabled);
        _loadPublicAvatarsButton.SetEnabled(enabled);
        _uploadScoreButton.SetEnabled(enabled);
        _getLeaderboardButton.SetEnabled(enabled);
        _logoutButton.SetEnabled(enabled);

        // Update achievement extension login state
        if (_achievementExtension != null)
        {
	        _achievementExtension.UpdateLoginState(enabled);
        }
    }

    private HostConfig GetEnvironmentConfig()
    {
        HostConfigUtil.HostType hostType = new HostConfigUtil().GetHostTypeFromPageURLIfPossible(Application.absoluteURL);
        return HostConfigLookup.HostTypeToDefaultHostConfig.TryGetValue(hostType, out var config)
            ? config
            : HostConfigLookup.HostTypeToDefaultHostConfig[HostConfigUtil.HostType.PROD];
    }

    private bool CheckInitialization()
    {
        if (!_isInitialized || _core == null)
        {
            ShowError("Services not initialized. Please login first.");
            return false;
        }
        return true;
    }

    public void SetLoading(bool isLoading, string message = "")
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.style.display = isLoading ? DisplayStyle.Flex : DisplayStyle.None;
            if (_loadingText != null)
            {
                _loadingText.text = message;
            }
        }
    }

    public void ShowMessage(string message)
    {
        Debug.Log(message);
        // Implement UI notification system here if needed
    }

    public void ShowError(string message)
    {
        Debug.LogError(message);
        // Implement UI error dialog here if needed
    }

    // Expose core for the VRM extension
    public ViverseCore GetViverseCore() => _core;

    private void OnDestroy()
    {
        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
        if (_vrmExtension != null)
        {
            _vrmExtension.Cleanup();
        }
        #endif

	    // Clean up achievement extension
	    if (_achievementExtension != null)
	    {
		    _achievementExtension.Cleanup();
	    }
    }
}
