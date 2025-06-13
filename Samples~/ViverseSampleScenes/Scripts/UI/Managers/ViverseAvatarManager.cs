using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using ViverseUI.Infrastructure;
using ViverseWebGLAPI;
using Avatar = ViverseWebGLAPI.Avatar;

namespace ViverseUI.Managers
{
    /// <summary>
    /// Manages avatar service operations and UI
    /// </summary>
    public class ViverseAvatarManager : ViverseManagerBase
    {
        // UI Elements
        private Button _loadProfileButton;
        private Button _loadAvatarsButton;
        private Button _loadPublicAvatarsButton;
        private Button _getActiveAvatarButton;
        private TextField _avatarIdInput;
        private Button _getAvatarByIdButton;
        private TextField _profileResult;
        private TextField _activeAvatarResult;
        private TextField _avatarByIdResult;
        private VisualElement _avatarContainer;

        // State
        private MonoBehaviour _monoBehaviour; // For coroutine support

        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
        private ViverseVRMExtension _vrmExtension;
        #endif

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Service context</param>
        /// <param name="root">Root UI element</param>
        /// <param name="monoBehaviour">MonoBehaviour for coroutine support</param>
        public ViverseAvatarManager(IViverseServiceContext context, VisualElement root, MonoBehaviour monoBehaviour)
            : base(context, root)
        {
            _monoBehaviour = monoBehaviour;
        }

        /// <summary>
        /// Initialize UI elements
        /// </summary>
        protected override void InitializeUIElements()
        {
            _loadProfileButton = Root.Q<Button>("load-profile-button");
            _loadAvatarsButton = Root.Q<Button>("load-avatars-button");
            _loadPublicAvatarsButton = Root.Q<Button>("load-public-avatars-button");
            _getActiveAvatarButton = Root.Q<Button>("get-active-avatar-button");
            _avatarIdInput = Root.Q<TextField>("avatar-id-input");
            _getAvatarByIdButton = Root.Q<Button>("get-avatar-by-id-button");
            _profileResult = Root.Q<TextField>("profile-result");
            _activeAvatarResult = Root.Q<TextField>("active-avatar-result");
            _avatarByIdResult = Root.Q<TextField>("avatar-by-id-result");
            _avatarContainer = Root.Q<VisualElement>("avatar-container");

            // Initialize VRM support if available
            #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
            InitializeVRMSupport();
            #endif
        }

        /// <summary>
        /// Setup event handlers
        /// </summary>
        protected override void SetupEventHandlers()
        {
            if (_loadProfileButton != null)
                _loadProfileButton.clicked += async () => await LoadProfile();

            if (_loadAvatarsButton != null)
                _loadAvatarsButton.clicked += async () => await LoadMyAvatars();

            if (_loadPublicAvatarsButton != null)
                _loadPublicAvatarsButton.clicked += async () => await LoadPublicAvatars();

            if (_getActiveAvatarButton != null)
                _getActiveAvatarButton.clicked += async () => await GetActiveAvatar();

            if (_getAvatarByIdButton != null)
                _getAvatarByIdButton.clicked += async () => await GetAvatarById();
        }

        /// <summary>
        /// Cleanup event handlers
        /// </summary>
        protected override void CleanupEventHandlers()
        {
            if (_loadProfileButton != null)
                _loadProfileButton.clicked -= async () => await LoadProfile();

            if (_loadAvatarsButton != null)
                _loadAvatarsButton.clicked -= async () => await LoadMyAvatars();

            if (_loadPublicAvatarsButton != null)
                _loadPublicAvatarsButton.clicked -= async () => await LoadPublicAvatars();

            if (_getActiveAvatarButton != null)
                _getActiveAvatarButton.clicked -= async () => await GetActiveAvatar();

            if (_getAvatarByIdButton != null)
                _getAvatarByIdButton.clicked -= async () => await GetAvatarById();
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        protected override void CleanupResources()
        {
            #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
            if (_vrmExtension != null)
            {
                // VRM extension cleanup would go here
            }
            #endif
        }

        #if UNI_VRM_INSTALLED && UNI_GLTF_INSTALLED
        /// <summary>
        /// Initialize VRM support if available
        /// </summary>
        private void InitializeVRMSupport()
        {
            if (_vrmExtension == null && _monoBehaviour != null)
            {
                _vrmExtension = _monoBehaviour.gameObject.GetComponent<ViverseVRMExtension>();
                if (_vrmExtension == null)
                {
                    _vrmExtension = _monoBehaviour.gameObject.AddComponent<ViverseVRMExtension>();
                }

                // Initialize VRM extension with necessary parameters
                // _vrmExtension.Initialize(_monoBehaviour, Root, animationController);
            }
        }
        #endif

        /// <summary>
        /// Load user profile information
        /// </summary>
        private async Task LoadProfile()
        {
            if (!CheckInitialization()) return;

            UIState.SetLoading(true, "Loading profile...");

            try
            {
                var profileResult = await Context.Core.AvatarService.GetProfile();

                if (profileResult.IsSuccess && profileResult.Data != null)
                {
                    var profile = profileResult.Data;

                    string profileText = $"Profile loaded successfully:\n" +
                                       $"Name: {profile.name}\n" +
                                       $"Active Avatar: {(profile.activeAvatar != null ? profile.activeAvatar.id : "None")}";

                    if (_profileResult != null)
                        _profileResult.value = profileText;

                    UIState.ShowMessage("Profile loaded successfully");
                }
                else
                {
                    string errorText = $"Failed to load profile: {profileResult.ErrorMessage}";

                    if (_profileResult != null)
                        _profileResult.value = errorText;

                    UIState.ShowError(errorText);
                }
            }
            catch (Exception e)
            {
                string errorText = $"Error loading profile: {e.Message}";
                Debug.LogError(errorText);

                if (_profileResult != null)
                    _profileResult.value = errorText;

                UIState.ShowError(errorText);
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Load user's private avatars
        /// </summary>
        private async Task LoadMyAvatars()
        {
            if (!CheckInitialization()) return;

            UIState.SetLoading(true, "Loading your avatars...");

            try
            {
                ViverseResult<Avatar[]> avatarsResult = await Context.Core.AvatarService.GetAvatarList();

                if (avatarsResult.IsSuccess && avatarsResult.Data != null)
                {
                    var avatars = avatarsResult.Data;
                    DisplayAvatars(avatars, "My Avatars");
                    UIState.ShowMessage($"Loaded {avatars?.Length ?? 0} private avatars");
                }
                else
                {
                    UIState.ShowError($"Failed to load avatars: {avatarsResult.ErrorMessage}");
                    ClearAvatarContainer();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading avatars: {e.Message}");
                UIState.ShowError($"Error loading avatars: {e.Message}");
                ClearAvatarContainer();
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Load public avatars
        /// </summary>
        private async Task LoadPublicAvatars()
        {
            if (!CheckInitialization()) return;

            UIState.SetLoading(true, "Loading public avatars...");

            try
            {
                ViverseResult<Avatar[]> avatarsResult = await Context.Core.AvatarService.GetPublicAvatarList();

                if (avatarsResult.IsSuccess && avatarsResult.Data != null)
                {
                    Avatar[] avatars = avatarsResult.Data;
                    DisplayAvatars(avatars, "Public Avatars");
                    UIState.ShowMessage($"Loaded {avatars?.Length ?? 0} public avatars");
                }
                else
                {
                    UIState.ShowError($"Failed to load public avatars: {avatarsResult.ErrorMessage}");
                    ClearAvatarContainer();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading public avatars: {e.Message}");
                UIState.ShowError($"Error loading public avatars: {e.Message}");
                ClearAvatarContainer();
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Get active avatar information
        /// </summary>
        private async Task GetActiveAvatar()
        {
            if (!CheckInitialization()) return;

            UIState.SetLoading(true, "Getting active avatar...");

            try
            {
                var activeAvatarResult = await Context.Core.AvatarService.GetActiveAvatar();

                if (activeAvatarResult.IsSuccess && activeAvatarResult.Data != null)
                {
                    Avatar avatar = activeAvatarResult.Data;

                    string avatarText = $"Active Avatar Found:\n" +
                                      $"ID: {avatar.id}\n" +
                                      $"Private: {avatar.isPrivate}\n" +
                                      $"VRM URL: {avatar.vrmUrl}\n" +
                                      $"Head Icon: {avatar.headIconUrl}\n" +
                                      $"Created: {DateTimeOffset.FromUnixTimeMilliseconds(avatar.CreateTime):yyyy-MM-dd HH:mm}";

                    if (_activeAvatarResult != null)
                        _activeAvatarResult.value = avatarText;

                    UIState.ShowMessage("Active avatar retrieved successfully");
                }
                else
                {
                    string noAvatarText = "No active avatar set or failed to retrieve";

                    if (_activeAvatarResult != null)
                        _activeAvatarResult.value = noAvatarText;

                    UIState.ShowMessage(noAvatarText);
                }
            }
            catch (Exception e)
            {
                string errorText = $"Error: {e.Message}";
                Debug.LogError($"Get active avatar failed: {e.Message}");

                if (_activeAvatarResult != null)
                    _activeAvatarResult.value = errorText;

                UIState.ShowError($"Get active avatar failed: {e.Message}");
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Get avatar by specific ID
        /// </summary>
        private async Task GetAvatarById()
        {
            if (!CheckInitialization()) return;

            string avatarId = _avatarIdInput?.value?.Trim();
            if (string.IsNullOrEmpty(avatarId))
            {
                UIState.ShowError("Please enter an Avatar ID");
                return;
            }

            UIState.SetLoading(true, $"Getting avatar {avatarId}...");

            try
            {
                var avatarResult = await Context.Core.AvatarService.GetPublicAvatarByID(avatarId);

                if (avatarResult.IsSuccess && avatarResult.Data != null)
                {
                    var avatar = avatarResult.Data;

                    string avatarText = $"Avatar Found:\n" +
                                      $"ID: {avatar.id}\n" +
                                      $"Private: {avatar.isPrivate}\n" +
                                      $"VRM URL: {avatar.vrmUrl}\n" +
                                      $"Head Icon: {avatar.headIconUrl}\n" +
                                      $"Created: {DateTimeOffset.FromUnixTimeMilliseconds(avatar.CreateTime):yyyy-MM-dd HH:mm}";

                    if (_avatarByIdResult != null)
                        _avatarByIdResult.value = avatarText;

                    UIState.ShowMessage($"Avatar {avatarId} retrieved successfully");
                }
                else
                {
                    string notFoundText = $"Avatar not found or access denied: {avatarResult.ErrorMessage}";

                    if (_avatarByIdResult != null)
                        _avatarByIdResult.value = notFoundText;

                    UIState.ShowError(notFoundText);
                }
            }
            catch (Exception e)
            {
                string errorText = $"Error: {e.Message}";
                Debug.LogError($"Get avatar by ID failed: {e.Message}");

                if (_avatarByIdResult != null)
                    _avatarByIdResult.value = errorText;

                UIState.ShowError($"Get avatar by ID failed: {e.Message}");
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }

        /// <summary>
        /// Display avatars in the UI container
        /// </summary>
        /// <param name="avatars">Array of avatars to display</param>
        /// <param name="title">Section title</param>
        private void DisplayAvatars(Avatar[] avatars, string title)
        {
            if (_avatarContainer == null || avatars == null)
                return;

            // Clear existing content
            _avatarContainer.Clear();

            // Add title
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("avatar-section-title");
            _avatarContainer.Add(titleLabel);

            if (avatars.Length == 0)
            {
                var noAvatarsLabel = new Label("No avatars found");
                noAvatarsLabel.AddToClassList("no-avatars-message");
                _avatarContainer.Add(noAvatarsLabel);
                return;
            }

            // Create grid container
            var gridContainer = new VisualElement();
            gridContainer.AddToClassList("avatar-grid");
            _avatarContainer.Add(gridContainer);

            // Add each avatar
            foreach (var avatar in avatars)
            {
                var avatarCard = CreateAvatarCard(avatar);
                gridContainer.Add(avatarCard);

                // Load avatar image asynchronously
                if (!string.IsNullOrEmpty(avatar.headIconUrl))
                {
                    _ = LoadAvatarImage(avatarCard, avatar.headIconUrl);
                }
            }
        }

        /// <summary>
        /// Create UI card for a single avatar
        /// </summary>
        /// <param name="avatar">Avatar data</param>
        /// <returns>Avatar card visual element</returns>
        private VisualElement CreateAvatarCard(Avatar avatar)
        {
            var card = new VisualElement();
            card.AddToClassList("avatar-card");

            // Avatar image placeholder
            var imageContainer = new VisualElement();
            imageContainer.AddToClassList("avatar-image-container");
            card.Add(imageContainer);

            // Avatar info
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("avatar-info");

            var idLabel = new Label($"ID: {avatar.id}");
            idLabel.AddToClassList("avatar-id");
            infoContainer.Add(idLabel);

            var typeLabel = new Label(avatar.isPrivate ? "Private" : "Public");
            typeLabel.AddToClassList(avatar.isPrivate ? "avatar-private" : "avatar-public");
            infoContainer.Add(typeLabel);

            var dateLabel = new Label($"Created: {DateTimeOffset.FromUnixTimeMilliseconds(avatar.CreateTime):MM/dd/yyyy}");
            dateLabel.AddToClassList("avatar-date");
            infoContainer.Add(dateLabel);

            card.Add(infoContainer);

            return card;
        }

        /// <summary>
        /// Load avatar image for Image UI element (coroutine version for external access)
        /// </summary>
        /// <param name="imageElement">Image UI element to load into</param>
        /// <param name="imageUrl">Image URL to load</param>
        /// <returns>Coroutine enumerator</returns>
        public IEnumerator LoadAvatarImage(Image imageElement, string imageUrl)
        {
            if (imageElement == null || string.IsNullOrEmpty(imageUrl))
                yield break;

            using (var request = UnityWebRequestTexture.GetTexture(imageUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);
                    imageElement.image = texture;
                }
                else
                {
                    Debug.LogWarning($"Failed to load avatar image: {request.error}");
                }
            }
        }

        /// <summary>
        /// Load avatar image asynchronously
        /// </summary>
        /// <param name="avatarCard">Avatar card element</param>
        /// <param name="imageUrl">Image URL to load</param>
        private async Task LoadAvatarImage(VisualElement avatarCard, string imageUrl)
        {
            if (_monoBehaviour == null) return;

            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(imageUrl))
                {
                    var operation = request.SendWebRequest();

                    // Wait for request to complete
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var texture = DownloadHandlerTexture.GetContent(request);

                        // Find image container and set background
                        var imageContainer = avatarCard.Q<VisualElement>(className: "avatar-image-container");
                        if (imageContainer != null)
                        {
                            imageContainer.style.backgroundImage = new StyleBackground(texture);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load avatar image: {request.error}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading avatar image: {e.Message}");
            }
        }

        /// <summary>
        /// Clear avatar container
        /// </summary>
        private void ClearAvatarContainer()
        {
            _avatarContainer?.Clear();
        }
    }
}
