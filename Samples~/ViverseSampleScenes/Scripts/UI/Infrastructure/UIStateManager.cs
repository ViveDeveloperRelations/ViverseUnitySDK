using UnityEngine;
using UnityEngine.UIElements;

namespace ViverseUI.Infrastructure
{
    /// <summary>
    /// Implementation of IUIStateManager for managing UI state and user feedback
    /// </summary>
    public class UIStateManager : IUIStateManager
    {
        private readonly VisualElement _loadingOverlay;
        private readonly Label _loadingText;
        private readonly Button _loginButton;
        private readonly Button _logoutButton;
        private readonly Button _loadProfileButton;
        private readonly Button _loadAvatarsButton;
        private readonly Button _loadPublicAvatarsButton;
        private readonly Button _uploadScoreButton;
        private readonly Button _getLeaderboardButton;
        private readonly Button _subscribeRoomButton;
        
        public UIStateManager(VisualElement root)
        {
            // Get references to UI elements for state management
            _loadingOverlay = root.Q<VisualElement>("loading-overlay");
            _loadingText = _loadingOverlay?.Q<Label>("loading-text");
            
            // Get service buttons for enable/disable functionality
            _loginButton = root.Q<Button>("login-button");
            _logoutButton = root.Q<Button>("logout-button");
            _loadProfileButton = root.Q<Button>("load-profile-button");
            _loadAvatarsButton = root.Q<Button>("load-avatars-button");
            _loadPublicAvatarsButton = root.Q<Button>("load-public-avatars-button");
            _uploadScoreButton = root.Q<Button>("upload-score-button");
            _getLeaderboardButton = root.Q<Button>("get-leaderboard-button");
            _subscribeRoomButton = root.Q<Button>("subscribe-room-button");
        }
        
        /// <summary>
        /// Show or hide loading overlay with optional message
        /// </summary>
        /// <param name="isLoading">Whether to show loading state</param>
        /// <param name="message">Optional loading message</param>
        public void SetLoading(bool isLoading, string message = "")
        {
            if (_loadingOverlay == null) return;
            
            _loadingOverlay.style.display = isLoading ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (_loadingText != null && !string.IsNullOrEmpty(message))
            {
                _loadingText.text = message;
            }
        }
        
        /// <summary>
        /// Display a success message to the user
        /// </summary>
        /// <param name="message">Success message to display</param>
        public void ShowMessage(string message)
        {
            Debug.Log($"[UI Message] {message}");
            // TODO: Implement toast notification or status display
            // For now, just log the message
        }
        
        /// <summary>
        /// Display an error message to the user
        /// </summary>
        /// <param name="error">Error message to display</param>
        public void ShowError(string error)
        {
            Debug.LogError($"[UI Error] {error}");
            // TODO: Implement error notification or status display
            // For now, just log the error
        }
        
        /// <summary>
        /// Enable or disable service-related buttons based on authentication state
        /// </summary>
        /// <param name="enabled">Whether to enable service buttons</param>
        public void SetServiceButtonsEnabled(bool enabled)
        {
            // Authentication buttons
            _loginButton?.SetEnabled(!enabled);  // Login disabled when authenticated
            _logoutButton?.SetEnabled(enabled);  // Logout enabled when authenticated
            
            // Service buttons - enabled only when authenticated
            _loadProfileButton?.SetEnabled(enabled);
            _loadAvatarsButton?.SetEnabled(enabled);
            _loadPublicAvatarsButton?.SetEnabled(enabled);
            _uploadScoreButton?.SetEnabled(enabled);
            _getLeaderboardButton?.SetEnabled(enabled);
            _subscribeRoomButton?.SetEnabled(enabled);
        }
    }
}